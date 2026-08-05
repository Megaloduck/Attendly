using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;
using Attendly.Models;

namespace Attendly.ViewModels;

public partial class KelasPickerViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly Func<TartilLevel, Task> _onSelect;

    [ObservableProperty]
    private bool _isLoading = true;

    public string TodayDisplay => DateTime.Today.ToString("dddd, dd MMMM yyyy");

    public ObservableCollection<KelasSummaryViewModel> Kelas { get; } = new();

    public KelasPickerViewModel(AttendanceRepository repository, Func<TartilLevel, Task> onSelect)
    {
        _repository = repository;
        _onSelect = onSelect;
        _ = LoadAsync();
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        Kelas.Clear();

        var today = DateTime.Today;

        foreach (TartilLevel level in Enum.GetValues<TartilLevel>())
        {
            var roster = await _repository.GetSantriByTartilAsync(level);
            var config = await _repository.GetKelasConfigAsync(level) ?? new KelasConfig { TartilLevel = level };
            var isSessionToday = config.IsSessionDay(today.DayOfWeek);
            var markedCount = await _repository.GetMarkedCountForDateAsync(level, today);

            Kelas.Add(new KelasSummaryViewModel(level, roster.Count, markedCount, isSessionToday, _onSelect));
        }

        IsLoading = false;
    }
}