using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Attendly.Data;
using Attendly.Models;
using Attendly.ViewModels;

namespace Attendly.Desktop.Dashboard;

public sealed record TartilCountViewModel(string DisplayName, int Count);

public partial class DashboardViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private int _totalSantri;
    [ObservableProperty] private int _totalLakiLaki;
    [ObservableProperty] private int _totalPerempuan;
    [ObservableProperty] private int _unassignedCount;
    [ObservableProperty] private int _pairedDeviceCount;

    public ObservableCollection<TartilCountViewModel> PerTartil { get; } = new();

    public DashboardViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        PerTartil.Clear();

        var all = await _repository.GetAllSantriAsync();
        TotalSantri = all.Count;
        TotalLakiLaki = all.Count(s => s.JenisKelamin == JenisKelamin.LakiLaki);
        TotalPerempuan = all.Count(s => s.JenisKelamin == JenisKelamin.Perempuan);
        UnassignedCount = all.Count(s => s.TartilLevel is null);

        foreach (TartilLevel level in Enum.GetValues<TartilLevel>())
            PerTartil.Add(new TartilCountViewModel(level.ToDisplayString(), all.Count(s => s.TartilLevel == level)));

        var devices = await _repository.GetPairedDevicesAsync();
        PairedDeviceCount = devices.Count;

        IsLoading = false;
    }
}