using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Controls;
using Attendly.Data;
using Attendly.Models;
using Attendly.Services;

namespace Attendly.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    private readonly KelasPickerViewModel _homeTab;
    private readonly MobileDashboardViewModel _dashboardTab;
    private readonly ActivityLogViewModel _riwayatTab;
    private readonly MobilePairingViewModel _syncTab;

    public ObservableCollection<BottomNavItemViewModel> NavItems { get; } = new();

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    [ObservableProperty]
    private SyncState _syncState;

    [ObservableProperty]
    private ThemeMode _themeMode;

    [ObservableProperty]
    private bool _isDockVisible = true;

    public MainViewModel(AttendanceRepository repository, ILocalSyncService syncService, IThemeService themeService, IQrScanner qrScanner)
    {
        _themeService = themeService;

        _syncState = syncService.CurrentState;
        _themeMode = _themeService.CurrentMode;

        syncService.StateChanged += state => SyncState = state;
        _themeService.ModeChanged += mode => ThemeMode = mode;

        _homeTab = new KelasPickerViewModel(repository, level => OpenKelasAsync(level, repository, syncService));
        _dashboardTab = new MobileDashboardViewModel(repository, syncService);
        _riwayatTab = new ActivityLogViewModel(repository, GoHome);
        _syncTab = new MobilePairingViewModel(repository, qrScanner, GoHome);

        var tabs = new (string Key, string Label, LucideIconKind Icon, ViewModelBase Page)[]
        {
            ("home", "Home", LucideIconKind.Home, _homeTab),
            ("dashboard", "Dashboard", LucideIconKind.LayoutDashboard, _dashboardTab),
            ("riwayat", "Riwayat", LucideIconKind.Clock, _riwayatTab),
            ("sync", "Sync", LucideIconKind.RefreshCw, _syncTab),
        };

        foreach (var (key, label, icon, page) in tabs)
        {
            var capturedKey = key;
            var capturedPage = page;
            var item = new BottomNavItemViewModel(label, icon, async () =>
            {
                if (capturedPage == _dashboardTab)
                    await _dashboardTab.LoadAsync();

                Navigate(capturedPage, capturedKey);
                IsDockVisible = true;
            })
            { Key = key };
            NavItems.Add(item);
        }

        Navigate(_homeTab, "home");
    }

    private void Navigate(ViewModelBase page, string key)
    {
        CurrentPage = page;
        foreach (var nav in NavItems)
            nav.IsActive = nav.Key == key;
    }

    private void GoHome()
    {
        Navigate(_homeTab, "home");
        IsDockVisible = true;
    }

    private async Task OpenKelasAsync(TartilLevel level, AttendanceRepository repository, ILocalSyncService syncService)
    {
        var attendanceViewModel = new AttendanceViewModel(level, repository, syncService, GoHome);
        await attendanceViewModel.InitializeAsync();

        CurrentPage = attendanceViewModel;
        IsDockVisible = false;
        foreach (var nav in NavItems)
            nav.IsActive = false;
    }

    [RelayCommand]
    private async Task ToggleTheme()
    {
        var next = ThemeMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        await _themeService.SetModeAsync(next);
    }
}