using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GrassiBoard.Services.Remote;

internal static class RemoteNetworkInfo
{
    public static IPAddress? GetPreferredPrivateIpv4()
    {
        IEnumerable<NetworkInterface> interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                              network.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              network.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderByDescending(network => network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
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

    private static bool IsPrivate(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
