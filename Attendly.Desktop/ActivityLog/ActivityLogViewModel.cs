using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;
using Attendly.ViewModels;

namespace Attendly.Desktop.ActivityLog;

/// <summary>Desktop admin view of the change log - every paired teacher's pushed
/// entries land in Desktop's canonical SQLite via /api/attendance/sync, so this
/// is simply everything that's arrived so far, newest first.</summary>
public partial class ActivityLogViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly List<ActivityLogRowViewModel> _allEntries = new();

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<ActivityLogRowViewModel> Entries { get; } = new();

    public bool IsEmpty => !IsLoading && Entries.Count == 0;

    public ActivityLogViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        _ = LoadAsync();
    }

    [RelayCommand]
    public Task Refresh() => LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;

        var log = await _repository.GetRecentChangeLogAsync(300);

        _allEntries.Clear();
        _allEntries.AddRange(log.Select(e => new ActivityLogRowViewModel(e)));

        ApplyFilter();
        IsLoading = false;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    private void ApplyFilter()
    {
        Entries.Clear();

        var query = string.IsNullOrWhiteSpace(SearchText)
            ? _allEntries.AsEnumerable()
            : _allEntries.Where(e =>
                e.TeacherName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.SantriNamaPanggilan.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in query)
            Entries.Add(entry);

        OnPropertyChanged(nameof(IsEmpty));
    }
}