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

/// <summary>ColorBrushKey is a theme resource key ("StatusSuccessBrush", etc.), resolved by
/// ResourceKeyToBrushConverter in XAML - same fix applied to Dashboard's stat cards
/// (Identity Statement §10 step 7), now applied here for step 8.</summary>
public sealed record LegendItemViewModel(string DisplayText, string ColorBrushKey);

/// <summary>One day cell in the flat attendance table - a colored status dot (or none,
/// if not yet marked). No letter code is drawn on the grid itself; the dot's color plus
/// its tooltip carry the same information the letter used to.</summary>
public sealed class MonthlyGridCell
{
    public AttendanceStatus? Status { get; init; }
    public string? StatusBrushKey { get; init; }
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
    public IReadOnlyList<string> DayNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<MonthlyGridRow> Rows { get; init; } = Array.Empty<MonthlyGridRow>();
}

public partial class MonthlyGridViewModel : ViewModels.ViewModelBase
{
    private readonly AttendanceRepository _repository;

    private static readonly string[] MonthNames =
    {
        "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember",
    };

    /// <summary>3-letter Indonesian day abbreviations for the grid's header row - hardcoded
    /// rather than culture-dependent formatting, same reasoning as MonthNames above.</summary>
    private static readonly Dictionary<DayOfWeek, string> DayAbbreviations = new()
    {
        [DayOfWeek.Sunday] = "Min",
        [DayOfWeek.Monday] = "Sen",
        [DayOfWeek.Tuesday] = "Sel",
        [DayOfWeek.Wednesday] = "Rab",
        [DayOfWeek.Thursday] = "Kam",
        [DayOfWeek.Friday] = "Jum",
        [DayOfWeek.Saturday] = "Sab",
    };

    /// <summary>Resource keys, not literal hex - resolved at render time against whichever
    /// theme dictionary (Light/Dark) is active, via ResourceKeyToBrushConverter (legend) and
    /// MonthlyGridView.axaml.cs's GetBrush() (table cells).</summary>
    private static readonly Dictionary<AttendanceStatus, string> StatusBrushKeys = new()
    {
        [AttendanceStatus.Hadir] = "StatusSuccessBrush",
        [AttendanceStatus.Alpha] = "StatusErrorBrush",
        [AttendanceStatus.Izin] = "StatusWarningBrush",
        [AttendanceStatus.Sakit] = "StatusInfoBrush",
        [AttendanceStatus.Libur] = "StatusNeutralBrush",
    };

    public IReadOnlyList<TartilOption> AvailableTartil { get; } =
        Enum.GetValues<TartilLevel>().Select(l => new TartilOption(l, l.ToDisplayString())).ToList();

    /// <summary>MonthOption is defined alongside TartilOption in ExportViewModel.cs and
    /// shared from there - same cross-reference this file already had for TartilOption.</summary>
    public IReadOnlyList<MonthOption> AvailableMonths { get; } =
        Enumerable.Range(1, 12).Select(m => new MonthOption(m, MonthNames[m - 1])).ToList();

    [ObservableProperty]
    private TartilOption _selectedTartil;

    [ObservableProperty]
    private decimal _selectedYear = DateTime.Today.Year;

    [ObservableProperty]
    private MonthOption _selectedMonth;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private MonthlyGridTableData? _gridData;

    // Four KPI cards: Present / Absent / Izin / Sakit, out of all counted (session-day)
    // marks. Libur is excluded from the base - same convention the export already uses.
    [ObservableProperty] private int _presentCount;
    [ObservableProperty] private int _absentCount;
    [ObservableProperty] private int _izinCount;
    [ObservableProperty] private int _sakitCount;
    [ObservableProperty] private double _presentPercent;

    public ObservableCollection<LegendItemViewModel> LegendItems { get; } = new();

    public MonthlyGridViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        _selectedTartil = AvailableTartil[0];
        _selectedMonth = AvailableMonths[DateTime.Today.Month - 1];
        _ = LoadAsync();
    }

    partial void OnSelectedTartilChanged(TartilOption value) => _ = LoadAsync();
    partial void OnSelectedYearChanged(decimal value) => _ = LoadAsync();
    partial void OnSelectedMonthChanged(MonthOption value) => _ = LoadAsync();

    public async Task LoadAsync()
    {
        IsLoading = true;

        var level = SelectedTartil.Level;
        var year = (int)SelectedYear;
        var month = SelectedMonth.Value;
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
                        StatusBrushKey = StatusBrushKeys[record.Status],
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

        GridData = new MonthlyGridTableData
        {
            DaysInMonth = daysInMonth,
            DayNames = Enumerable.Range(1, daysInMonth)
                .Select(d => DayAbbreviations[new DateTime(year, month, d).DayOfWeek])
                .ToList(),
            Rows = rows,
        };

        var countedTotal = counts.Where(kv => kv.Key != AttendanceStatus.Libur).Sum(kv => kv.Value);

        PresentCount = counts[AttendanceStatus.Hadir];
        AbsentCount = counts[AttendanceStatus.Alpha];
        IzinCount = counts[AttendanceStatus.Izin];
        SakitCount = counts[AttendanceStatus.Sakit];
        PresentPercent = countedTotal == 0 ? 0 : PresentCount / (double)countedTotal * 100;

        LegendItems.Clear();
        LegendItems.Add(new LegendItemViewModel("Hadir (Present)", StatusBrushKeys[AttendanceStatus.Hadir]));
        LegendItems.Add(new LegendItemViewModel("Alpha (Absent)", StatusBrushKeys[AttendanceStatus.Alpha]));
        LegendItems.Add(new LegendItemViewModel("Izin (Leave)", StatusBrushKeys[AttendanceStatus.Izin]));
        LegendItems.Add(new LegendItemViewModel("Sakit (Sick)", StatusBrushKeys[AttendanceStatus.Sakit]));
        LegendItems.Add(new LegendItemViewModel("Libur (Non-session)", StatusBrushKeys[AttendanceStatus.Libur]));

        IsLoading = false;
    }
}