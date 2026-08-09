using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Attendly.Data;
using Attendly.Desktop.Export;
using Attendly.Models;

namespace Attendly.Desktop.MonthlyGrid;

public sealed record LegendItemViewModel(string DisplayText, string ColorHex);

/// <summary>One day cell in the flat attendance table - a colored status dot (or none,
/// if not yet marked). No letter code is drawn on the grid itself; the dot's color plus
/// its tooltip carry the same information the letter used to.</summary>
public sealed class MonthlyGridCell
{
    public AttendanceStatus? Status { get; init; }
    public string? DotColorHex { get; init; }
    public string? TooltipText { get; init; }
}

public sealed class MonthlyGridRow
{
    public string NamaPanggilan { get; init; } = string.Empty;
    public IReadOnlyList<MonthlyGridCell> Cells { get; init; } = Array.Empty<MonthlyGridCell>();
}

public sealed class MonthlyGridTableData
{
    public int DaysInMonth { get; init; }
    public IReadOnlyList<MonthlyGridRow> Rows { get; init; } = Array.Empty<MonthlyGridRow>();
}

public partial class MonthlyGridViewModel : ViewModels.ViewModelBase
{
    private readonly AttendanceRepository _repository;

    /// <summary>Saturated dot colors. Present/Absent/Leave map to green/red/orange per the
    /// flat data-table brief; Sakit and Libur get their own colors too since the real domain
    /// has five states, not three - Sakit is folded into the "Leave" KPI card below.</summary>
    private static readonly Dictionary<AttendanceStatus, string> DotColors = new()
    {
        [AttendanceStatus.Hadir] = "#16A34A",  // Present
        [AttendanceStatus.Alpha] = "#DC2626",  // Absent
        [AttendanceStatus.Izin] = "#D97706",   // Leave (permission)
        [AttendanceStatus.Sakit] = "#2563EB",  // Sick - grouped into "Leave" for the KPI cards
        [AttendanceStatus.Libur] = "#9CA3AF",  // Non-session day
    };

    public IReadOnlyList<TartilOption> AvailableTartil { get; } =
        Enum.GetValues<TartilLevel>().Select(l => new TartilOption(l, l.ToDisplayString())).ToList();

    [ObservableProperty]
    private TartilOption _selectedTartil;

    [ObservableProperty]
    private decimal _selectedYear = DateTime.Today.Year;

    [ObservableProperty]
    private decimal _selectedMonth = DateTime.Today.Month;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private MonthlyGridTableData? _gridData;

    // The three KPI cards: Present / Absent / Leave, out of all counted (session-day)
    // marks. Libur is excluded from the base - same convention the export already uses.
    [ObservableProperty] private int _presentCount;
    [ObservableProperty] private int _absentCount;
    [ObservableProperty] private int _leaveCount;
    [ObservableProperty] private double _presentPercent;

    public ObservableCollection<LegendItemViewModel> LegendItems { get; } = new();

    public MonthlyGridViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        _selectedTartil = AvailableTartil[0];
        _ = LoadAsync();
    }

    partial void OnSelectedTartilChanged(TartilOption value) => _ = LoadAsync();
    partial void OnSelectedYearChanged(decimal value) => _ = LoadAsync();
    partial void OnSelectedMonthChanged(decimal value) => _ = LoadAsync();

    public async Task LoadAsync()
    {
        IsLoading = true;

        var level = SelectedTartil.Level;
        var year = (int)SelectedYear;
        var month = (int)SelectedMonth;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var roster = await _repository.GetSantriByTartilAsync(level);
        var records = await _repository.GetAttendanceForMonthAsync(level, year, month);
        var bySantri = records.ToLookup(r => r.SantriId);

        var counts = Enum.GetValues<AttendanceStatus>().ToDictionary(s => s, _ => 0);

        var rows = new List<MonthlyGridRow>();
        foreach (var santri in roster)
        {
            var byDay = bySantri[santri.Id].ToDictionary(r => r.Date.Day);
            var cells = new List<MonthlyGridCell>(daysInMonth);

            for (var day = 1; day <= daysInMonth; day++)
            {
                if (byDay.TryGetValue(day, out var record))
                {
                    counts[record.Status]++;
                    cells.Add(new MonthlyGridCell
                    {
                        Status = record.Status,
                        DotColorHex = DotColors[record.Status],
                        TooltipText = $"Tgl {day}: {record.Status.ToDisplayLabel()}",
                    });
                }
                else
                {
                    cells.Add(new MonthlyGridCell { TooltipText = $"Tgl {day}: Belum diabsen" });
                }
            }

            rows.Add(new MonthlyGridRow { NamaPanggilan = santri.NamaPanggilan, Cells = cells });
        }

        GridData = new MonthlyGridTableData { DaysInMonth = daysInMonth, Rows = rows };

        var countedTotal = counts.Where(kv => kv.Key != AttendanceStatus.Libur).Sum(kv => kv.Value);

        PresentCount = counts[AttendanceStatus.Hadir];
        AbsentCount = counts[AttendanceStatus.Alpha];
        LeaveCount = counts[AttendanceStatus.Izin] + counts[AttendanceStatus.Sakit];
        PresentPercent = countedTotal == 0 ? 0 : PresentCount / (double)countedTotal * 100;

        LegendItems.Clear();
        LegendItems.Add(new LegendItemViewModel("Hadir (Present)", DotColors[AttendanceStatus.Hadir]));
        LegendItems.Add(new LegendItemViewModel("Alpha (Absent)", DotColors[AttendanceStatus.Alpha]));
        LegendItems.Add(new LegendItemViewModel("Izin (Leave)", DotColors[AttendanceStatus.Izin]));
        LegendItems.Add(new LegendItemViewModel("Sakit (Sick)", DotColors[AttendanceStatus.Sakit]));
        LegendItems.Add(new LegendItemViewModel("Libur (Non-session)", DotColors[AttendanceStatus.Libur]));

        IsLoading = false;
    }
}   