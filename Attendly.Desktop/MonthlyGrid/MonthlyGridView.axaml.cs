using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Attendly.Desktop.MonthlyGrid;

public partial class MonthlyGridView : UserControl
{
    private const double NameColumnWidth = 150;
    private const double DayColumnWidth = 34;

    // Fixed to match the Light palette in App.axaml - the table is built in code-behind
    // (as before), so it doesn't currently re-theme for dark mode. Flag if that matters.
    private static readonly IBrush GridBorderBrush = new SolidColorBrush(Color.Parse("#E2E5EA"));
    private static readonly IBrush HeaderBackground = new SolidColorBrush(Color.Parse("#F7F8FA"));
    private static readonly IBrush StripeBackground = new SolidColorBrush(Color.Parse("#FAFBFC"));

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

        AddHeaderCell(0, "Santri", isNameColumn: true);
        for (var d = 0; d < data.DaysInMonth; d++)
            AddHeaderCell(d + 1, (d + 1).ToString());

        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            var isStripe = r % 2 == 1;

            AddNameCell(r + 1, row.NamaPanggilan, isStripe);

            for (var d = 0; d < row.Cells.Count; d++)
                AddDotCell(r + 1, d + 1, row.Cells[d], isStripe);
        }
    }

    private void AddHeaderCell(int column, string text, bool isNameColumn = false)
    {
        var border = new Border
        {
            BorderBrush = GridBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = HeaderBackground,
            Padding = new Thickness(6, 10),
            MinWidth = isNameColumn ? NameColumnWidth : DayColumnWidth,
        };

        Grid.SetRow(border, 0);
        Grid.SetColumn(border, column);

        border.Child = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = isNameColumn ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            TextAlignment = isNameColumn ? TextAlignment.Left : TextAlignment.Center,
            Opacity = 0.75,
        };

        TableGrid.Children.Add(border);
    }

    private void AddNameCell(int row, string text, bool isStripe)
    {
        var border = new Border
        {
            BorderBrush = GridBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = isStripe ? StripeBackground : Brushes.Transparent,
            Padding = new Thickness(10, 8),
            MinWidth = NameColumnWidth,
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);

        border.Child = new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        TableGrid.Children.Add(border);
    }

    // Each day is a flat table cell with a centered colored dot - green/red/orange
    // (plus blue for Sakit, gray for Libur) instead of a letter code. Blank = not
    // yet marked. Hovering a dot shows the day + status as a tooltip.
    private void AddDotCell(int row, int column, MonthlyGridCell cell, bool isStripe)
    {
        var border = new Border
        {
            BorderBrush = GridBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = isStripe ? StripeBackground : Brushes.Transparent,
            MinWidth = DayColumnWidth,
            MinHeight = 32,
        };

        if (cell.DotColorHex is not null)
        {
            border.Child = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Color.Parse(cell.DotColorHex)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        if (cell.TooltipText is not null)
            ToolTip.SetTip(border, cell.TooltipText);

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);

        TableGrid.Children.Add(border);
    }
}