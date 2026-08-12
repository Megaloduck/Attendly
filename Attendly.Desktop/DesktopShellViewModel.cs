using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Controls;
using Attendly.Data;
using Attendly.Desktop.Dashboard;
using Attendly.Desktop.Export;
using Attendly.Desktop.MonthlyGrid;
using Attendly.Desktop.Pairing;
using Attendly.Desktop.Roster;
using Attendly.Models;
using Attendly.Services;
using Attendly.ViewModels;
using DesktopActivityLogViewModel = Attendly.Desktop.ActivityLog.ActivityLogViewModel;

namespace Attendly.Desktop;

public partial class DesktopShellViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    public ObservableCollection<NavItemViewModel> NavItems { get; } = new();

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    [ObservableProperty]
    private ThemeMode _themeMode;

    public DesktopShellViewModel(AttendanceRepository repository, IThemeService themeService)
    {
        _themeService = themeService;
        _themeMode = themeService.CurrentMode;
        themeService.ModeChanged += mode => ThemeMode = mode;

        var dashboard = new DashboardViewModel(repository);
        var monthlyGrid = new MonthlyGridViewModel(repository);
        var roster = new RosterViewModel(repository);
        var activityLog = new DesktopActivityLogViewModel(repository);
        var pairing = new DesktopPairingViewModel(repository);
        var export = new ExportViewModel(new AttendanceExportService(repository));

        var pages = new (string Label, LucideIconKind Icon, ViewModelBase Page)[]
        {
            ("Dashboard", LucideIconKind.LayoutDashboard, dashboard),
            ("Absensi Bulanan", LucideIconKind.Calendar, monthlyGrid),
            ("Data Santri", LucideIconKind.Users, roster),
            ("Riwayat", LucideIconKind.Clock, activityLog),
            ("Perangkat Guru", LucideIconKind.QrCode, pairing),
            ("Ekspor", LucideIconKind.FileText, export),
        };

        foreach (var (label, icon, page) in pages)
        {
            NavItemViewModel? item = null;
            item = new NavItemViewModel(label, icon, async () =>
            {
                // Dashboard, Riwayat, and Absensi Bulanan can go stale while parked on
                // another tab (roster edits, new syncs), so refresh them every time
                // they're opened.
                if (page == dashboard)
                    await dashboard.LoadAsync();
                else if (page == activityLog)
                    await activityLog.Refresh();
                else if (page == monthlyGrid)
                    await monthlyGrid.LoadAsync();

                Navigate(page, item!);
            });
            NavItems.Add(item);
        }

        Navigate(dashboard, NavItems[0]);
    }

    private void Navigate(ViewModelBase page, NavItemViewModel item)
    {
        CurrentPage = page;
        foreach (var nav in NavItems)
            nav.IsActive = ReferenceEquals(nav, item);
    }

    [RelayCommand]
    private async Task ToggleTheme()
    {
        var next = ThemeMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        await _themeService.SetModeAsync(next);
    }
}