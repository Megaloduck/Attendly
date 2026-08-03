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
    private string? _pairingCodeText;

    public ObservableCollection<PairedDeviceRowViewModel> PairedDevices { get; } = new();

    public DesktopPairingViewModel(AttendanceRepository repository)
    {
        _repository = repository;
        _ = LoadPairedDevicesAsync();
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
        var ip = LocalNetwork.GetLikelyLanIp() ?? "192.168.1.X";
        var token = Guid.NewGuid().ToString("N");

        await _repository.AddPairedDeviceAsync(token, "Perangkat baru");

        var code = PairingCode.Encode(ip, AttendlyApiHost.Port, token);
        PairingCodeText = code;

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(code, QRCodeGenerator.ECCLevel.Q);
        using var pngQr = new PngByteQRCode(qrData);
        var bytes = pngQr.GetGraphic(10);

        using var stream = new System.IO.MemoryStream(bytes);
        QrImage = new Bitmap(stream);

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