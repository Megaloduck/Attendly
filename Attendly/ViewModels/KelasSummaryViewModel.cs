using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Models;

namespace Attendly.ViewModels;

/// <summary>One row in the Kelas picker: a Tartil level plus its active santri count.</summary>
public partial class KelasSummaryViewModel : ObservableObject
{
    private readonly Func<TartilLevel, Task> _onSelect;

    public TartilLevel TartilLevel { get; }
    public string DisplayName => TartilLevel.ToDisplayString();
    public int SantriCount { get; }

    public KelasSummaryViewModel(TartilLevel level, int santriCount, Func<TartilLevel, Task> onSelect)
    {
        TartilLevel = level;
        SantriCount = santriCount;
        _onSelect = onSelect;
    }

    [RelayCommand]
    private Task Select() => _onSelect(TartilLevel);
}