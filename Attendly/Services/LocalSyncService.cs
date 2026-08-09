using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Timers;
using Attendly.Data;
using Attendly.Models;
using Attendly.Sync;

namespace Attendly.Services;

public interface ILocalSyncService
{
    SyncState CurrentState { get; }
    DateTime? LastSyncedAt { get; }
    event Action<SyncState>? StateChanged;

    /// <summary>Call after any local write; debounces 2s before pushing to Desktop.</summary>
    void RequestSync();

    /// <summary>Deliberate, immediate sync - bypasses the debounce, pushes any pending
    /// records/change-log entries, then pulls the latest roster. Backs the Dashboard's
    /// "Sinkronkan Sekarang" button, for teachers who only connect occasionally
    /// (e.g. once a month) rather than relying on the write-triggered debounce.</summary>
    Task SyncNowAsync();
}

/// <summary>
/// Mobile-side sync client for the Desktop-hosted LAN API (PRD Section 2).
/// Debounces 2s after the last local write, then merges Desktop's response
/// back using the same last-write-wins rule the repository already applies.
/// </summary>
public class LocalSyncService : ILocalSyncService, IDisposable
{
    private readonly AttendanceRepository _repository;
    private readonly Timer _debounceTimer;
    private SyncState _state = SyncState.Offline;

    public SyncState CurrentState
    {
        get => _state;
        private set { _state = value; StateChanged?.Invoke(value); }
    }

    public DateTime? LastSyncedAt { get; private set; }

    public event Action<SyncState>? StateChanged;

    public LocalSyncService(AttendanceRepository repository)
    {
        _repository = repository;
        _debounceTimer = new Timer(2000) { AutoReset = false };
        _debounceTimer.Elapsed += async (_, _) => await PushPendingAsync();
    }

    public void RequestSync()
    {
        CurrentState = SyncState.Pending;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public async Task SyncNowAsync()
    {
        _debounceTimer.Stop(); // a manual sync supersedes any pending debounce
        await PushPendingAsync();
        await PullRosterAsync();
    }

    private async Task PushPendingAsync()
    {
        var pairing = await _repository.GetPairingAsync();
        if (pairing is not { IsPaired: true })
        {
            CurrentState = SyncState.Offline;
            return;
        }

        CurrentState = SyncState.Syncing;

        try
        {
            var pendingRecords = await _repository.GetPendingSyncRecordsAsync();
            var pendingChangeLog = await _repository.GetPendingChangeLogEntriesAsync();

            if (pendingRecords.Count == 0 && pendingChangeLog.Count == 0)
            {
                LastSyncedAt = DateTime.Now;
                CurrentState = SyncState.Synced;
                return;
            }

            using var client = new HttpClient { BaseAddress = new Uri($"http://{pairing.DesktopIp}:{pairing.DesktopPort}") };
            client.DefaultRequestHeaders.Add("X-Pairing-Token", pairing.PairingToken);
            client.Timeout = TimeSpan.FromSeconds(10);

            var request = new SyncPushRequest
            {
                Records = pendingRecords.Select(r => new AttendanceRecordDto
                {
                    SantriId = r.SantriId,
                    TartilLevel = r.TartilLevel,
                    Date = r.Date,
                    StatusCode = r.Status.ToCode(),
                    DicatatPadaTicks = r.DicatatPadaTicks,
                }).ToList(),
                ChangeLogEntries = pendingChangeLog.Select(c => new ChangeLogEntryDto
                {
                    ChangeId = c.ChangeId,
                    SantriId = c.SantriId,
                    SantriNamaPanggilan = c.SantriNamaPanggilan,
                    TartilLevel = c.TartilLevel,
                    AttendanceDate = c.AttendanceDate,
                    OldStatusCode = c.OldStatus?.ToCode(),
                    NewStatusCode = c.NewStatus.ToCode(),
                    TeacherName = c.TeacherName,
                    ChangedAtTicks = c.ChangedAtTicks,
                }).ToList(),
            };

            var response = await client.PostAsJsonAsync("/api/attendance/sync", request);
            if (!response.IsSuccessStatusCode)
            {
                CurrentState = SyncState.Error;
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<SyncPushResponse>();
            await _repository.MarkSyncedAsync(pendingRecords.Select(r => r.Id));
            await _repository.MarkChangeLogSyncedAsync(pendingChangeLog.Select(c => c.Id));

            if (result?.ServerWins is { Count: > 0 } serverWins)
            {
                foreach (var dto in serverWins)
                {
                    await _repository.ApplyIncomingRecordAsync(new AttendanceRecord
                    {
                        SantriId = dto.SantriId,
                        TartilLevel = dto.TartilLevel,
                        Date = dto.Date,
                        Status = AttendanceStatusExtensions.FromCode(dto.StatusCode),
                        DicatatPadaTicks = dto.DicatatPadaTicks,
                    });
                }
            }

            LastSyncedAt = DateTime.Now;
            CurrentState = SyncState.Synced;
        }
        catch
        {
            CurrentState = SyncState.Error;
        }
    }

    /// <summary>Pulls the current roster from Desktop and reconciles it locally - the half
    /// of sync that RequestSync()'s debounce never covered, since nothing about opening
    /// the app or walking within WiFi range ever called it.</summary>
    private async Task PullRosterAsync()
    {
        var pairing = await _repository.GetPairingAsync();
        if (pairing is not { IsPaired: true }) return;

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://{pairing.DesktopIp}:{pairing.DesktopPort}") };
            client.DefaultRequestHeaders.Add("X-Pairing-Token", pairing.PairingToken);
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync("/api/roster");
            if (!response.IsSuccessStatusCode)
            {
                CurrentState = SyncState.Error;
                return;
            }

            var roster = await response.Content.ReadFromJsonAsync<RosterResponse>();
            if (roster is not null)
                await _repository.ApplyIncomingRosterAsync(roster.Santri, roster.Kelas);

            LastSyncedAt = DateTime.Now;
            CurrentState = SyncState.Synced;
        }
        catch
        {
            CurrentState = SyncState.Error;
        }
    }

    public void Dispose() => _debounceTimer.Dispose();
}