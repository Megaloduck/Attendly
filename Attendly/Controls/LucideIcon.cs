using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Attendly.Controls;

/// <summary>
/// Icons used across Attendly, sourced from the real Lucide SVGs
/// (https://lucide.dev, ISC licensed) at github.com/lucide-icons/lucide.
/// Each icon's circles/lines/rects were converted to equivalent path arcs
/// so the whole icon is a single Avalonia Geometry - no icon font, no
/// NuGet package, just this one control plus the data below.
/// Default stroke/stretch styling lives in Styles/AttendlyStyles.axaml.
/// </summary>
public enum LucideIconKind
{
    Check,
    X,
    Thermometer,
    FileText,
    CalendarOff,
    Wifi,
    WifiOff,
    RefreshCw,
    CircleCheck,
    CircleAlert,
    Users,
    Settings,
    ChevronRight,
    ChevronLeft,
    Plus,
    Pencil,
    Trash2,
    QrCode,
    ScanLine,
}

public class LucideIcon : Path
{
    public static readonly StyledProperty<LucideIconKind> KindProperty =
        AvaloniaProperty.Register<LucideIcon, LucideIconKind>(nameof(Kind));

    public LucideIconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    static LucideIcon()
    {
        KindProperty.Changed.AddClassHandler<LucideIcon>((icon, _) => icon.UpdateGeometry());
    }

    public LucideIcon()
    {
        UpdateGeometry();
    }

    private void UpdateGeometry()
    {
        Data = Geometry.Parse(LucideIconData.PathData[Kind]);
    }
}

/// <summary>
/// Combined path data per icon, in Lucide's native 24x24 viewBox space.
/// Generated directly from the real SVG sources - not hand-typed.
/// </summary>
internal static class LucideIconData
{
    public static readonly IReadOnlyDictionary<LucideIconKind, string> PathData = new Dictionary<LucideIconKind, string>
    {
        [LucideIconKind.Check] = "M20 6 9 17l-5-5",
        [LucideIconKind.X] = "M18 6 6 18 m6 6 12 12",
        [LucideIconKind.Thermometer] = "M14 4v10.54a4 4 0 1 1-4 0V4a2 2 0 0 1 4 0Z",
        [LucideIconKind.FileText] = "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M10 9H8 M16 13H8 M16 17H8",
        [LucideIconKind.CalendarOff] = "M16 2v3 m2 2 20 20 M21 9h-5.5 M3 9h6 M3.586 3.586A2 2 0 003 5v14a2 2 0 002 2h14a2 2 0 001.414-.586 M8.656 3H19a2 2 0 012 2v10.344",
        [LucideIconKind.Wifi] = "M12 20h.01 M2 8.82a15 15 0 0 1 20 0 M5 12.859a10 10 0 0 1 14 0 M8.5 16.429a5 5 0 0 1 7 0",
        [LucideIconKind.WifiOff] = "M12 20h.01 M8.5 16.429a5 5 0 0 1 7 0 M5 12.859a10 10 0 0 1 5.17-2.69 M19 12.859a10 10 0 0 0-2.007-1.523 M2 8.82a15 15 0 0 1 4.177-2.643 M22 8.82a15 15 0 0 0-11.288-3.764 m2 2 20 20",
        [LucideIconKind.RefreshCw] = "M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8 M21 3v5h-5 M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16 M8 16H3v5",
        [LucideIconKind.CircleCheck] = "M2.0,12.0 a10.0,10.0 0 1,0 20.0,0 a10.0,10.0 0 1,0 -20.0,0 m9 12 2 2 4-4",
        [LucideIconKind.CircleAlert] = "M2.0,12.0 a10.0,10.0 0 1,0 20.0,0 a10.0,10.0 0 1,0 -20.0,0 M12,8 L12,12 M12,16 L12.01,16",
        [LucideIconKind.Users] = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M16 3.128a4 4 0 0 1 0 7.744 M22 21v-2a4 4 0 0 0-3-3.87 M5.0,7.0 a4.0,4.0 0 1,0 8.0,0 a4.0,4.0 0 1,0 -8.0,0",
        [LucideIconKind.Settings] = "M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915 M9.0,12.0 a3.0,3.0 0 1,0 6.0,0 a3.0,3.0 0 1,0 -6.0,0",
        [LucideIconKind.ChevronRight] = "m9 18 6-6-6-6",
        [LucideIconKind.ChevronLeft] = "m15 18-6-6 6-6",
        [LucideIconKind.Plus] = "M5 12h14 M12 5v14",
        [LucideIconKind.Pencil] = "M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z m15 5 4 4",
        [LucideIconKind.Trash2] = "M10 11v6 M14 11v6 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M3 6h18 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2",
        [LucideIconKind.QrCode] = "M4,3 H7 A1,1 0 0 1 8,4 V7 A1,1 0 0 1 7,8 H4 A1,1 0 0 1 3,7 V4 A1,1 0 0 1 4,3 Z M17,3 H20 A1,1 0 0 1 21,4 V7 A1,1 0 0 1 20,8 H17 A1,1 0 0 1 16,7 V4 A1,1 0 0 1 17,3 Z M4,16 H7 A1,1 0 0 1 8,17 V20 A1,1 0 0 1 7,21 H4 A1,1 0 0 1 3,20 V17 A1,1 0 0 1 4,16 Z M21 16h-3a2 2 0 0 0-2 2v3 M21 21v.01 M12 7v3a2 2 0 0 1-2 2H7 M3 12h.01 M12 3h.01 M12 16v.01 M16 12h1 M21 12v.01 M12 21v-1",
        [LucideIconKind.ScanLine] = "M3 7V5a2 2 0 0 1 2-2h2 M17 3h2a2 2 0 0 1 2 2v2 M21 17v2a2 2 0 0 1-2 2h-2 M7 21H5a2 2 0 0 1-2-2v-2 M7 12h10",
    };
}
