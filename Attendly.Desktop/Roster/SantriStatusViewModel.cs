using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;
using Attendly.Models;
using Attendly.ViewModels;

namespace Attendly.Desktop.Roster;

/// <summary>Read-only overview of one santri, shown in Roster's right-hand panel when a
/// row is inspected (the eye icon) - distinct from Editing, which shows SantriEditView
/// instead. RosterViewModel keeps the two mutually exclusive.</summary>
public partial class SantriStatusViewModel : ViewModelBase
{
    private readonly Santri _santri;
    private readonly AttendanceRepository _repository;
    private readonly Action _onEdit;
    private readonly Action _onClose;

    public string Nama => _santri.Nama;
    public string NamaPanggilan => _santri.NamaPanggilan;
    public string Initials { get; }
    public string Nik => _santri.Nik;
    public string GenderDisplay => _santri.JenisKelamin == JenisKelamin.LakiLaki ? "Laki-laki" : "Perempuan";
    public string TartilDisplay => _santri.TartilLevel?.ToDisplayString() ?? "Belum ditentukan";
    public string MasukTpqDisplay => _santri.MasukTpqTahun.ToString();
    public string? TempatLahir => _santri.TempatLahir;
    public string? Alamat => _santri.Alamat;
    public bool HasTempatLahir => !string.IsNullOrWhiteSpace(_santri.TempatLahir);
    public bool HasAlamat => !string.IsNullOrWhiteSpace(_santri.Alamat);
    public string StatusDisplay => _santri.IsActive ? "Aktif" : "Nonaktif";
    public string StatusBrushKey => _santri.IsActive ? "StatusSuccessBrush" : "StatusNeutralBrush";

    public string MonthLabel => DateTime.Today.ToString("MMMM yyyy");

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private int _countHadir;
    [ObservableProperty] private int _countAlpha;
    [ObservableProperty] private int _countIzin;
    [ObservableProperty] private int _countSakit;

    public ObservableCollection<ActivityLogRowViewModel> RecentActivity { get; } = new();
    public bool HasRecentActivity => RecentActivity.Count > 0;
    public bool ShowNoActivityMessage => !IsLoading && RecentActivity.Count == 0;

    public SantriStatusViewModel(Santri santri, AttendanceRepository repository, Action onEdit, Action onClose)
    {
        _santri = santri;
        _repository = repository;
        _onEdit = onEdit;
        _onClose = onClose;
        Initials = BuildInitials(santri.NamaPanggilan);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;

        // Unassigned santri (no TartilLevel) have nothing to query attendance against -
        // the four counts just stay at zero rather than erroring.
        if (_santri.TartilLevel is { } level)
        {
            var today = DateTime.Today;
            var monthRecords = await _repository.GetAttendanceForMonthAsync(level, today.Year, today.Month);
            var mine = monthRecords.Where(r => r.SantriId == _santri.Id).ToList();

            CountHadir = mine.Count(r => r.Status == AttendanceStatus.Hadir);
            CountAlpha = mine.Count(r => r.Status == AttendanceStatus.Alpha);
            CountIzin = mine.Count(r => r.Status == AttendanceStatus.Izin);
            CountSakit = mine.Count(r => r.Status == AttendanceStatus.Sakit);
        }

        // Reuses the same 300-entry recent-activity pull Desktop's own ActivityLogViewModel
        // already does, filtered down to just this santri - no new repository method needed
        // at this school's scale (~75 students).
        var recentLog = await _repository.GetRecentChangeLogAsync(300);
        RecentActivity.Clear();
        foreach (var entry in recentLog.Where(e => e.SantriId == _santri.Id).Take(5))
            RecentActivity.Add(new ActivityLogRowViewModel(entry));

        OnPropertyChanged(nameof(HasRecentActivity));
        IsLoading = false;
        OnPropertyChanged(nameof(ShowNoActivityMessage));
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

    [RelayCommand]
    private void Edit() => _onEdit();

    [RelayCommand]
    private void Close() => _onClose();
}