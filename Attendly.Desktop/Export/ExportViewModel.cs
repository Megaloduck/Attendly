using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Attendly.Models;

namespace Attendly.Desktop.Export;

public sealed record TartilOption(TartilLevel Level, string Label);

public partial class ExportViewModel : ViewModels.ViewModelBase
{
    private readonly AttendanceExportService _exportService;

    public IReadOnlyList<TartilOption> AvailableTartil { get; } =
        Enum.GetValues<TartilLevel>().Select(l => new TartilOption(l, l.ToDisplayString())).ToList();

    [ObservableProperty]
    private TartilOption _selectedTartil;

    // decimal to match Avalonia's NumericUpDown.Value type directly - avoids any
    // int/decimal binding-conversion ambiguity under compiled bindings.
    [ObservableProperty]
    private decimal _selectedYear = DateTime.Today.Year;

    [ObservableProperty]
    private decimal _selectedMonth = DateTime.Today.Month;

    [ObservableProperty]
    private string? _statusMessage;

    public ExportViewModel(AttendanceExportService exportService)
    {
        _exportService = exportService;
        _selectedTartil = AvailableTartil[0];
    }

    public string SuggestedFileName =>
        $"Absensi_{SelectedTartil.Label.Replace(" ", "_")}_{(int)SelectedYear:D4}{(int)SelectedMonth:D2}";

    public async Task ExportCsvAsync(string filePath)
    {
        StatusMessage = "Mengekspor...";
        try
        {
            await _exportService.ExportCsvAsync(SelectedTartil.Level, (int)SelectedYear, (int)SelectedMonth, filePath);
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
            await _exportService.ExportXlsxAsync(SelectedTartil.Level, (int)SelectedYear, (int)SelectedMonth, filePath);
            StatusMessage = $"Berhasil disimpan: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal ekspor: {ex.Message}";
        }
    }
}