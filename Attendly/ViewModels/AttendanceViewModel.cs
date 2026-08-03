using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;
using Attendly.Models;
using Attendly.Services;

namespace Attendly.ViewModels;

public partial class AttendanceViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly ILocalSyncService _syncService;
    private readonly TartilLevel _level;
    private readonly Action _goBack;
    private readonly DateTime _date = DateTime.Today;

    public string KelasDisplayName => _level.ToDisplayString();
    public string DateDisplay => _date.ToString("dddd, dd MMMM yyyy");

    public ObservableCollection<SantriAttendanceRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private bool _isLoading = true;

    public AttendanceViewModel(TartilLevel level, AttendanceRepository repository, ILocalSyncService syncService, Action goBack)
    {
        _level = level;
        _repository = repository;
        _syncService = syncService;
        _goBack = goBack;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;

        var roster = await _repository.GetSantriByTartilAsync(_level);
        var records = await _repository.GetOrInitializeDayAsync(_level, _date);
        var recordsBySantriId = records.ToDictionary(r => r.SantriId);

        Rows.Clear();
        foreach (var santri in roster)
        {
            recordsBySantriId.TryGetValue(santri.Id, out var record);
            AttendanceStatus? initialStatus = record?.Status;
            Rows.Add(new SantriAttendanceRowViewModel(santri, initialStatus, status => MarkAsync(santri.Id, status)));
        }

        IsLoading = false;
    }

    private async Task MarkAsync(int santriId, AttendanceStatus status)
    {
        await _repository.MarkAttendanceAsync(santriId, _level, _date, status);
        _syncService.RequestSync();
    }

    [RelayCommand]
    private void GoBack() => _goBack();
}