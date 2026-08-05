using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Attendly.Data;
using Attendly.Sync;

namespace Attendly.ViewModels;

/// <summary>
/// Mobile side of pairing: manual entry of the ip:port:token code shown on
/// Desktop, plus the teacher's name (the "who" for the activity log - stamped
/// onto every attendance change this device makes from now on).
/// </summary>
public partial class MobilePairingViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;
    private readonly Action _goBack;

    [ObservableProperty]
    private string _teacherName = string.Empty;

    [ObservableProperty]
    private string _pairingCodeInput = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isPaired;

    [ObservableProperty]
    private string? _pairedDesktopLabel;

    public MobilePairingViewModel(AttendanceRepository repository, Action goBack)
    {
        _repository = repository;
        _goBack = goBack;
        _ = LoadCurrentPairingAsync();
    }

    private async Task LoadCurrentPairingAsync()
    {
        var pairing = await _repository.GetPairingAsync();
        IsPaired = pairing?.IsPaired == true;
        if (IsPaired && pairing is not null)
            PairedDesktopLabel = $"{pairing.DesktopIp}:{pairing.DesktopPort}";
        if (!string.IsNullOrWhiteSpace(pairing?.TeacherName))
            TeacherName = pairing!.TeacherName!;
    }

    [RelayCommand]
    private async Task Connect()
    {
        if (string.IsNullOrWhiteSpace(TeacherName))
        {
            StatusMessage = "Masukkan nama Anda terlebih dahulu.";
            return;
        }

        StatusMessage = "Menghubungkan...";

        if (!PairingCode.TryParse(PairingCodeInput, out var ip, out var port, out var token))
        {
            StatusMessage = "Kode tidak valid. Format: ip:port:token";
            return;
        }

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://{ip}:{port}") };
            client.DefaultRequestHeaders.Add("X-Pairing-Token", token);
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync("/api/health");
            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = "Desktop menolak kode ini. Coba buat kode baru di Desktop.";
                return;
            }
        }
        catch
        {
            StatusMessage = "Tidak bisa menghubungi Desktop. Pastikan satu WiFi yang sama.";
            return;
        }

        await _repository.SavePairingAsync(ip, port, token, TeacherName.Trim());
        IsPaired = true;
        PairedDesktopLabel = $"{ip}:{port}";
        StatusMessage = "Berhasil terhubung!";
    }

    [RelayCommand]
    private void Done() => _goBack();
}       