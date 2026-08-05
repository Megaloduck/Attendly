using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Models;

namespace Attendly.ViewModels;

/// <summary>One row in the Kelas picker: a Tartil level plus its active santri count
/// and how many of today's roster have already been marked (session days only).</summary>
public partial class KelasSummaryViewModel : ObservableObject
{
    private readonly Func<TartilLevel, Task> _onSelect;

    public TartilLevel TartilLevel { get; }
    public string DisplayName => TartilLevel.ToDisplayString();
    public int SantriCount { get; }
    public int MarkedCount { get; }
    public bool IsSessionToday { get; }

    // Avalonia's compiled bindings don't always resolve int->double implicitly onto
    // AvaloniaProperties (see ExportViewModel's note on NumericUpDown) - exposing these
    // pre-converted keeps ProgressBar's Value/Maximum binding unambiguous.
    public double MarkedCountValue => MarkedCount;
    public double SantriCountValue => SantriCount;

    public bool IsComplete => IsSessionToday && SantriCount > 0 && MarkedCount >= SantriCount;
    public bool IsInProgress => IsSessionToday && !IsComplete;

    public string ProgressLabel => !IsSessionToday
        ? "Libur hari ini"
        : SantriCount == 0
            ? "Belum ada santri"
            : $"{MarkedCount}/{SantriCount} sudah diabsen";

    public KelasSummaryViewModel(TartilLevel level, int santriCount, int markedCount, bool isSessionToday, Func<TartilLevel, Task> onSelect)
    {
        TartilLevel = level;
        SantriCount = santriCount;
        MarkedCount = markedCount;
        IsSessionToday = isSessionToday;
        _onSelect = onSelect;
    }

    [RelayCommand]
    private Task Select() => _onSelect(TartilLevel);
}