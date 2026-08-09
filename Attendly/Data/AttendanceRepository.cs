using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using SQLite;
using Attendly.Models;
using Attendly.Sync;

namespace Attendly.Data;

/// <summary>
/// Resolves where the SQLite database file lives. Each platform head can
/// register its own implementation via DI (Android/iOS get their app-sandbox
/// path in Phase 5, registered *before* calling AddAttendlyCore()); this
/// default works out of the box on Desktop.
/// </summary>
public interface IAppPathProvider
{
    string GetDatabasePath();
}

public class DefaultAppPathProvider : IAppPathProvider
{
    public string GetDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Attendly");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "attendly.db3");
    }
}

/// <summary>
/// Single point of access to the local SQLite database. One instance is
/// shared for the lifetime of the app (registered as a singleton in Services.cs).
/// Call InitializeAsync() once at startup before using anything else.
/// </summary>
public class AttendanceRepository
{
    private readonly SQLiteAsyncConnection _db;

    public AttendanceRepository(IAppPathProvider pathProvider)
    {
        _db = new SQLiteAsyncConnection(pathProvider.GetDatabasePath());
    }

    public async Task InitializeAsync()
    {
        await _db.CreateTableAsync<Santri>();
        await _db.CreateTableAsync<KelasConfig>();
        await _db.CreateTableAsync<AttendanceRecord>();
        await _db.CreateTableAsync<DevicePairing>();
        await _db.CreateTableAsync<SyncCheckpoint>();
        await _db.CreateTableAsync<PairedDevice>();
        await _db.CreateTableAsync<AppSettings>();
        await _db.CreateTableAsync<AttendanceChangeLogEntry>();

        await EnsureDefaultKelasConfigsAsync();
    }

    private async Task EnsureDefaultKelasConfigsAsync()
    {
        foreach (TartilLevel level in Enum.GetValues<TartilLevel>())
        {
            var existing = await _db.Table<KelasConfig>()
                .Where(k => k.TartilLevel == level)
                .FirstOrDefaultAsync();

            if (existing is null)
            {
                await _db.InsertAsync(new KelasConfig { TartilLevel = level });
            }
        }
    }

    // ---------------- Santri ----------------

    public Task<List<Santri>> GetAllSantriAsync() =>
        _db.Table<Santri>().Where(s => s.IsActive).OrderBy(s => s.Nama).ToListAsync();

    public Task<List<Santri>> GetSantriByTartilAsync(TartilLevel level) =>
        _db.Table<Santri>()
            .Where(s => s.IsActive && s.TartilLevel == level)
            .OrderBy(s => s.NamaPanggilan)
            .ToListAsync();

    /// <summary>The Desktop admin "Unassigned" bucket - santri with no Tartil yet.</summary>
    public Task<List<Santri>> GetUnassignedSantriAsync() =>
        _db.Table<Santri>()
            .Where(s => s.IsActive && s.TartilLevel == null)
            .OrderBy(s => s.Nama)
            .ToListAsync();

    public Task<int> UpsertSantriAsync(Santri santri) =>
        santri.Id == 0 ? _db.InsertAsync(santri) : _db.UpdateAsync(santri);

    public Task<int> DeactivateSantriAsync(int santriId) =>
        _db.ExecuteAsync("UPDATE Santri SET IsActive = 0 WHERE Id = ?", santriId);

    /// <summary>
    /// Mobile-side reconciliation of the roster pulled from Desktop's GET /api/roster.
    /// Uses InsertOrReplace keyed on Desktop's own Id (not UpsertSantriAsync's
    /// insert-if-zero/update-otherwise logic, which assumes a locally-generated id and
    /// would silently no-op on a santri this device has never seen before). Preserving
    /// Desktop's Id here is what keeps AttendanceRecordDto.SantriId lined up correctly
    /// across devices on every future sync.
    /// </summary>
    public async Task ApplyIncomingRosterAsync(IEnumerable<SantriDto> santri, IEnumerable<KelasConfigDto> kelas)
    {
        foreach (var dto in santri)
        {
            await _db.InsertOrReplaceAsync(new Santri
            {
                Id = dto.Id,
                Nik = dto.Nik,
                Nama = dto.Nama,
                NamaPanggilan = dto.NamaPanggilan,
                JenisKelamin = JenisKelaminExtensions.FromCode(dto.JenisKelaminCode),
                TempatLahir = dto.TempatLahir,
                Alamat = dto.Alamat,
                MasukTpqTahun = dto.MasukTpqTahun,
                TartilLevel = dto.TartilLevel,
                IsActive = dto.IsActive,
            });
        }

        foreach (var dto in kelas)
        {
            await _db.InsertOrReplaceAsync(new KelasConfig
            {
                TartilLevel = dto.TartilLevel,
                SessionDaysMask = dto.SessionDaysMask,
            });
        }
    }

