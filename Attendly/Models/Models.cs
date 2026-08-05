using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Attendly.Models;

#region Enums

/// <summary>
/// The seven Tartil levels taught at TPQ Al-Khoirot, matching the class sheets
/// in the school's official Excel attendance manual (A1-A6 + MARHALLAH).
/// </summary>
public enum TartilLevel
{
    AtTartil1 = 1,
    AtTartil2 = 2,
    AtTartil3 = 3,
    AtTartil4 = 4,
    AtTartil5 = 5,
    AtTartil6 = 6,
    Marhallah = 7,
}

public static class TartilLevelExtensions
{
    /// <summary>Matches the exact "TARTIL" text used in the real Excel manual.</summary>
    public static string ToDisplayString(this TartilLevel level) => level switch
    {
        TartilLevel.AtTartil1 => "AT-TARTIL 1",
        TartilLevel.AtTartil2 => "AT-TARTIL 2",
        TartilLevel.AtTartil3 => "AT-TARTIL 3",
        TartilLevel.AtTartil4 => "AT-TARTIL 4",
        TartilLevel.AtTartil5 => "AT-TARTIL 5",
        TartilLevel.AtTartil6 => "AT-TARTIL 6",
        TartilLevel.Marhallah => "MARHALLAH",
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>Parses the "TARTIL" column text as it appears in the real manual.</summary>
    public static bool TryParse(string? text, out TartilLevel level)
    {
        switch (text?.Trim().ToUpperInvariant())
        {
            case "AT-TARTIL 1": level = TartilLevel.AtTartil1; return true;
            case "AT-TARTIL 2": level = TartilLevel.AtTartil2; return true;
            case "AT-TARTIL 3": level = TartilLevel.AtTartil3; return true;
            case "AT-TARTIL 4": level = TartilLevel.AtTartil4; return true;
            case "AT-TARTIL 5": level = TartilLevel.AtTartil5; return true;
            case "AT-TARTIL 6": level = TartilLevel.AtTartil6; return true;
            case "MARHALLAH": level = TartilLevel.Marhallah; return true;
            default: level = default; return false;
        }
    }
}

/// <summary>Gender, matching "JENIS KELAMIN" (L/P) on the master roster sheet.</summary>
public enum JenisKelamin
{
    LakiLaki,
    Perempuan,
}

public static class JenisKelaminExtensions
{
    public static char ToCode(this JenisKelamin jk) => jk == JenisKelamin.LakiLaki ? 'L' : 'P';

    public static JenisKelamin FromCode(char code) =>
        char.ToUpperInvariant(code) == 'L' ? JenisKelamin.LakiLaki : JenisKelamin.Perempuan;
}

/// <summary>
/// Daily attendance status. Codes verified against every day-cell in the real
/// July 2026 workbook: only H, A, S, I, O ever appear - nothing else.
/// NOTE: Libur maps to 'O', not 'L' - do not assume a first-letter mapping.
/// </summary>
public enum AttendanceStatus
{
    Hadir,
    Alpha,
    Izin,
    Sakit,
    Libur,
}

public static class AttendanceStatusExtensions
{
    public static char ToCode(this AttendanceStatus status) => status switch
    {
        AttendanceStatus.Hadir => 'H',
        AttendanceStatus.Alpha => 'A',
        AttendanceStatus.Izin => 'I',
        AttendanceStatus.Sakit => 'S',
        AttendanceStatus.Libur => 'O',
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static AttendanceStatus FromCode(char code) => char.ToUpperInvariant(code) switch
    {
        'H' => AttendanceStatus.Hadir,
        'A' => AttendanceStatus.Alpha,
        'I' => AttendanceStatus.Izin,
        'S' => AttendanceStatus.Sakit,
        'O' => AttendanceStatus.Libur,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown attendance code."),
    };

    /// <summary>Indonesian display label - shared by the attendance rows and the activity log.</summary>
    public static string ToDisplayLabel(this AttendanceStatus status) => status switch
    {
        AttendanceStatus.Hadir => "Hadir",
        AttendanceStatus.Alpha => "Alpha",
        AttendanceStatus.Izin => "Izin",
        AttendanceStatus.Sakit => "Sakit",
        AttendanceStatus.Libur => "Libur",
        _ => "?",
    };
}

/// <summary>Per-record sync status against the Desktop-hosted local API.</summary>
public enum SyncState
{
    Offline,
    Idle,
    Pending,
    Syncing,
    Synced,
    Error,
}

public enum ThemeMode
{
    Light,
    Dark,
}

#endregion

#region Tables

[Table("Santri")]
public class Santri
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_Santri_Nik", Unique = true)]
    [NotNull, MaxLength(32)]
    public string Nik { get; set; } = string.Empty;

    [NotNull]
    public string Nama { get; set; } = string.Empty;

    [NotNull]
    public string NamaPanggilan { get; set; } = string.Empty;

    public JenisKelamin JenisKelamin { get; set; }

    /// <summary>Optional - populated in the real roster ~88-93% of the time.</summary>
    public string? TempatLahir { get; set; }

    /// <summary>Optional - populated in the real roster ~88-93% of the time.</summary>
    public string? Alamat { get; set; }

    public int MasukTpqTahun { get; set; }

    /// <summary>
    /// Nullable: 3 of 75 real santri have no Tartil assigned yet. These show up
    /// in the Desktop admin "Unassigned" bucket and are excluded from daily
    /// attendance marking until assigned.
    /// </summary>
    public TartilLevel? TartilLevel { get; set; }

