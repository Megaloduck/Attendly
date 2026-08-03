using System;
using System.Collections.Generic;
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

public class SyncPushRequest
{
    public List<AttendanceRecordDto> Records { get; set; } = new();
}

/// <summary>Records the client pushed that lost the last-write-wins comparison - here's what Desktop actually has instead.</summary>
public class SyncPushResponse
{
    public List<AttendanceRecordDto> ServerWins { get; set; } = new();
}

public class HealthResponse
{
    public bool Ok { get; set; }
    public string ServerName { get; set; } = string.Empty;
}

/// <summary>Encodes/decodes the "ip:port:token" pairing string shared between the QR code and manual entry.</summary>
public static class PairingCode
{
    public static string Encode(string ip, int port, string token) => $"{ip}:{port}:{token}";

    public static bool TryParse(string raw, out string ip, out int port, out string token)
    {
        ip = string.Empty;
        token = string.Empty;
        port = 0;

        var parts = raw.Trim().Split(':');
        if (parts.Length != 3) return false;

        ip = parts[0];
        token = parts[2];
        return int.TryParse(parts[1], out port) && ip.Length > 0 && token.Length > 0;
    }
}