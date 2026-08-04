using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Attendly.Controls;
using Attendly.Models;

namespace Attendly.Converters;

/// <summary>Colors an attendance chip based on whether its code (H/A/I/S) matches the row's current status.</summary>
public class StatusChipBrushConverter : IValueConverter
{
    private static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#E5E7EB"));
    private static readonly IBrush Hadir = new SolidColorBrush(Color.Parse("#16A34A"));
    private static readonly IBrush Alpha = new SolidColorBrush(Color.Parse("#DC2626"));
    private static readonly IBrush Izin = new SolidColorBrush(Color.Parse("#D97706"));
    private static readonly IBrush Sakit = new SolidColorBrush(Color.Parse("#2563EB"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AttendanceStatus status || parameter is not string code || code.Length == 0)
            return Neutral;

        if (status.ToCode() != char.ToUpperInvariant(code[0]))
            return Neutral;

        return char.ToUpperInvariant(code[0]) switch
        {
            'H' => Hadir,
            'A' => Alpha,
            'I' => Izin,
            'S' => Sakit,
            _ => Neutral,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps SyncState to the top-bar badge color. Live once Phase 3's LocalSyncService exists.</summary>
public class SyncStateToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SyncState state) return Brushes.Gray;
        return state switch
        {
            SyncState.Offline or SyncState.Idle => new SolidColorBrush(Color.Parse("#9CA3AF")),
            SyncState.Pending => new SolidColorBrush(Color.Parse("#D97706")),
            SyncState.Syncing => new SolidColorBrush(Color.Parse("#2563EB")),
            SyncState.Synced => new SolidColorBrush(Color.Parse("#16A34A")),
            SyncState.Error => new SolidColorBrush(Color.Parse("#DC2626")),
            _ => Brushes.Gray,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps SyncState to the icon shown in the sync badge.</summary>
public class SyncStateToIconKindConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SyncState state) return LucideIconKind.WifiOff;
        return state switch
        {
            SyncState.Offline => LucideIconKind.WifiOff,
            SyncState.Idle => LucideIconKind.Wifi,
            SyncState.Pending or SyncState.Syncing => LucideIconKind.RefreshCw,
            SyncState.Synced => LucideIconKind.CircleCheck,
            SyncState.Error => LucideIconKind.CircleAlert,
            _ => LucideIconKind.WifiOff,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Icon reflects the theme currently active - Sun while Light, Moon while Dark.</summary>
public class ThemeModeToIconKindConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
value is ThemeMode.Dark ? LucideIconKind.Moon : LucideIconKind.Sun;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
throw new NotSupportedException();
    }

public class ThemeModeToLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
value is ThemeMode.Dark ? "Mode Gelap" : "Mode Terang";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
throw new NotSupportedException();
}