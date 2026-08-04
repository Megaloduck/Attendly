using Attendly.Models;
using Attendly.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Attendly.Desktop.Roster;

public sealed record GenderOption(string Label, JenisKelamin Value);

/// <summary>Add/edit form for a single santri. Shown in RosterView's right-hand panel.</summary>
public partial class SantriEditViewModel : ViewModelBase
{
    private readonly int _id; // 0 = new santri
    private readonly Func<Santri, Task> _onSave;
    private readonly Action _onCancel;

    public bool IsNew => _id == 0;
    public string HeaderText => IsNew ? "Tambah Santri" : "Edit Santri";

    [ObservableProperty] private string _nik = string.Empty;
    [ObservableProperty] private string _nama = string.Empty;
    [ObservableProperty] private string _namaPanggilan = string.Empty;
    [ObservableProperty] private string? _tempatLahir;
    [ObservableProperty] private string? _alamat;
    [ObservableProperty] private decimal _masukTpqTahun = DateTime.Today.Year;
    [ObservableProperty] private string? _errorMessage;

    public IReadOnlyList<GenderOption> GenderChoices { get; } = new List<GenderOption>
    {
        new("Laki-laki", JenisKelamin.LakiLaki),
        new("Perempuan", JenisKelamin.Perempuan),
    };

    [ObservableProperty] private GenderOption _selectedGender;

    public IReadOnlyList<TartilFilterOption> TartilChoices { get; } = BuildTartilChoices();

    [ObservableProperty] private TartilFilterOption _selectedTartil;

    public SantriEditViewModel(Santri? existing, Func<Santri, Task> onSave, Action onCancel)
    {
        _onSave = onSave;
        _onCancel = onCancel;

        if (existing is not null)
        {
            _id = existing.Id;
            _nik = existing.Nik;
            _nama = existing.Nama;
            _namaPanggilan = existing.NamaPanggilan;
            _tempatLahir = existing.TempatLahir;
            _alamat = existing.Alamat;
            _masukTpqTahun = existing.MasukTpqTahun;
            _selectedGender = GenderChoices.First(g => g.Value == existing.JenisKelamin);
            _selectedTartil = TartilChoices.FirstOrDefault(t => t.Level == existing.TartilLevel) ?? TartilChoices[0];
        }
        else
        {
            _selectedGender = GenderChoices[0];
            _selectedTartil = TartilChoices[0]; // "Belum ditentukan"
        }
    }

    private static List<TartilFilterOption> BuildTartilChoices()
    {
        var list = new List<TartilFilterOption> { new("Belum ditentukan", null, IsUnassigned: true) };
        foreach (TartilLevel level in Enum.GetValues<TartilLevel>())
            list.Add(new TartilFilterOption(level.ToDisplayString(), level));
        return list;
    }

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Nik) || string.IsNullOrWhiteSpace(Nama) || string.IsNullOrWhiteSpace(NamaPanggilan))
        {
            ErrorMessage = "NIK, Nama, dan Nama Panggilan wajib diisi.";
            return;
        }

        var santri = new Santri
        {
            Id = _id,
            Nik = Nik.Trim(),
            Nama = Nama.Trim(),
            NamaPanggilan = NamaPanggilan.Trim(),
            JenisKelamin = SelectedGender.Value,
            TempatLahir = string.IsNullOrWhiteSpace(TempatLahir) ? null : TempatLahir.Trim(),
            Alamat = string.IsNullOrWhiteSpace(Alamat) ? null : Alamat.Trim(),
            MasukTpqTahun = (int)MasukTpqTahun,
            TartilLevel = SelectedTartil.Level,
            IsActive = true,
        };

        try
        {
            await _onSave(santri);
        }
        catch (Exception ex)
        {
            // NIK has a UNIQUE index (Phase 1) - this is the most likely failure here.
            ErrorMessage = $"Gagal menyimpan (NIK mungkin sudah dipakai): {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel() => _onCancel();
}