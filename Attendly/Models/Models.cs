using SQLite;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

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
 #endregion