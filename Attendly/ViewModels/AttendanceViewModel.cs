using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly List<SantriAttendanceRowViewModel> _allRows = new();

    public string KelasDisplayName => _level.ToDisplayString();
    public string DateDisplay => Date.ToString("dddd, dd MMMM yyyy");
    public bool IsToday => Date.Date == DateTime.Today;
    public bool CanGoNext => Date.Date < DateTime.Today;

    public ObservableCollection<SantriAttendanceRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private DateTime _date = DateTime.Today;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isSessionDay = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty] private int _countHadir;
    [ObservableProperty] private int _countAlpha;
    [ObservableProperty] private int _countIzin;
    [ObservableProperty] private int _countSakit;
    [ObservableProperty] private int _countBelum;

    public bool HasUnmarked => IsSessionDay && CountBelum > 0;
    public bool IsSearchEmpty => !string.IsNullOrWhiteSpace(SearchText) && Rows.Count == 0 && !IsLoading;

    public AttendanceViewModel(TartilLevel level, AttendanceRepository repository, ILocalSyncService syncService, Action goBack)
    {
        _level = level;
        _repository = repository;
        _syncService = syncService;
        _goBack = goBack;
    }

    public Task InitializeAsync() => LoadForDateAsync();

    private async Task LoadForDateAsync()
    {
        IsLoading = true;

        foreach (var row in _allRows)
            row.PropertyChanged -= OnRowChanged;
        _allRows.Clear();

        var config = await _repository.GetKelasConfigAsync(_level) ?? new KelasConfig { TartilLevel = _level };
        IsSessionDay = config.IsSessionDay(Date.DayOfWeek);

        var roster = await _repository.GetSantriByTartilAsync(_level);
        var records = await _repository.GetOrInitializeDayAsync(_level, Date);
        var recordsBySantriId = records.ToDictionary(r => r.SantriId);

        foreach (var santri in roster)
        {
            recordsBySantriId.TryGetValue(santri.Id, out var record);
            var row = new SantriAttendanceRowViewModel(santri, record?.Status, status => MarkAsync(santri.Id, santri.NamaPanggilan, status));
            row.PropertyChanged += OnRowChanged;
            _allRows.Add(row);
        }

        ApplyFilter();
        RecalculateSummary();
        IsLoading = false;
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SantriAttendanceRowViewModel.Status))
            RecalculateSummary();
    }

    private void RecalculateSummary()
    {
        CountHadir = _allRows.Count(r => r.Status == AttendanceStatus.Hadir);
        CountAlpha = _allRows.Count(r => r.Status == AttendanceStatus.Alpha);
        CountIzin = _allRows.Count(r => r.Status == AttendanceStatus.Izin);
        CountSakit = _allRows.Count(r => r.Status == AttendanceStatus.Sakit);
        CountBelum = _allRows.Count(r => r.Status is null);
        OnPropertyChanged(nameof(HasUnmarked));
    }

    partial void OnDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(DateDisplay));
        OnPropertyChanged(nameof(IsToday));
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsSearchEmpty));

    private void ApplyFilter()
    {
        Rows.Clear();

        var query = string.IsNullOrWhiteSpace(SearchText)
            ? _allRows.AsEnumerable()
            : _allRows.Where(r =>
                r.NamaPanggilan.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Nama.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var row in query)
            Rows.Add(row);

        OnPropertyChanged(nameof(IsSearchEmpty));
    }

    private async Task MarkAsync(int santriId, string namaPanggilan, AttendanceStatus status)
    {
        await _repository.MarkAttendanceAsync(santriId, namaPanggilan, _level, Date, status);
        _syncService.RequestSync();
    }

    [RelayCommand]
    private async Task PreviousDay()
    {
        Date = Date.AddDays(-1);
        await LoadForDateAsync();
    }

    [RelayCommand]
    private async Task NextDay()
    {
        if (!CanGoNext) return;
        Date = Date.AddDays(1);
        await LoadForDateAsync();
    }

    [RelayCommand]
    private async Task GoToToday()
    {
        if (IsToday) return;
        Date = DateTime.Today;
        await LoadForDateAsync();
    }

    [RelayCommand]
    private async Task MarkAllPresent()
    {
        var unmarked = _allRows.Where(r => r.Status is null).ToList();
        if (unmarked.Count == 0) return;

        await _repository.MarkManyAsync(unmarked.Select(r => (r.SantriId, r.NamaPanggilan)), _level, Date, AttendanceStatus.Hadir);

        foreach (var row in unmarked)
            row.Status = AttendanceStatus.Hadir;

        _syncService.RequestSync();
    }

    [RelayCommand]
    private void GoBack() => _goBack();
}