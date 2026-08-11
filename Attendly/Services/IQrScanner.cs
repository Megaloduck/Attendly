using System.Threading.Tasks;

namespace Attendly.Services;

/// <summary>Launches the platform's native QR scanner, if one is available on this device.
/// Desktop and any mobile build that never registered a real implementation get NullQrScanner,
/// which just returns null immediately - callers should treat that as "not available here" and
/// fall back to manual entry, never as an error.</summary>
public interface IQrScanner
{
    bool IsSupported { get; }
    Task<string?> ScanAsync();
}

public class NullQrScanner : IQrScanner
{
    public bool IsSupported => false;
    public Task<string?> ScanAsync() => Task.FromResult<string?>(null);
}