    // ---------------- KelasConfig ----------------

    public Task<List<KelasConfig>> GetAllKelasConfigsAsync() => _db.Table<KelasConfig>().ToListAsync();

    public Task<KelasConfig?> GetKelasConfigAsync(TartilLevel level) =>
        _db.Table<KelasConfig>().Where(k => k.TartilLevel == level).FirstOrDefaultAsync();

    public Task<int> UpsertKelasConfigAsync(KelasConfig config) => _db.InsertOrReplaceAsync(config);

    // ---------------- AttendanceRecord ----------------

    /// <summary>
    /// Returns the day's attendance for a class, auto-creating Libur ('O')
    /// records for any santri who don't have one yet on non-session days.
    /// </summary>
    public async Task<List<AttendanceRecord>> GetOrInitializeDayAsync(TartilLevel level, DateTime date)
    {
        date = date.Date;
        var config = await GetKelasConfigAsync(level) ?? new KelasConfig { TartilLevel = level };
        var roster = await GetSantriByTartilAsync(level);
        var existing = await _db.Table<AttendanceRecord>()
            .Where(r => r.TartilLevel == level && r.Date == date)
            .ToListAsync();

        var bySantriId = existing.ToDictionary(r => r.SantriId);
        var isSessionDay = config.IsSessionDay(date.DayOfWeek);

        foreach (var santri in roster)
        {
            if (bySantriId.ContainsKey(santri.Id)) continue;
            if (!isSessionDay)
            {
                var libur = new AttendanceRecord
                {
                    SantriId = santri.Id,
                    TartilLevel = level,
                    Date = date,
                    Status = AttendanceStatus.Libur,
                    DicatatPadaTicks = DateTime.UtcNow.Ticks,
                    SyncState = SyncState.Pending,
                };
                libur.Id = await _db.InsertAsync(libur);
                bySantriId[santri.Id] = libur;
            }
        }

        return bySantriId.Values.OrderBy(r => r.SantriId).ToList();
    }

    /// <summary>
    /// Marks (or updates) one santri's status for one day, and appends a
    /// what/who/when entry to the activity log.
    /// </summary>
    public async Task MarkAttendanceAsync(int santriId, string namaPanggilan, TartilLevel level, DateTime date, AttendanceStatus status)
    {
        date = date.Date;
        var existing = await _db.Table<AttendanceRecord>()
            .Where(r => r.SantriId == santriId && r.Date == date)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow.Ticks;
        var oldStatus = existing?.Status;

        if (existing is null)
        {
            await _db.InsertAsync(new AttendanceRecord
            {
                SantriId = santriId,
                TartilLevel = level,
                Date = date,
                Status = status,
                DicatatPadaTicks = now,
                SyncState = SyncState.Pending,
            });
        }
        else
        {
            existing.Status = status;
            existing.DicatatPadaTicks = now;
            existing.SyncState = SyncState.Pending;
            await _db.UpdateAsync(existing);
        }

        await LogChangeAsync(santriId, namaPanggilan, level, date, oldStatus, status);
    }

