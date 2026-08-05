using System;
using Avalonia.Media;
using Attendly.Models;

namespace Attendly.ViewModels;

/// <summary>Read-only display wrapper around one AttendanceChangeLogEntry - shared by the
/// mobile "Riwayat" screen (this device's own changes) and Desktop's admin-wide view.</summary>
public sealed class ActivityLogRowViewModel
{
    public string SantriNamaPanggilan { get; }
    public string TeacherName { get; }
    public string KelasDisplayName { get; }
    public string StatusChangeText { get; }
    public string TimeDisplay { get; }
    public string AttendanceDateDisplay { get; }
    public string MetaLine { get; }
    public IBrush StatusColor { get; }

    public ActivityLogRowViewModel(AttendanceChangeLogEntry entry)
    {
        SantriNamaPanggilan = entry.SantriNamaPanggilan;
        TeacherName = string.IsNullOrWhiteSpace(entry.TeacherName) ? "Tidak diketahui" : entry.TeacherName;
        KelasDisplayName = entry.TartilLevel.ToDisplayString();

        StatusChangeText = entry.OldStatus is { } oldStatus
            ? $"{oldStatus.ToDisplayLabel()} → {entry.NewStatus.ToDisplayLabel()}"
            : entry.NewStatus.ToDisplayLabel();

        StatusColor = new SolidColorBrush(Color.Parse(entry.NewStatus switch
        {
            AttendanceStatus.Hadir => "#16A34A",
            AttendanceStatus.Alpha => "#DC2626",
            AttendanceStatus.Izin => "#D97706",
            AttendanceStatus.Sakit => "#2563EB",
            AttendanceStatus.Libur => "#9CA3AF",
            _ => "#9CA3AF",
        }));

        var changedAt = new DateTime(entry.ChangedAtTicks).ToLocalTime();
        TimeDisplay = changedAt.ToString("HH:mm");

        var attendanceDate = entry.AttendanceDate.Date;
        AttendanceDateDisplay = attendanceDate == DateTime.Today
            ? "Hari ini"
            : attendanceDate == DateTime.Today.AddDays(-1)
                ? "Kemarin"
                : attendanceDate.ToString("dd MMM");

        MetaLine = $"{KelasDisplayName} • oleh {TeacherName} • {AttendanceDateDisplay}";
    }
}