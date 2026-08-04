using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Attendly.Controls;
using Attendly.Data;
using Attendly.Models;
using Attendly.ViewModels;

namespace Attendly.Desktop.Dashboard;

/// <summary>One row in "Santri per Kelas" - now carries its share of the total
/// so the row can show a proportional bar, not just a bare number.</summary>
public sealed record TartilCountViewModel(string DisplayName, int Count, double PercentOfTotal);

/// <summary>One icon-badge stat card in the top row (Total Santri, Laki-laki, etc.).
/// AccentHex colors the icon/number; BadgeBackgroundHex is the light tint behind the icon -
/// both precomputed here rather than derived in XAML, so the palette stays a single source of truth.</summary>
public sealed record DashboardStatCard(string Label, int Value, LucideIconKind Icon, string AccentHex, string BadgeBackgroundHex);

public partial class DashboardViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private int _totalSantri;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AssignedPercentageDisplay))]
    private double _assignedPercentage;

    /// <summary>Share of active santri that already have a Tartil - the Dashboard's donut stat.</summary>
    public string AssignedPercentageDisplay => $"{AssignedPercentage:0}%";

    public ObservableCollection<DashboardStatCard> StatCards { get; } = new();
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
        StatCards.Clear();

        var all = await _repository.GetAllSantriAsync();
        TotalSantri = all.Count;
        var lakiLaki = all.Count(s => s.JenisKelamin == JenisKelamin.LakiLaki);
        var perempuan = all.Count(s => s.JenisKelamin == JenisKelamin.Perempuan);
        var unassigned = all.Count(s => s.TartilLevel is null);

        AssignedPercentage = TotalSantri == 0 ? 0 : (TotalSantri - unassigned) / (double)TotalSantri * 100;

        var devices = await _repository.GetPairedDevicesAsync();

        // Colors match what the app already used for these categories (Perempuan pink,
        // Unassigned amber, etc.) - restyled as badges, not a new palette.
        StatCards.Add(new DashboardStatCard("Total Santri", TotalSantri, LucideIconKind.Users, "#2563EB", "#DBEAFE"));
        StatCards.Add(new DashboardStatCard("Laki-laki", lakiLaki, LucideIconKind.Users, "#16A34A", "#DCFCE7"));
        StatCards.Add(new DashboardStatCard("Perempuan", perempuan, LucideIconKind.Users, "#DB2777", "#FCE7F3"));
        StatCards.Add(new DashboardStatCard("Belum Ditentukan", unassigned, LucideIconKind.CircleAlert, "#D97706", "#FEF3C7"));
        StatCards.Add(new DashboardStatCard("Perangkat Terhubung", devices.Count, LucideIconKind.QrCode, "#7C3AED", "#EDE9FE"));

        foreach (TartilLevel level in Enum.GetValues<TartilLevel>())
        {
            var count = all.Count(s => s.TartilLevel == level);
            var percent = TotalSantri == 0 ? 0 : count / (double)TotalSantri * 100;
            PerTartil.Add(new TartilCountViewModel(level.ToDisplayString(), count, percent));
        }

        IsLoading = false;
    }
}