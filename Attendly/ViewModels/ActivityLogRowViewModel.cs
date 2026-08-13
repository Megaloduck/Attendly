using System;
using Avalonia.Media;
using Attendly.Converters;
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

        // Same resource keys as MonthlyGridViewModel.StatusBrushKeys - kept in sync by
        // convention rather than a shared constant, since the two live in different
        // projects (Attendly vs Attendly.Desktop) and neither depends on the other.
        StatusColor = entry.NewStatus switch
        {
            AttendanceStatus.Hadir => ThemeBrush.Resolve("StatusSuccessBrush", "#6E8F73"),
            AttendanceStatus.Alpha => ThemeBrush.Resolve("StatusErrorBrush", "#B2564A"),
            AttendanceStatus.Izin => ThemeBrush.Resolve("StatusWarningBrush", "#C08A4E"),
            AttendanceStatus.Sakit => ThemeBrush.Resolve("StatusInfoBrush", "#7C8FA6"),
            AttendanceStatus.Libur => ThemeBrush.Resolve("StatusNeutralBrush", "#A29C90"),
            _ => ThemeBrush.Resolve("StatusNeutralBrush", "#A29C90"),
        };

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