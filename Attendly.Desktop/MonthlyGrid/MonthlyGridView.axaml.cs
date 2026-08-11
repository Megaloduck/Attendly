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

    private MonthlyGridViewModel? _subscribed;

    public MonthlyGridView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Rebuild whenever the app's actual theme flips - the table's colors now come from
        // Themes/LightTheme.axaml / DarkTheme.axaml via GetBrush() below, same as everything
        // else DynamicResource-bound in XAML, just resolved in code since this table has no
        // per-cell XAML to bind against.
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += OnActualThemeVariantChanged;
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

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => Rebuild();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
    }

    /// <summary>Resolves a themed brush by resource key - same lookup chain a DynamicResource
    /// binding uses, just called from code since this table is built programmatically.
    /// Falls back to the Light hex if the resource isn't found yet (e.g. called before this
    /// control is attached), so nothing ever renders blank.</summary>
    private IBrush GetBrush(string key, string fallbackHex) =>
        this.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(Color.Parse(fallbackHex));

    private void Rebuild()
    {
        TableGrid.Children.Clear();
        TableGrid.RowDefinitions.Clear();
        TableGrid.ColumnDefinitions.Clear();

        var data = ViewModel?.GridData;
        if (data is null) return;

        var gridBorderBrush = GetBrush("BorderSubtleBrush", "#E2E5EA");
        var headerBackground = GetBrush("GridHeaderBackgroundBrush", "#F7F8FA");
        var stripeBackground = GetBrush("GridStripeBackgroundBrush", "#FAFBFC");

        TableGrid.ColumnDefinitions.Add(new ColumnDefinition(NameColumnWidth, GridUnitType.Pixel));
        for (var d = 0; d < data.DaysInMonth; d++)
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition(DayColumnWidth, GridUnitType.Pixel));

        TableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var r = 0; r < data.Rows.Count; r++)
            TableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddHeaderCell(0, "Santri", gridBorderBrush, headerBackground, isNameColumn: true);
        for (var d = 0; d < data.DaysInMonth; d++)
            AddHeaderCell(d + 1, (d + 1).ToString(), gridBorderBrush, headerBackground);

        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            var isStripe = r % 2 == 1;

            AddNameCell(r + 1, row.NamaPanggilan, isStripe, gridBorderBrush, stripeBackground);

            for (var d = 0; d < row.Cells.Count; d++)
                AddDotCell(r + 1, d + 1, row.Cells[d], isStripe, gridBorderBrush, stripeBackground);
        }
    }

    private void AddHeaderCell(int column, string text, IBrush borderBrush, IBrush headerBackground, bool isNameColumn = false)
    {
        var border = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = headerBackground,
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

    private void AddNameCell(int row, string text, bool isStripe, IBrush borderBrush, IBrush stripeBackground)
    {
        var border = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = isStripe ? stripeBackground : Brushes.Transparent,
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
    private void AddDotCell(int row, int column, MonthlyGridCell cell, bool isStripe, IBrush borderBrush, IBrush stripeBackground)
    {
        var border = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = isStripe ? stripeBackground : Brushes.Transparent,
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