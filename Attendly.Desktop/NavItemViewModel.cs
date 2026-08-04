using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Controls;

namespace Attendly.Desktop;

/// <summary>One entry in the sidebar - label, icon, an active/highlight flag,
/// and the action that navigates to it (which DesktopShellViewModel supplies).</summary>
public partial class NavItemViewModel : ObservableObject
{
    private readonly Func<Task> _onSelect;

    public string Label { get; }
    public LucideIconKind Icon { get; }

    [ObservableProperty]
    private bool _isActive;

    public NavItemViewModel(string label, LucideIconKind icon, Func<Task> onSelect)
    {
        Label = label;
        Icon = icon;
        _onSelect = onSelect;
    }

    [RelayCommand]
    private Task Select() => _onSelect();
}