using Attendly.Data;
using Attendly.Models;
using Attendly.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Attendly.Desktop.Roster;

/// <summary>Used both as the list filter (includes "Semua") and, separately, the
/// Tartil-assignment dropdown in the edit form (includes "Belum ditentukan" instead).</summary>
public sealed record TartilFilterOption(string Label, TartilLevel? Level, bool IsUnassigned = false);

public partial class RosterViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly List<SantriRowViewModel> _allRows = new();

    public IReadOnlyList<TartilFilterOption> FilterOptions { get; } = BuildFilterOptions();

    [ObservableProperty]
    private TartilFilterOption _selectedFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>Non-null while the add/edit form should be showing in the right-hand panel.</summary>
    [ObservableProperty]
    private SantriEditViewModel? _editing;

    public ObservableCollection<SantriRowViewModel> Rows { get; } = new();

    /// <summary>The view already bound to this (Roster footer's "Total Santri: {0}"), but
    /// nothing ever defined it - the binding was silently failing. Now reflects whatever
    /// the search box has filtered down to, not just the raw Tartil-filtered count.</summary>
    public int TotalCount => Rows.Count;

    public bool IsSearchEmpty => !string.IsNullOrWhiteSpace(SearchText) && Rows.Count == 0 && !IsLoading;

    public RosterViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        _selectedFilter = FilterOptions[0]; // "Semua"
        _ = LoadAsync();
    }

    partial void OnSelectedFilterChanged(TartilFilterOption value) => _ = LoadAsync();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsSearchEmpty));

    private static List<TartilFilterOption> BuildFilterOptions()
    {
        var list = new List<TartilFilterOption> { new("Semua", null) };
        foreach (TartilLevel level in Enum.GetValues<TartilLevel>())
            list.Add(new TartilFilterOption(level.ToDisplayString(), level));
        list.Add(new TartilFilterOption("Belum ditentukan", null, IsUnassigned: true));
        return list;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;

        List<Santri> santri = SelectedFilter.IsUnassigned
            ? await _repository.GetUnassignedSantriAsync()
            : SelectedFilter.Level is { } level
                ? await _repository.GetSantriByTartilAsync(level)
                : await _repository.GetAllSantriAsync();

        _allRows.Clear();
        foreach (var s in santri)
            _allRows.Add(new SantriRowViewModel(s, EditAsync, DeactivateAsync));

        ApplyFilter();
        IsLoading = false;
    }

    /// <summary>Name search runs client-side over whatever the Tartil dropdown already
    /// loaded - same two-layer pattern AttendanceViewModel and ActivityLogViewModel use,
    /// so switching the Tartil filter doesn't need a new DB round-trip per keystroke.</summary>
    private void ApplyFilter()
    {
        Rows.Clear();

        var query = string.IsNullOrWhiteSpace(SearchText)
            ? _allRows.AsEnumerable()
            : _allRows.Where(r =>
                r.Nama.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.SubtitleDisplay.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var row in query)
            Rows.Add(row);

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(IsSearchEmpty));
    }

    [RelayCommand]
    private void AddNew()
    {
        Editing = new SantriEditViewModel(null, SaveAsync, () => Editing = null);
    }

    private Task EditAsync(Santri santri)
    {
        Editing = new SantriEditViewModel(santri, SaveAsync, () => Editing = null);
        return Task.CompletedTask;
    }

    private async Task SaveAsync(Santri santri)
    {
        await _repository.UpsertSantriAsync(santri);
        Editing = null;
        await LoadAsync();
    }

    private async Task DeactivateAsync(int santriId)
    {
        await _repository.DeactivateSantriAsync(santriId);
        await LoadAsync();
    }
}

/// <summary>One row in the roster list - a santri plus Edit/Deactivate actions.</summary>
public partial class SantriRowViewModel : ObservableObject
{
    private readonly Santri _santri;
    private readonly Func<Santri, Task> _onEdit;
    private readonly Func<int, Task> _onDeactivate;

    public string Nama => _santri.Nama;

    public string SubtitleDisplay =>
        $"{_santri.NamaPanggilan} · {_santri.TartilLevel?.ToDisplayString() ?? "Belum ditentukan"} · " +
        (_santri.JenisKelamin == JenisKelamin.LakiLaki ? "Laki-laki" : "Perempuan");

    public SantriRowViewModel(Santri santri, Func<Santri, Task> onEdit, Func<int, Task> onDeactivate)
    {
        _santri = santri;
        _onEdit = onEdit;
        _onDeactivate = onDeactivate;
    }

    [RelayCommand]
    private Task Edit() => _onEdit(_santri);

    [RelayCommand]
    private Task Deactivate() => _onDeactivate(_santri.Id);
}