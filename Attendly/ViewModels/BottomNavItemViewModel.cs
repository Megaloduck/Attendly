using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Controls;

namespace Attendly.ViewModels;

/// <summary>One tab in the mobile bottom dock - label, icon, active-highlight flag, and
/// the action MainViewModel supplies to switch CurrentPage. Mirrors Desktop's
/// NavItemViewModel but lives in the shared project since the mobile shell isn't
/// platform-specific the way DesktopShellView is.</summary>
public partial class BottomNavItemViewModel : ObservableObject
{
    private readonly Func<Task> _onSelect;

    /// <summary>Stable identifier MainViewModel uses to mark the right tab active - not
    /// shown in the UI, just used for the IsActive comparison.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; }
    public LucideIconKind Icon { get; }

    [ObservableProperty]
    private bool _isActive;

    public BottomNavItemViewModel(string label, LucideIconKind icon, Func<Task> onSelect)
    {
        Label = label;
        Icon = icon;
        _onSelect = onSelect;
    }

    [RelayCommand]
    private Task Select() => _onSelect();
}