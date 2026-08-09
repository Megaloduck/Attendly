using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;
using Attendly.Models;
using Attendly.Services;

namespace Attendly.ViewModels;

/// <summary>Mobile "Dashboard" tab - a cross-Kelas glance at today's attendance, plus the
/// sync status card with the deliberate "Sinkronkan Sekarang" action. This is the fix for
/// sync previously only firing as a side effect of marking attendance: a teacher who
/// connects once a month can now open this tab and get a real push + roster pull on demand.</summary>
public partial class MobileDashboardViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly ILocalSyncService _syncService;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private int _totalSantri;
    [ObservableProperty] private int _countHadir;
    [ObservableProperty] private int _countAlpha;
    [ObservableProperty] private int _countIzin;
    [ObservableProperty] private int _countSakit;
    [ObservableProperty] private int _countBelum;

    [ObservableProperty] private SyncState _syncState;
    [ObservableProperty] private string _lastSyncedDisplay = "Belum pernah sinkron";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SyncNowLabel))]
    private bool _isSyncing;

    public string SyncNowLabel => IsSyncing ? "Menyinkronkan..." : "Sinkronkan Sekarang";
    public string TodayDisplay => DateTime.Today.ToString("dddd, dd MMMM yyyy");

    public MobileDashboardViewModel(AttendanceRepository repository, ILocalSyncService syncService)
    {
        _repository = repository;
        _syncService = syncService;
        _syncState = _syncService.CurrentState;

        // Named HandleSyncStateChanged (not OnSyncStateChanged) deliberately - MVVM Toolkit's
        // source generator already emits a partial "OnSyncStateChanged(SyncState value)" hook
        // for the SyncState ObservableProperty above, and a same-named method here collides
        // with it (CS0121/CS0111).
        _syncService.StateChanged += HandleSyncStateChanged;

        _ = LoadAsync();
    }

    private void HandleSyncStateChanged(SyncState state)
    {
        SyncState = state;
        UpdateLastSyncedDisplay();

        // A completed sync may have changed the roster or today's records underneath us.
        if (state is SyncState.Synced or SyncState.Error)
            _ = LoadAsync();
    }

    private void UpdateLastSyncedDisplay()
    {
        LastSyncedDisplay = _syncService.LastSyncedAt is { } at
            ? $"Terakhir sinkron: {at:dd MMM, HH:mm}"
            : "Belum pernah sinkron";
    }

    public async Task LoadAsync()
    {
        IsLoading = true;

        var allSantri = await _repository.GetAllSantriAsync();
        TotalSantri = allSantri.Count;

        var today = await _repository.GetAttendanceForDateAsync(DateTime.Today);
        CountHadir = today.Count(r => r.Status == AttendanceStatus.Hadir);
        CountAlpha = today.Count(r => r.Status == AttendanceStatus.Alpha);
        CountIzin = today.Count(r => r.Status == AttendanceStatus.Izin);
        CountSakit = today.Count(r => r.Status == AttendanceStatus.Sakit);

        var markedToday = today.Count(r => r.Status != AttendanceStatus.Libur);
        CountBelum = Math.Max(0, TotalSantri - markedToday);

        UpdateLastSyncedDisplay();
        IsLoading = false;
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private async Task SyncNow()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        try
        {
            await _syncService.SyncNowAsync();
        }
        finally
        {
            IsSyncing = false;
        }
    }
}