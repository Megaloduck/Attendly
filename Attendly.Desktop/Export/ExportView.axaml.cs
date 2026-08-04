using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Attendly.Desktop.Export;

public partial class ExportView : UserControl
{
    public ExportView()
    {
        InitializeComponent();
    }

    private ExportViewModel? ViewModel => DataContext as ExportViewModel;

    // File dialogs need a TopLevel reference, which ViewModels shouldn't hold -
    // that's why picking happens here in code-behind rather than as a Command.
    private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Simpan Ekspor CSV",
            SuggestedFileName = ViewModel.SuggestedFileName,
            DefaultExtension = "csv",
            FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } },
        });

        if (file is not null)
            await ViewModel.ExportCsvAsync(file.Path.LocalPath);
    }

    private async void OnExportXlsxClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Simpan Ekspor Excel",
            SuggestedFileName = ViewModel.SuggestedFileName,
            DefaultExtension = "xlsx",
            FileTypeChoices = new[] { new FilePickerFileType("Excel Workbook") { Patterns = new[] { "*.xlsx" } } },
        });

        if (file is not null)
            await ViewModel.ExportXlsxAsync(file.Path.LocalPath);
    }
}