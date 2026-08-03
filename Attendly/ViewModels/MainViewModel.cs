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

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    [ObservableProperty]
    private SyncState _syncState;

    public MainViewModel(AttendanceRepository repository, ILocalSyncService syncService)
    {
        _repository = repository;
        _syncService = syncService;
        _syncState = _syncService.CurrentState;
        _syncService.StateChanged += state => SyncState = state;

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
}