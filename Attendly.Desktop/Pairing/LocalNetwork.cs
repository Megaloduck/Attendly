using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Attendly.Desktop.Pairing;

internal static class LocalNetwork
{
    // Common virtual/tunnel adapter name fragments that are technically "Up" with a
    // real-looking IPv4 address, but aren't the school WiFi/Ethernet adapter a phone
    // could actually reach - picking one of these instead of the real adapter is the
    // most common reason the old single-guess logic produced an unreachable IP.
    private static readonly string[] VirtualAdapterHints =
    {
        "virtualbox", "vmware", "hyper-v", "hyper v", "virtual", "vethernet",
        "loopback", "tunnel", "tap-", "tap ", "npcap", "bluetooth", "vpn",
    };

    /// <summary>Best single guess - kept for callers that just want one address.</summary>
    public static string? GetLikelyLanIp() => GetCandidateLanIps().FirstOrDefault();

    /// <summary>
    /// All plausible LAN IPv4 addresses, best guess first. Ranks adapters with a default
    /// gateway (i.e. actually connected to a router, not just self-assigned) above everything
    /// else, and excludes common virtual/VPN adapters by name so those don't outrank the real
    /// one just because they happened to enumerate first. Returning a list (not one guess)
    /// lets the Pairing screen show a picker when a machine has more than one active adapter.
    /// </summary>
    public static List<string> GetCandidateLanIps()
    {
        var candidates = new List<(string Ip, bool HasGateway, bool PreferredType)>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (LooksVirtual(nic.Name) || LooksVirtual(nic.Description)) continue;

            var props = nic.GetIPProperties();
            var ip = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
            if (ip is null) continue;

            var hasGateway = props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

            var preferredType = nic.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet;

            candidates.Add((ip.ToString(), hasGateway, preferredType));
        }

        return candidates
            .OrderByDescending(c => c.HasGateway)
            .ThenByDescending(c => c.PreferredType)
            .Select(c => c.Ip)
            .Distinct()
            .ToList();
    }

    private static bool LooksVirtual(string text) =>
        VirtualAdapterHints.Any(hint => text.Contains(hint, StringComparison.OrdinalIgnoreCase));
}