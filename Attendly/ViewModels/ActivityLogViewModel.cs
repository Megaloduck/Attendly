using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;

namespace Attendly.ViewModels;

/// <summary>Mobile "Riwayat" screen - this device's own recent attendance changes.</summary>
public partial class ActivityLogViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly Action _goBack;

    [ObservableProperty]
    private bool _isLoading = true;

    public ObservableCollection<ActivityLogRowViewModel> Entries { get; } = new();

    public bool IsEmpty => !IsLoading && Entries.Count == 0;

    public ActivityLogViewModel(AttendanceRepository repository, Action goBack)
    {
        _repository = repository;
        _goBack = goBack;
        _ = LoadAsync();
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;

        var log = await _repository.GetRecentChangeLogAsync(100);

        Entries.Clear();
        foreach (var entry in log)
            Entries.Add(new ActivityLogRowViewModel(entry));

        IsLoading = false;
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    [RelayCommand]
    private void GoBack() => _goBack();
}