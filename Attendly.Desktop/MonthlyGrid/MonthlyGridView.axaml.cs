using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Attendly.Desktop.MonthlyGrid;

public partial class MonthlyGridView : UserControl
{
    private const double NameColumnWidth = 150;
    private const double DayColumnWidth = 34;

    private MonthlyGridViewModel? _subscribed;

    public MonthlyGridView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private MonthlyGridViewModel? ViewModel => DataContext as MonthlyGridViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribed is not null)
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribed = ViewModel;

        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;
            Rebuild();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonthlyGridViewModel.GridData))
            Rebuild();
    }

    private void Rebuild()
    {
        TableGrid.Children.Clear();
        TableGrid.RowDefinitions.Clear();
        TableGrid.ColumnDefinitions.Clear();

        var data = ViewModel?.GridData;
        if (data is null) return;

        TableGrid.ColumnDefinitions.Add(new ColumnDefinition(NameColumnWidth, GridUnitType.Pixel));
        for (var d = 0; d < data.DaysInMonth; d++)
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition(DayColumnWidth, GridUnitType.Pixel));

        TableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var r = 0; r < data.Rows.Count; r++)
            TableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddCell(0, 0, "Santri", isHeader: true, isNameColumn: true);
        for (var d = 0; d < data.DaysInMonth; d++)
            AddCell(0, d + 1, (d + 1).ToString(), isHeader: true);

        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            AddCell(r + 1, 0, row.NamaPanggilan, isNameColumn: true);

            for (var d = 0; d < row.Cells.Count; d++)
            {
                var cell = row.Cells[d];
                AddCell(r + 1, d + 1, cell.Code, background: cell.ColorHex);
            }
        }
    }

    private void AddCell(int row, int column, string text, bool isHeader = false, bool isNameColumn = false, string? background = null)
    {
        var resolvedBackground = isHeader ? "#F3F4F6" : background;

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#E5E7EB")),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Background = resolvedBackground is not null ? new SolidColorBrush(Color.Parse(resolvedBackground)) : Brushes.Transparent,
            Padding = new Thickness(6, 4),
            MinWidth = isNameColumn ? NameColumnWidth : DayColumnWidth,
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);

        border.Child = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = isHeader || isNameColumn ? FontWeight.SemiBold : FontWeight.Normal,
            HorizontalAlignment = isNameColumn ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = isNameColumn ? TextAlignment.Left : TextAlignment.Center,
        };

        TableGrid.Children.Add(border);
    }
}