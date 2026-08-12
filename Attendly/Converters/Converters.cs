using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Attendly.Controls;
using Attendly.Models;

namespace Attendly.Converters;

/// <summary>
/// Shared lookup used by every converter below that needs a themed brush instead of a
/// literal hex - resolves against whichever ResourceDictionary (Light/Dark) is currently
/// active, so a converter's output tracks theme changes the same way a DynamicResource
/// binding in XAML would.
///
/// Caveat (same one MonthlyGridView.axaml.cs already documents for its own code-behind
/// coloring): a converter only re-runs when its *bound source value* changes, not when the
/// theme flips on its own. In practice this is fine here - SyncState, IsActive, and
/// AttendanceStatus all change often enough during normal use - but if a stale color is
/// ever visible right after a light/dark toggle with no other UI change, the fix is to have
/// the owning ViewModel re-raise PropertyChanged for the bound property on
/// IThemeService.ModeChanged, not to change this helper.
/// </summary>
internal static class ThemeBrush
{
    public static IBrush Resolve(string resourceKey, string fallbackHex)
    {
        if (Application.Current is { } app &&
            app.TryGetResource(resourceKey, app.ActualThemeVariant, out var value) &&
            value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}

/// <summary>Colors an attendance chip based on whether its code (H/A/I/S) matches the row's current status.</summary>
public class StatusChipBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AttendanceStatus status || parameter is not string code || code.Length == 0)
            return ThemeBrush.Resolve("SurfaceMutedBrush", "#EFE9DF");

        if (status.ToCode() != char.ToUpperInvariant(code[0]))
            return ThemeBrush.Resolve("SurfaceMutedBrush", "#EFE9DF");

        return char.ToUpperInvariant(code[0]) switch
        {
            'H' => ThemeBrush.Resolve("StatusSuccessBrush", "#6E8F73"),
            'A' => ThemeBrush.Resolve("StatusErrorBrush", "#B2564A"),
            'I' => ThemeBrush.Resolve("StatusWarningBrush", "#C08A4E"),
            'S' => ThemeBrush.Resolve("StatusInfoBrush", "#7C8FA6"),
            _ => ThemeBrush.Resolve("SurfaceMutedBrush", "#EFE9DF"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps SyncState to the top-bar badge color.</summary>
public class SyncStateToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SyncState state) return ThemeBrush.Resolve("StatusNeutralBrush", "#A29C90");
        return state switch
        {
            SyncState.Offline or SyncState.Idle => ThemeBrush.Resolve("StatusNeutralBrush", "#A29C90"),
            SyncState.Pending => ThemeBrush.Resolve("StatusWarningBrush", "#C08A4E"),
            SyncState.Syncing => ThemeBrush.Resolve("AccentBrush", "#BD5B3D"),
            SyncState.Synced => ThemeBrush.Resolve("StatusSuccessBrush", "#6E8F73"),
            SyncState.Error => ThemeBrush.Resolve("StatusErrorBrush", "#B2564A"),
            _ => ThemeBrush.Resolve("StatusNeutralBrush", "#A29C90"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps SyncState to the icon shown in the sync badge. Icon selection only - no
/// color here, so nothing about this converter changes with the restyle.</summary>
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

/// <summary>Turns a "#RRGGBB" string into a brush - used to bind per-item accent colors
/// (Dashboard stat cards) without hardcoding a fixed set of brushes in XAML. Unchanged by
/// the restyle itself - it's a generic pass-through; DashboardViewModel supplying muted
/// palette hex values instead of the old saturated ones is what actually fixes its output
/// (Identity Statement §10 step 7).</summary>
public class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex ? new SolidColorBrush(Color.Parse(hex)) : Brushes.Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Like HexToBrushConverter, but the bound string is a theme resource key
/// ("AccentBrush", "StatusWarningBrush", etc.) rather than a literal hex - resolves via
/// ThemeBrush so the result is correct in both Light and Dark. Used by Dashboard's stat
/// cards (Identity Statement §10 step 7); HexToBrushConverter itself is left as a plain
/// literal-hex pass-through for any future case that genuinely needs one.</summary>
public class ResourceKeyToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? ThemeBrush.Resolve(key, "#7A7367") : ThemeBrush.Resolve("TextSecondaryBrush", "#7A7367");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Colors a bottom-dock icon/label - accent while active, muted secondary text
/// otherwise. Same rule DesktopShellView's Sidebar now follows (Identity Statement §7):
/// one nav coloring language for both platforms.</summary>
public class ActiveToDockBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? ThemeBrush.Resolve("AccentBrush", "#BD5B3D")
            : ThemeBrush.Resolve("TextSecondaryBrush", "#7A7367");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}