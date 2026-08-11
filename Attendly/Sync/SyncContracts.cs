using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Attendly.Models;

namespace Attendly.Sync;

public class SantriDto
{
    public int Id { get; set; }
    public string Nik { get; set; } = string.Empty;
    public string Nama { get; set; } = string.Empty;
    public string NamaPanggilan { get; set; } = string.Empty;
    public char JenisKelaminCode { get; set; }
    public string? TempatLahir { get; set; }
    public string? Alamat { get; set; }
    public int MasukTpqTahun { get; set; }
    public TartilLevel? TartilLevel { get; set; }
    public bool IsActive { get; set; }
}

public class KelasConfigDto
{
    public TartilLevel TartilLevel { get; set; }
    public int SessionDaysMask { get; set; }
}

public class RosterResponse
{
    public List<SantriDto> Santri { get; set; } = new();
    public List<KelasConfigDto> Kelas { get; set; } = new();
}

public class AttendanceRecordDto
{
    public int SantriId { get; set; }
    public TartilLevel TartilLevel { get; set; }
    public DateTime Date { get; set; }
    public char StatusCode { get; set; }
    public long DicatatPadaTicks { get; set; }
}

public class ChangeLogEntryDto
{
    public string ChangeId { get; set; } = string.Empty;
    public int SantriId { get; set; }
    public string SantriNamaPanggilan { get; set; } = string.Empty;
    public TartilLevel TartilLevel { get; set; }
    public DateTime AttendanceDate { get; set; }
    public char? OldStatusCode { get; set; }
    public char NewStatusCode { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public long ChangedAtTicks { get; set; }
}

public class SyncPushRequest
{
    public List<AttendanceRecordDto> Records { get; set; } = new();
    public List<ChangeLogEntryDto> ChangeLogEntries { get; set; } = new();
}

public class SyncPushResponse
{
    public List<AttendanceRecordDto> ServerWins { get; set; } = new();
}

public class HealthResponse
{
    public bool Ok { get; set; }
    public string ServerName { get; set; } = string.Empty;
}

/// <summary>Generates the short, human-typeable pairing token shown on the Desktop Pairing
/// screen (replaces the old 32-char GUID hex token). Crockford's Base32 alphabet deliberately
/// excludes I, L, O, U so nothing is ambiguous when someone reads it off a phone screen and
/// types it into another device by hand. 12 characters = 60 bits of randomness - short enough
/// to type accurately, and since this token only ever needs to resist guessing on a closed
/// school LAN (not the open internet), that's a reasonable trade against the old 128 bits.</summary>
public static class PairingTokenGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // 32 symbols - Crockford Base32
    private const int Length = 12;

    public static string Generate()
    {
        // Alphabet.Length (32) divides evenly into a byte's range (256), so this mapping
        // introduces no modulo bias - every symbol is equally likely.
        var bytes = RandomNumberGenerator.GetBytes(Length);
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}

/// <summary>Encodes/decodes the "ip:port:token" pairing string shared between the QR code and
/// manual entry. The token segment is shown dash-grouped (e.g. "8F3A-9C1D-2E4B") purely for
/// readability - TryParse strips the dashes back out, so grouped and ungrouped forms parse
/// identically, and a shorter encoded string also means a less dense, easier-to-scan QR code.</summary>
public static class PairingCode
{
    public static string Encode(string ip, int port, string token) => $"{ip}:{port}:{GroupForDisplay(token)}";

    private static string GroupForDisplay(string token)
    {
        var clean = token.Replace("-", "").Trim();
        var groups = new List<string>();
        for (var i = 0; i < clean.Length; i += 4)
            groups.Add(clean.Substring(i, Math.Min(4, clean.Length - i)));
        return string.Join("-", groups);
    }

    public static bool TryParse(string raw, out string ip, out int port, out string token)
    {
        ip = string.Empty;
        token = string.Empty;
        port = 0;

        var parts = raw.Trim().Split(':');
        if (parts.Length != 3) return false;

        ip = parts[0].Trim();
        token = parts[2].Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
        return int.TryParse(parts[1].Trim(), out port) && ip.Length > 0 && token.Length > 0;
    }
}