    public bool IsActive { get; set; } = true;
}

[Table("KelasConfig")]
public class KelasConfig
{
    [PrimaryKey]
    public TartilLevel TartilLevel { get; set; }

    /// <summary>
    /// Bitmask over System.DayOfWeek (bit N = 1 &lt;&lt; (int)DayOfWeek).
    /// Default is Monday-Saturday, matching the school's real session pattern.
    /// </summary>
    public int SessionDaysMask { get; set; } = DefaultSessionDaysMask;

    public const int DefaultSessionDaysMask =
        (1 << (int)DayOfWeek.Monday) |
        (1 << (int)DayOfWeek.Tuesday) |
        (1 << (int)DayOfWeek.Wednesday) |
        (1 << (int)DayOfWeek.Thursday) |
        (1 << (int)DayOfWeek.Friday) |
        (1 << (int)DayOfWeek.Saturday);

    public bool IsSessionDay(DayOfWeek day) => (SessionDaysMask & (1 << (int)day)) != 0;
}

[Table("AttendanceRecord")]
public class AttendanceRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_Attendance_Santri_Date", Unique = true, Order = 1)]
    public int SantriId { get; set; }

    /// <summary>Denormalized for fast per-class queries without a join.</summary>
    public TartilLevel TartilLevel { get; set; }

    [Indexed(Name = "UX_Attendance_Santri_Date", Unique = true, Order = 2)]
    public DateTime Date { get; set; }

    public AttendanceStatus Status { get; set; }

    /// <summary>
    /// Timestamp (UTC ticks) the record was last written on *this* device.
    /// The sync merge rule is last-write-wins by this value.
    /// </summary>
    public long DicatatPadaTicks { get; set; }

    public SyncState SyncState { get; set; } = SyncState.Idle;
}

/// <summary>
/// Single-row table (Id is always 1) holding this device's pairing with the
/// Desktop-hosted local sync API. Populated by the QR/manual pairing flow (Phase 3).
/// </summary>
[Table("DevicePairing")]
public class DevicePairing
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public string? DesktopIp { get; set; }
    public int? DesktopPort { get; set; }
    public string? PairingToken { get; set; }
    public bool IsPaired { get; set; }

    /// <summary>The teacher's own name, entered once during pairing. Stamped onto every
    /// AttendanceChangeLogEntry this device writes - this is the "who" in the activity log.</summary>
    public string? TeacherName { get; set; }
}

/// <summary>Tracks the last successful sync per Tartil/month, for incremental pulls.</summary>
[Table("SyncCheckpoint")]
public class SyncCheckpoint
{
    [Indexed(Name = "UX_Checkpoint_Tartil_Month", Unique = true, Order = 1)]
    public TartilLevel TartilLevel { get; set; }

    /// <summary>Format: "yyyyMM".</summary>
    [Indexed(Name = "UX_Checkpoint_Tartil_Month", Unique = true, Order = 2)]
    public string YearMonth { get; set; } = string.Empty;

    public long LastSyncedTicks { get; set; }
}

/// <summary>
/// Desktop-side registry of phones/tablets allowed to sync against the local
/// API (PRD Section 2). One row per paired device's token.
/// </summary>
[Table("PairedDevice")]
public class PairedDevice
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_PairedDevice_Token", Unique = true)]
    [NotNull]
    public string Token { get; set; } = string.Empty;

    public string Label { get; set; } = "Perangkat baru";
    public long PairedAtTicks { get; set; }
    public long? LastSeenTicks { get; set; }
}
/// <summary>Single-row table (Id is always 1) holding the person's theme preference.</summary>
[Table("AppSettings")]
public class AppSettings
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public ThemeMode ThemeMode { get; set; } = ThemeMode.Light;
}

/// <summary>
/// One "what/who/when" entry: a single attendance mark or change. Written locally on
/// whichever mobile device made the change, then pushed to Desktop alongside the
/// AttendanceRecord itself so admin has a full cross-teacher activity log.
/// Append-only - never updated after insert, only ever read.
/// </summary>
[Table("AttendanceChangeLog")]
public class AttendanceChangeLogEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Client-generated (Guid) at the moment of the change. Lets Desktop dedupe
    /// entries if a sync push is retried, since AttendanceRecord's own PK is per-device.</summary>
    [Indexed(Name = "UX_ChangeLog_ChangeId", Unique = true)]
    [NotNull]
    public string ChangeId { get; set; } = string.Empty;

    public int SantriId { get; set; }

    /// <summary>Denormalized so the log still reads correctly even if the santri is later renamed.</summary>
    public string SantriNamaPanggilan { get; set; } = string.Empty;

    public TartilLevel TartilLevel { get; set; }

    /// <summary>The attendance day being marked/changed (not necessarily today - date nav lets
    /// a teacher correct a past day).</summary>
    public DateTime AttendanceDate { get; set; }

    /// <summary>Null if this was the first time this santri/day was marked. Stored as the
    /// AttendanceStatus enum, not char - sqlite-net-pcl's ORM doesn't know how to map
    /// System.Char to a SQL column type, only enums/strings/numerics/etc.</summary>
    public AttendanceStatus? OldStatus { get; set; }
    public AttendanceStatus NewStatus { get; set; }

    public string TeacherName { get; set; } = string.Empty;

    /// <summary>UTC ticks - when the change actually happened (the "when").</summary>
    public long ChangedAtTicks { get; set; }

    /// <summary>Mobile-only: whether this entry has been pushed to Desktop yet.
    /// Meaningless (always true) on Desktop's own copy of the table.</summary>
    public bool Synced { get; set; }
}

#endregion