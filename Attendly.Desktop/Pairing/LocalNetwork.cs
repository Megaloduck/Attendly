using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Attendly.Desktop.Pairing;

internal static class LocalNetwork
{
    /// <summary>Best-effort guess at this machine's LAN IPv4 address (not loopback, adapter up).</summary>
    public static string? GetLikelyLanIp()
    {
        var candidate = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                       && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

        return candidate?.Address.ToString();
    }
}