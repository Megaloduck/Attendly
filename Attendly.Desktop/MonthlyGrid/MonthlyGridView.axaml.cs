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

    /// <summary>Resolves a themed brush by resource key. Uses Application.Current directly
    /// against its ActualThemeVariant - the same mechanism Converters.cs's ThemeBrush.Resolve
    /// already uses successfully for the status dots, sync badge, etc. - rather than
    /// this.TryFindResource(), which depends on this control's own theme context resolving
    /// correctly through the visual tree at the exact moment Rebuild() runs. That dependency
    /// was the bug: it could silently fail and fall through to the light-mode fallback hex
    /// even while the app was actually in Dark mode, which is why the name column was
    /// unreadable (near-black text on a dark background) regardless of theme.</summary>
    private IBrush GetBrush(string key, string fallbackHex)
    {
        if (Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var value) &&
            value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private void Rebuild()
    {
        TableGrid.Children.Clear();
        TableGrid.RowDefinitions.Clear();
        TableGrid.ColumnDefinitions.Clear();

        var data = ViewModel?.GridData;
        if (data is null) return;

        var gridBorderBrush = GetBrush("BorderSubtleBrush", "#E4DDD0");
        var headerBackground = GetBrush("GridHeaderBackgroundBrush", "#EFE9DF");
        var stripeBackground = GetBrush("GridStripeBackgroundBrush", "#F7F3EC");

        TableGrid.ColumnDefinitions.Add(new ColumnDefinition(NameColumnWidth, GridUnitType.Pixel));
        for (var d = 0; d < data.DaysInMonth; d++)
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition(DayColumnWidth, GridUnitType.Pixel));

        TableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var r = 0; r < data.Rows.Count; r++)
            TableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddNameHeaderCell(gridBorderBrush, headerBackground);
        for (var d = 0; d < data.DaysInMonth; d++)
        {
            var dayName = d < data.DayNames.Count ? data.DayNames[d] : null;
            AddDayHeaderCell(d + 1, (d + 1).ToString(), dayName, gridBorderBrush, headerBackground);
        }

        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            var isStripe = r % 2 == 1;

            AddNameCell(r + 1, row.NamaPanggilan, isStripe, gridBorderBrush, stripeBackground);

            for (var d = 0; d < row.Cells.Count; d++)
                AddDotCell(r + 1, d + 1, row.Cells[d], isStripe, gridBorderBrush, stripeBackground);
        }
    }

    /// <summary>The "Santri" corner header - left-aligned, single line, distinct from the
    /// day columns which now carry two lines (day name + day number).</summary>
    private void AddNameHeaderCell(IBrush borderBrush, IBrush headerBackground)
    {
        var border = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = headerBackground,
            Padding = new Thickness(10, 10),
            MinWidth = NameColumnWidth,
        };

        Grid.SetRow(border, 0);
        Grid.SetColumn(border, 0);

        border.Child = new TextBlock
        {
            Text = "Santri",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = GetBrush("TextSecondaryBrush", "#7A7367"),
        };

        TableGrid.Children.Add(border);
    }

    /// <summary>One day column's header - day-of-week abbreviation (small, muted) stacked
    /// above the day number (existing size/weight), so a glance at the header shows which
    /// columns are session days without needing to count from the 1st.</summary>
    private void AddDayHeaderCell(int column, string dayNumber, string? dayName, IBrush borderBrush, IBrush headerBackground)
    {
        var border = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = headerBackground,
            Padding = new Thickness(4, 8),
            MinWidth = DayColumnWidth,
        };

        Grid.SetRow(border, 0);
        Grid.SetColumn(border, column);

        var stack = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center };

        if (!string.IsNullOrEmpty(dayName))
        {
            stack.Children.Add(new TextBlock
            {
                Text = dayName,
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = GetBrush("TextSecondaryBrush", "#7A7367"),
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = dayNumber,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Foreground = GetBrush("TextSecondaryBrush", "#7A7367"),
        });

        border.Child = stack;
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
            Foreground = GetBrush("TextPrimaryBrush", "#2A2724"),
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

        if (cell.StatusBrushKey is not null)
        {
            border.Child = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = GetBrush(cell.StatusBrushKey, "#A29C90"),
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