    /// <summary>
    /// Marks several santri at once with the same status in a single transaction -
    /// backs the mobile "Tandai Semua Hadir" bulk action. Logs one activity entry per santri.
    /// </summary>
    public async Task MarkManyAsync(IEnumerable<(int SantriId, string NamaPanggilan)> santri, TartilLevel level, DateTime date, AttendanceStatus status)
    {
        date = date.Date;
        var now = DateTime.UtcNow.Ticks;
        var pairing = await GetPairingAsync();
        var teacherName = string.IsNullOrWhiteSpace(pairing?.TeacherName) ? "Tidak diketahui" : pairing!.TeacherName!;
        var santriList = santri.ToList();

        await _db.RunInTransactionAsync(conn =>
        {
            foreach (var (santriId, namaPanggilan) in santriList)
            {
                var existing = conn.Table<AttendanceRecord>()
                    .Where(r => r.SantriId == santriId && r.Date == date)
                    .FirstOrDefault();

                var oldStatus = existing?.Status;

                if (existing is null)
                {
                    conn.Insert(new AttendanceRecord
                    {
                        SantriId = santriId,
                        TartilLevel = level,
                        Date = date,
                        Status = status,
                        DicatatPadaTicks = now,
                        SyncState = SyncState.Pending,
                    });
                }
                else
                {
                    existing.Status = status;
                    existing.DicatatPadaTicks = now;
                    existing.SyncState = SyncState.Pending;
                    conn.Update(existing);
                }

                conn.Insert(new AttendanceChangeLogEntry
                {
                    ChangeId = Guid.NewGuid().ToString("N"),
                    SantriId = santriId,
                    SantriNamaPanggilan = namaPanggilan,
                    TartilLevel = level,
                    AttendanceDate = date,
                    OldStatus = oldStatus,
                    NewStatus = status,
                    TeacherName = teacherName,
                    ChangedAtTicks = now,
                });
            }
        });
    }

