using Avalonia.Controls;
using Attendly.Converters;

namespace Attendly.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        // Registered once here so every child view (KelasPickerView, AttendanceView, ...)
        // can reference these as StaticResources without redeclaring them.
        Resources["StatusChipBrushConverter"] = new StatusChipBrushConverter();
        Resources["SyncStateToBrushConverter"] = new SyncStateToBrushConverter();
        Resources["SyncStateToIconKindConverter"] = new SyncStateToIconKindConverter();
    }
}