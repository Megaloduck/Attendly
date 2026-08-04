using Attendly.Data;
using Attendly.Models;
using Attendly.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Bibliography;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Attendly.Desktop.Roster;

/// <summary>Used both as the list filter (includes "Semua") and, separately, the
/// Tartil-assignment dropdown in the edit form (includes "Belum ditentukan" instead).</summary>
public sealed record TartilFilterOption(string Label, TartilLevel? Level, bool IsUnassigned = false);

public partial class RosterViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;

    public IReadOnlyList<TartilFilterOption> FilterOptions { get; } = BuildFilterOptions();

    [ObservableProperty]
    private TartilFilterOption _selectedFilter;

    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>Non-null while the add/edit form should be showing in the right-hand panel.</summary>
    [ObservableProperty]
    private SantriEditViewModel? _editing;

    public ObservableCollection<SantriRowViewModel> Rows { get; } = new();

    public RosterViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        _selectedFilter = FilterOptions[0]; // "Semua"
        _ = LoadAsync();
    }

    partial void OnSelectedFilterChanged(TartilFilterOption value) => _ = LoadAsync();

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
        Rows.Clear();

        List<Santri> santri = SelectedFilter.IsUnassigned
            ? await _repository.GetUnassignedSantriAsync()
            : SelectedFilter.Level is { } level
                ? await _repository.GetSantriByTartilAsync(level)
                : await _repository.GetAllSantriAsync();

        foreach (var s in santri)
            Rows.Add(new SantriRowViewModel(s, EditAsync, DeactivateAsync));

        IsLoading = false;
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