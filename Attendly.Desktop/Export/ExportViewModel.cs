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

/// <summary>Export screen's own Tartil selector - distinct from TartilOption (which
/// MonthlyGridViewModel also uses and always expects a concrete class). Level is null
/// for the "Semua Kelas" entry, meaning every class exported together.</summary>
public sealed record ExportTartilOption(string Label, TartilLevel? Level);

public partial class ExportViewModel : ViewModels.ViewModelBase
{
    private readonly AttendanceExportService _exportService;

    private static readonly string[] MonthNames =
    {
        "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember",
    };

    public IReadOnlyList<ExportTartilOption> AvailableTartil { get; } = BuildTartilOptions();

    public IReadOnlyList<MonthOption> AvailableMonths { get; } =
        Enumerable.Range(1, 12).Select(m => new MonthOption(m, MonthNames[m - 1])).ToList();

    [ObservableProperty]
    private ExportTartilOption _selectedTartil;

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
        _selectedTartil = AvailableTartil[1]; // first real class - "Semua Kelas" sits at [0] and stays opt-in
        _selectedMonth = AvailableMonths[DateTime.Today.Month - 1];
    }

    private static List<ExportTartilOption> BuildTartilOptions()
    {
        var list = new List<ExportTartilOption> { new("Semua Kelas", null) };
        list.AddRange(Enum.GetValues<TartilLevel>().Select(l => new ExportTartilOption(l.ToDisplayString(), l)));
        return list;
    }

    public string SuggestedFileName =>
        $"Absensi_{(SelectedTartil.Level is { } level ? level.ToDisplayString() : "Semua_Kelas").Replace(" ", "_")}_{(int)SelectedYear:D4}{SelectedMonth.Value:D2}";

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