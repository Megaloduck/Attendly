using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Attendly.Data;
using Attendly.Models;

namespace Attendly.ViewModels;

public partial class KelasPickerViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly Func<TartilLevel, Task> _onSelect;

    [ObservableProperty]
    private bool _isLoading = true;

    public ObservableCollection<KelasSummaryViewModel> Kelas { get; } = new();

    public KelasPickerViewModel(AttendanceRepository repository, Func<TartilLevel, Task> onSelect)
    {
        _repository = repository;
        _onSelect = onSelect;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        Kelas.Clear();

        foreach (TartilLevel level in Enum.GetValues<TartilLevel>())
        {
            var roster = await _repository.GetSantriByTartilAsync(level);
            Kelas.Add(new KelasSummaryViewModel(level, roster.Count, _onSelect));
        }

        IsLoading = false;
    }
}