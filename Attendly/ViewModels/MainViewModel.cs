using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;
using Attendly.Models;
using Attendly.Services;

namespace Attendly.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly ILocalSyncService _syncService;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    [ObservableProperty]
    private SyncState _syncState;

    [ObservableProperty]
    private ThemeMode _themeMode;

    public MainViewModel(AttendanceRepository repository, ILocalSyncService syncService, IThemeService themeService)
    {
        _repository = repository;
        _syncService = syncService;
        _themeService = themeService;

        _syncState = _syncService.CurrentState;
        _themeMode = _themeService.CurrentMode;

        _syncService.StateChanged += state => SyncState = state;
        _themeService.ModeChanged += mode => ThemeMode = mode;

        CurrentPage = new KelasPickerViewModel(_repository, OpenKelasAsync);
    }

    private async Task OpenKelasAsync(TartilLevel level)
    {
        var attendanceViewModel = new AttendanceViewModel(level, _repository, _syncService, GoBackToKelasPicker);
        await attendanceViewModel.InitializeAsync();
        CurrentPage = attendanceViewModel;
    }

    private void GoBackToKelasPicker()
    {
        CurrentPage = new KelasPickerViewModel(_repository, OpenKelasAsync);
    }

    [RelayCommand]
    private void OpenPairing()
    {
        CurrentPage = new MobilePairingViewModel(_repository, GoBackToKelasPicker);
    }

    [RelayCommand]
    private void OpenActivityLog()
    {
        CurrentPage = new ActivityLogViewModel(_repository, GoBackToKelasPicker);
    }

    [RelayCommand]
    private async Task ToggleTheme()
    {
        var next = ThemeMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        await _themeService.SetModeAsync(next);
    }
}