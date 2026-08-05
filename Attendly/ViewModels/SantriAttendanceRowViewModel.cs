using Attendly.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace Attendly.ViewModels;

public partial class SantriAttendanceRowViewModel : ViewModelBase
{
    private readonly Func<AttendanceStatus, Task> _onMark;

    public int SantriId { get; }
    public string Nama { get; }
    public string NamaPanggilan { get; }
    public string Initials { get; }

    [ObservableProperty]
    private AttendanceStatus? _status;

    public string StatusLabel => Status?.ToDisplayLabel() ?? "Belum diabsen";

    public SantriAttendanceRowViewModel(Santri santri, AttendanceStatus? status, Func<AttendanceStatus, Task> onMark)
    {
        SantriId = santri.Id;
        Nama = santri.Nama;
        NamaPanggilan = santri.NamaPanggilan;
        Initials = BuildInitials(santri.NamaPanggilan);
        _status = status;
        _onMark = onMark;
    }

    partial void OnStatusChanged(AttendanceStatus? value) => OnPropertyChanged(nameof(StatusLabel));

    [RelayCommand]
    private async Task Mark(string code)
    {
        var status = AttendanceStatusExtensions.FromCode(code[0]);
        Status = status;
        await _onMark(status);
    }

    private static string BuildInitials(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";

        return name.Length >= 2 ? name[..2].ToUpperInvariant() : name[..1].ToUpperInvariant();
    }
}