    private async Task LogChangeAsync(int santriId, string namaPanggilan, TartilLevel level, DateTime date, AttendanceStatus? oldStatus, AttendanceStatus newStatus)
    {
        var pairing = await GetPairingAsync();
        var teacherName = string.IsNullOrWhiteSpace(pairing?.TeacherName) ? "Tidak diketahui" : pairing!.TeacherName!;

        await _db.InsertAsync(new AttendanceChangeLogEntry
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            SantriId = santriId,
            SantriNamaPanggilan = namaPanggilan,
            TartilLevel = level,
            AttendanceDate = date.Date,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            TeacherName = teacherName,
            ChangedAtTicks = DateTime.UtcNow.Ticks,
        });
    }

    /// <summary>Most recent activity, newest first - backs both the mobile "Riwayat" screen
    /// (this device's own changes) and Desktop's aggregated admin view.</summary>
    public Task<List<AttendanceChangeLogEntry>> GetRecentChangeLogAsync(int take = 100) =>
        _db.Table<AttendanceChangeLogEntry>().OrderByDescending(c => c.ChangedAtTicks).Take(take).ToListAsync();

    public Task<List<AttendanceChangeLogEntry>> GetPendingChangeLogEntriesAsync() =>
        _db.Table<AttendanceChangeLogEntry>().Where(c => !c.Synced).ToListAsync();

    public async Task MarkChangeLogSyncedAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids)
            await _db.ExecuteAsync("UPDATE AttendanceChangeLog SET Synced = 1 WHERE Id = ?", id);
    }

    /// <summary>Desktop-side: applies an incoming change-log entry from a mobile push,
    /// deduped by ChangeId so a retried sync push never double-logs.</summary>
    public async Task ApplyIncomingChangeLogEntryAsync(AttendanceChangeLogEntry incoming)
    {
        var existing = await _db.Table<AttendanceChangeLogEntry>()
            .Where(c => c.ChangeId == incoming.ChangeId)
            .FirstOrDefaultAsync();

        if (existing is null)
            await _db.InsertAsync(incoming);
    }

    /// <summary>
    /// Applies a record coming from the sync API using last-write-wins.
    /// Returns true if the incoming record won and was applied.
    /// </summary>
    public async Task<bool> ApplyIncomingRecordAsync(AttendanceRecord incoming)
    {
        var existing = await _db.Table<AttendanceRecord>()
            .Where(r => r.SantriId == incoming.SantriId && r.Date == incoming.Date)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            incoming.SyncState = SyncState.Synced;
            await _db.InsertAsync(incoming);
            return true;
        }

        if (incoming.DicatatPadaTicks <= existing.DicatatPadaTicks)
            return false; // local copy is newer or equal - keep it

        existing.Status = incoming.Status;
        existing.DicatatPadaTicks = incoming.DicatatPadaTicks;
        existing.SyncState = SyncState.Synced;
        await _db.UpdateAsync(existing);
        return true;
    }

    public Task<List<AttendanceRecord>> GetPendingSyncRecordsAsync() =>
        _db.Table<AttendanceRecord>().Where(r => r.SyncState == SyncState.Pending).ToListAsync();

    public async Task MarkSyncedAsync(IEnumerable<int> recordIds)
    {
        foreach (var id in recordIds)
        {
            await _db.ExecuteAsync(
                "UPDATE AttendanceRecord SET SyncState = ? WHERE Id = ?",
                (int)SyncState.Synced, id);
        }
    }

    public Task<List<AttendanceRecord>> GetAttendanceForMonthAsync(TartilLevel level, int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        return _db.Table<AttendanceRecord>()
            .Where(r => r.TartilLevel == level && r.Date >= start && r.Date < end)
            .ToListAsync();
    }

    /// <summary>All attendance across every Kelas for one day - backs the mobile Dashboard's
    /// cross-class summary cards. Unlike GetOrInitializeDayAsync, this never auto-inserts
    /// Libur records; it's a pure read of whatever already exists.</summary>
    public Task<List<AttendanceRecord>> GetAttendanceForDateAsync(DateTime date) =>
        _db.Table<AttendanceRecord>().Where(r => r.Date == date.Date).ToListAsync();

    /// <summary>Single-record lookup used by the Desktop API to report what it actually holds after a conflict.</summary>
    public Task<AttendanceRecord?> GetRecordAsync(int santriId, DateTime date) =>
         _db.Table<AttendanceRecord>().Where(r => r.SantriId == santriId && r.Date == date.Date).FirstOrDefaultAsync();

    /// <summary>How many santri in this Tartil already have an attendance record for the
    /// given date - drives the "X/Y sudah diabsen" progress badge on the Kelas picker.</summary>
    public Task<int> GetMarkedCountForDateAsync(TartilLevel level, DateTime date) =>
        _db.Table<AttendanceRecord>().Where(r => r.TartilLevel == level && r.Date == date.Date).CountAsync();

    // ---------------- Device pairing ----------------

    public Task<DevicePairing?> GetPairingAsync() =>
        _db.Table<DevicePairing>().Where(p => p.Id == 1).FirstOrDefaultAsync();

    public Task<int> SavePairingAsync(string ip, int port, string token, string teacherName) =>
        _db.InsertOrReplaceAsync(new DevicePairing
        {
            Id = 1,
            DesktopIp = ip,
            DesktopPort = port,
            PairingToken = token,
            IsPaired = true,
            TeacherName = teacherName,
        });

    public async Task<int> ClearPairingAsync()
    {
        // Preserve the teacher's name across a re-pair - no need to make them retype it.
        var existing = await GetPairingAsync();
        return await _db.InsertOrReplaceAsync(new DevicePairing
        {
            Id = 1,
            IsPaired = false,
            TeacherName = existing?.TeacherName,
        });
    }

    // ---------------- Desktop's paired-device registry ----------------

    public Task<List<PairedDevice>> GetPairedDevicesAsync() =>
        _db.Table<PairedDevice>().OrderByDescending(d => d.PairedAtTicks).ToListAsync();

    public Task<PairedDevice?> GetPairedDeviceByTokenAsync(string token) =>
        _db.Table<PairedDevice>().Where(d => d.Token == token).FirstOrDefaultAsync();

    public Task<int> AddPairedDeviceAsync(string token, string label) =>
        _db.InsertAsync(new PairedDevice { Token = token, Label = label, PairedAtTicks = DateTime.UtcNow.Ticks });

    public Task<int> RemovePairedDeviceAsync(int id) => _db.DeleteAsync<PairedDevice>(id);

    public Task TouchLastSeenAsync(string token) =>
        _db.ExecuteAsync("UPDATE PairedDevice SET LastSeenTicks = ? WHERE Token = ?", DateTime.UtcNow.Ticks, token);

    // ---------------- App settings ----------------
    public Task<AppSettings?> GetAppSettingsAsync() =>
        _db.Table<AppSettings>().Where(s => s.Id == 1).FirstOrDefaultAsync();

    public Task<int> SaveAppSettingsAsync(AppSettings settings) => _db.InsertOrReplaceAsync(settings);

    // ---------------- Sync checkpoints ----------------

    public Task<SyncCheckpoint?> GetSyncCheckpointAsync(TartilLevel level, string yearMonth) =>
        _db.Table<SyncCheckpoint>()
            .Where(c => c.TartilLevel == level && c.YearMonth == yearMonth)
            .FirstOrDefaultAsync();

    public Task<int> SaveSyncCheckpointAsync(TartilLevel level, string yearMonth, long ticks) =>
        _db.InsertOrReplaceAsync(new SyncCheckpoint
        {
            TartilLevel = level,
            YearMonth = yearMonth,
            LastSyncedTicks = ticks,
        });
}