using Attendly.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Attendly.ViewModels;

public partial class SantriAttendanceRowViewModel : ViewModelBase
{
    private readonly Func<AttendanceStatus, Task> _onMark;

    public int SantriId { get; }
    public string Nama { get; }
    public string NamaPanggilan { get; }

    [ObservableProperty]
    private AttendanceStatus? _status;

    public SantriAttendanceRowViewModel(Santri santri, AttendanceStatus? status, Func<AttendanceStatus, Task> onMark)
    {
        SantriId = santri.Id;
        Nama = santri.Nama;
        NamaPanggilan = santri.NamaPanggilan;
        _status = status;
        _onMark = onMark;
    }

    [RelayCommand]
    private async Task Mark(string code)
    {
        var status = AttendanceStatusExtensions.FromCode(code[0]);
        Status = status;
        await _onMark(status);
    }
}