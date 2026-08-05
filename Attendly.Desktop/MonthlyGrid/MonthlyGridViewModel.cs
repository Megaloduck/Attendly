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

public sealed class MonthlyGridCell
{
    public string Code { get; init; } = string.Empty;
    public string? ColorHex { get; init; }
}

public sealed class MonthlyGridRow
{
    public string NamaPanggilan { get; init; } = string.Empty;
    public IReadOnlyList<MonthlyGridCell> Cells { get; init; } = Array.Empty<MonthlyGridCell>();
}

/// <summary>Plain snapshot of one Tartil/month, rebuilt wholesale on every load rather
/// than kept as observable rows - the code-behind Grid in MonthlyGridView rebuilds
/// from this each time it changes, so there's no benefit to per-cell binding here.</summary>
public sealed class MonthlyGridTableData
{
    public int DaysInMonth { get; init; }
    public IReadOnlyList<MonthlyGridRow> Rows { get; init; } = Array.Empty<MonthlyGridRow>();
}

public partial class MonthlyGridViewModel : ViewModels.ViewModelBase
{
    private readonly AttendanceRepository _repository;

    // Lighter tints of the same hues StatusChipBrushConverter/AttendanceExportService
    // already use for H/A/I/S/O - kept as plain strings (not brushes) since this data
    // feeds a Grid built in code-behind, outside any binding/converter context.
    private static readonly Dictionary<AttendanceStatus, string> StatusColors = new()
    {
        [AttendanceStatus.Hadir] = "#DCFCE7",
        [AttendanceStatus.Alpha] = "#FEE2E2",
        [AttendanceStatus.Izin] = "#FEF3C7",
        [AttendanceStatus.Sakit] = "#DBEAFE",
        [AttendanceStatus.Libur] = "#E5E7EB",
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
                    cells.Add(new MonthlyGridCell { Code = record.Status.ToCode().ToString(), ColorHex = StatusColors[record.Status] });
                }
                else
                {
                    cells.Add(new MonthlyGridCell()); // not yet marked - blank cell
                }
            }

            rows.Add(new MonthlyGridRow { NamaPanggilan = santri.NamaPanggilan, Cells = cells });
        }

        GridData = new MonthlyGridTableData { DaysInMonth = daysInMonth, Rows = rows };

        // Libur is excluded from the percentage base, same convention the export
        // already uses (HADIR/S/I/A) - it's a non-session day, not an attendance outcome.
        var countedTotal = counts.Where(kv => kv.Key != AttendanceStatus.Libur).Sum(kv => kv.Value);
        LegendItems.Clear();
        LegendItems.Add(BuildLegendItem("Hadir", counts[AttendanceStatus.Hadir], countedTotal, StatusColors[AttendanceStatus.Hadir]));
        LegendItems.Add(BuildLegendItem("Sakit", counts[AttendanceStatus.Sakit], countedTotal, StatusColors[AttendanceStatus.Sakit]));
        LegendItems.Add(BuildLegendItem("Izin", counts[AttendanceStatus.Izin], countedTotal, StatusColors[AttendanceStatus.Izin]));
        LegendItems.Add(BuildLegendItem("Alpha", counts[AttendanceStatus.Alpha], countedTotal, StatusColors[AttendanceStatus.Alpha]));
        LegendItems.Add(new LegendItemViewModel($"Libur {counts[AttendanceStatus.Libur]}", StatusColors[AttendanceStatus.Libur]));

        IsLoading = false;
    }

    private static LegendItemViewModel BuildLegendItem(string label, int count, int total, string colorHex)
    {
        var percent = total == 0 ? 0 : count / (double)total * 100;
        return new LegendItemViewModel($"{label} {percent:0}%", colorHex);
    }
}