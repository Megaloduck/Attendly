using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Attendly.Data;
using Attendly.Models;
using Attendly.Sync;

namespace Attendly.Desktop.Hosting;

/// <summary>
/// Embeds a small local HTTP API inside the Desktop app - the "database host"
/// half of Attendly's LAN sync design (PRD Section 2). Runs alongside the
/// Avalonia UI in the same process; started once from Program.cs.
/// </summary>
public static class AttendlyApiHost
{
    public const int Port = 5279;

    public static async Task StartAsync(AttendanceRepository repository)
    {
        var builder = WebApplication.CreateBuilder();


        var app = builder.Build();
        app.Urls.Add($"http://0.0.0.0:{Port}");

        app.Use(async (context, next) =>
        {
            var token = context.Request.Headers["X-Pairing-Token"].ToString();
            if (string.IsNullOrEmpty(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing pairing token.");
                return;
            }

            var device = await repository.GetPairedDeviceByTokenAsync(token);
            if (device is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unknown pairing token.");
                return;
            }

            await repository.TouchLastSeenAsync(token);

            // Mobile sends its teacher name percent-encoded (X-Teacher-Name), currently
            // only on the pairing confirmation health check (see MobilePairingViewModel.
            // Connect()). This is what replaces the generic "Perangkat baru" placeholder -
            // set on Desktop at QR-generation time - with the real teacher's name once
            // they actually connect.
            var teacherNameHeader = context.Request.Headers["X-Teacher-Name"].ToString();
            if (!string.IsNullOrEmpty(teacherNameHeader))
            {
                var teacherName = Uri.UnescapeDataString(teacherNameHeader);
                if (!string.IsNullOrWhiteSpace(teacherName))
                    await repository.UpdatePairedDeviceLabelAsync(token, teacherName);
            }

            await next();
        });

        app.MapGet("/api/health", () => Results.Ok(new HealthResponse { Ok = true, ServerName = "Attendly Desktop" }));

        app.MapGet("/api/roster", async () =>
        {
            var santri = await repository.GetAllSantriAsync();
            var kelas = await repository.GetAllKelasConfigsAsync();

            return Results.Ok(new RosterResponse
            {
                Santri = santri.Select(s => new SantriDto
                {
                    Id = s.Id,
                    Nik = s.Nik,
                    Nama = s.Nama,
                    NamaPanggilan = s.NamaPanggilan,
                    JenisKelaminCode = s.JenisKelamin.ToCode(),
                    TempatLahir = s.TempatLahir,
                    Alamat = s.Alamat,
                    MasukTpqTahun = s.MasukTpqTahun,
                    TartilLevel = s.TartilLevel,
                    IsActive = s.IsActive,
                }).ToList(),
                Kelas = kelas.Select(k => new KelasConfigDto
                {
                    TartilLevel = k.TartilLevel,
                    SessionDaysMask = k.SessionDaysMask,
                }).ToList(),
            });
        });

        app.MapGet("/api/attendance/{tartil}/{yyyyMM}", async (TartilLevel tartil, string yyyyMM) =>
        {
            if (yyyyMM.Length != 6 || !int.TryParse(yyyyMM[..4], out var year) || !int.TryParse(yyyyMM[4..], out var month))
                return Results.BadRequest("yyyyMM must be e.g. 202607");

            var records = await repository.GetAttendanceForMonthAsync(tartil, year, month);
            return Results.Ok(records.Select(r => new AttendanceRecordDto
            {
                SantriId = r.SantriId,
                TartilLevel = r.TartilLevel,
                Date = r.Date,
                StatusCode = r.Status.ToCode(),
                DicatatPadaTicks = r.DicatatPadaTicks,
            }).ToList());
        });

        app.MapPost("/api/attendance/sync", async (SyncPushRequest request) =>
        {
            var serverWins = new List<AttendanceRecordDto>();

            foreach (var dto in request.Records)
            {
                var incoming = new AttendanceRecord
                {
                    SantriId = dto.SantriId,
                    TartilLevel = dto.TartilLevel,
                    Date = dto.Date,
                    Status = AttendanceStatusExtensions.FromCode(dto.StatusCode),
                    DicatatPadaTicks = dto.DicatatPadaTicks,
                };

                var applied = await repository.ApplyIncomingRecordAsync(incoming);
                if (!applied)
                {
                    var current = await repository.GetRecordAsync(dto.SantriId, dto.Date);
                    if (current is not null)
                    {
                        serverWins.Add(new AttendanceRecordDto
                        {
                            SantriId = current.SantriId,
                            TartilLevel = current.TartilLevel,
                            Date = current.Date,
                            StatusCode = current.Status.ToCode(),
                            DicatatPadaTicks = current.DicatatPadaTicks,
                        });
                    }
                }
            }

            // Append-only, deduped by ChangeId - safe even if this push is retried.
            // DTOs carry status as char over the wire; the local table stores the
            // enum directly (sqlite-net can't map System.Char to a column type).
            foreach (var dto in request.ChangeLogEntries)
            {
                await repository.ApplyIncomingChangeLogEntryAsync(new AttendanceChangeLogEntry
                {
                    ChangeId = dto.ChangeId,
                    SantriId = dto.SantriId,
                    SantriNamaPanggilan = dto.SantriNamaPanggilan,
                    TartilLevel = dto.TartilLevel,
                    AttendanceDate = dto.AttendanceDate,
                    OldStatus = dto.OldStatusCode is { } oldCode ? AttendanceStatusExtensions.FromCode(oldCode) : null,
                    NewStatus = AttendanceStatusExtensions.FromCode(dto.NewStatusCode),
                    TeacherName = dto.TeacherName,
                    ChangedAtTicks = dto.ChangedAtTicks,
                    Synced = true,
                });
            }

            return Results.Ok(new SyncPushResponse { ServerWins = serverWins });
        });

        await app.RunAsync();
    }
}