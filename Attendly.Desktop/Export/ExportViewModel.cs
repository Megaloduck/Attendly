using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Attendly.Models;

namespace Attendly.Desktop.Export;

public sealed record TartilOption(TartilLevel Level, string Label);

/// <summary>Value is the 1-12 month number the rest of the app already works with
/// (LoadAsync queries, filenames); Label is what the dropdown actually shows. Shared
/// with MonthlyGridViewModel the same way TartilOption already is.</summary>
public sealed record MonthOption(int Value, string Label);

public partial class ExportViewModel : ViewModels.ViewModelBase
{
    private readonly AttendanceExportService _exportService;

    private static readonly string[] MonthNames =
    {
        "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember",
    };

    public IReadOnlyList<TartilOption> AvailableTartil { get; } =
        Enum.GetValues<TartilLevel>().Select(l => new TartilOption(l, l.ToDisplayString())).ToList();

    public IReadOnlyList<MonthOption> AvailableMonths { get; } =
        Enumerable.Range(1, 12).Select(m => new MonthOption(m, MonthNames[m - 1])).ToList();

    [ObservableProperty]
    private TartilOption _selectedTartil;

    // decimal to match Avalonia's NumericUpDown.Value type directly - avoids any
    // int/decimal binding-conversion ambiguity under compiled bindings.
    [ObservableProperty]
    private decimal _selectedYear = DateTime.Today.Year;

    [ObservableProperty]
    private MonthOption _selectedMonth;

    [ObservableProperty]
    private string? _statusMessage;

    public ExportViewModel(AttendanceExportService exportService)
    {
        _exportService = exportService;
        _selectedTartil = AvailableTartil[0];
        _selectedMonth = AvailableMonths[DateTime.Today.Month - 1];
    }

    public string SuggestedFileName =>
        $"Absensi_{SelectedTartil.Label.Replace(" ", "_")}_{(int)SelectedYear:D4}{SelectedMonth.Value:D2}";

    public async Task ExportCsvAsync(string filePath)
    {
        StatusMessage = "Mengekspor...";
        try
        {
            await _exportService.ExportCsvAsync(SelectedTartil.Level, (int)SelectedYear, SelectedMonth.Value, filePath);
            StatusMessage = $"Berhasil disimpan: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal ekspor: {ex.Message}";
        }
    }

    public async Task ExportXlsxAsync(string filePath)
    {
        StatusMessage = "Mengekspor...";
        try
        {
            await _exportService.ExportXlsxAsync(SelectedTartil.Level, (int)SelectedYear, SelectedMonth.Value, filePath);
            StatusMessage = $"Berhasil disimpan: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal ekspor: {ex.Message}";
        }
    }
}