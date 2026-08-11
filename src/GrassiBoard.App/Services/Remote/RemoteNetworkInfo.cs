using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GrassiBoard.Services.Remote;

internal static class RemoteNetworkInfo
{
    private static readonly string[] VirtualOrVpnHints =
    [
        "vpn", "wireguard", "wintun", "tap-windows", "tailscale", "zerotier", "cloudflare",
        "proton", "nordlynx", "hamachi", "virtualbox", "vmware", "hyper-v", "wsl", "virtual ethernet"
    ];

    public static IPAddress? GetPreferredPrivateIpv4()
    {
        IEnumerable<NetworkInterface> interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                              network.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              network.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            // Keep VPN/VM adapters as a last-resort fallback, but prefer the physical LAN when both are up.
            .OrderBy(IsLikelyVirtualOrVpn)
            .ThenByDescending(network => network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .ThenByDescending(network => network.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .ThenByDescending(HasIpv4Gateway)
            .ThenByDescending(network => network.Speed);

        foreach (NetworkInterface network in interfaces)
        {
            foreach (UnicastIPAddressInformation address in network.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork && IsPrivate(address.Address))
                    return address.Address;
            }
        }
        return null;
    }

    private static bool IsLikelyVirtualOrVpn(NetworkInterface network)
    {
        string identity = $"{network.Name} {network.Description}".ToLowerInvariant();
        return VirtualOrVpnHints.Any(identity.Contains);
    }

    private static bool HasIpv4Gateway(NetworkInterface network) =>
        network.GetIPProperties().GatewayAddresses.Any(gateway =>
            gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
            !IPAddress.Any.Equals(gateway.Address));

    private static bool IsPrivate(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
