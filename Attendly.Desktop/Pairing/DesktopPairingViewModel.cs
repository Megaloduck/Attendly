using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using Avalonia.Media.Imaging;
using Attendly.Data;
using Attendly.Models;
using Attendly.Sync;
using Attendly.ViewModels;
using Attendly.Desktop.Hosting;

namespace Attendly.Desktop.Pairing;

public partial class DesktopPairingViewModel : ViewModelBase
{
    private readonly AttendanceRepository _repository;

    [ObservableProperty]
    private Bitmap? _qrImage;

    [ObservableProperty]
    private string? _qrError;

    [ObservableProperty]
    private string? _pairingCodeText;

    public ObservableCollection<string> AvailableIps { get; } = new();

    [ObservableProperty]
    private string _selectedIp = string.Empty;

    public ObservableCollection<PairedDeviceRowViewModel> PairedDevices { get; } = new();

    public DesktopPairingViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        RefreshIps();
        _ = LoadPairedDevicesAsync();
    }

    [RelayCommand]
    private void RefreshIps()
    {
        var candidates = LocalNetwork.GetCandidateLanIps();

        AvailableIps.Clear();
        foreach (var ip in candidates)
            AvailableIps.Add(ip);

        if (AvailableIps.Count == 0)
            AvailableIps.Add("192.168.1.X");

        if (string.IsNullOrWhiteSpace(SelectedIp) || !AvailableIps.Contains(SelectedIp))
            SelectedIp = AvailableIps[0];
    }

    private async Task LoadPairedDevicesAsync()
    {
        PairedDevices.Clear();
        var devices = await _repository.GetPairedDevicesAsync();
        foreach (var device in devices)
            PairedDevices.Add(new PairedDeviceRowViewModel(device, RemoveAsync));
    }

    [RelayCommand]
    private async Task GenerateCode()
    {
        var ip = string.IsNullOrWhiteSpace(SelectedIp) ? "192.168.1.X" : SelectedIp.Trim();
        var token = Guid.NewGuid().ToString("N");

        await _repository.AddPairedDeviceAsync(token, "Perangkat baru");

        var code = PairingCode.Encode(ip, AttendlyApiHost.Port, token);

        // Set the text code FIRST and unconditionally - this is the guaranteed fallback,
        // so it must not depend on the QR bitmap below succeeding.
        PairingCodeText = code;
        QrImage = null;
        QrError = null;

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(code, QRCodeGenerator.ECCLevel.Q);
            using var pngQr = new PngByteQRCode(qrData);
            var bytes = pngQr.GetGraphic(10);

            using var stream = new System.IO.MemoryStream(bytes);
            QrImage = new Bitmap(stream);
        }
        catch (Exception)
        {
            // QR rendering is a nice-to-have on top of the code text above, not a
            // prerequisite for pairing - a failure here should never hide the fallback.
            QrError = "QR tidak bisa ditampilkan di perangkat ini. Gunakan kode di bawah untuk dimasukkan manual di HP.";
        }

        await LoadPairedDevicesAsync();
    }

    private async Task RemoveAsync(int id)
    {
        await _repository.RemovePairedDeviceAsync(id);
        await LoadPairedDevicesAsync();
    }
}

public partial class PairedDeviceRowViewModel : ObservableObject
{
    private readonly Func<int, Task> _onRemove;
    private readonly int _id;

    public string Label { get; }
    public string PairedAndLastSeen { get; }

    public PairedDeviceRowViewModel(PairedDevice device, Func<int, Task> onRemove)
    {
        _id = device.Id;
        _onRemove = onRemove;
        Label = device.Label;

        var pairedAt = new DateTime(device.PairedAtTicks).ToLocalTime().ToString("dd MMM, HH:mm");
        var lastSeen = device.LastSeenTicks is { } ticks
            ? new DateTime(ticks).ToLocalTime().ToString("dd MMM, HH:mm")
            : "Belum pernah";

        PairedAndLastSeen = $"Dipasangkan: {pairedAt}  •  Terakhir aktif: {lastSeen}";
    }

    [RelayCommand]
    private Task Remove() => _onRemove(_id);
}