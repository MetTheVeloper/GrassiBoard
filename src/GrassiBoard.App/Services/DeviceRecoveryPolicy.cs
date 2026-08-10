using GrassiBoard.Models;
using GrassiBoard.Shared;

namespace GrassiBoard.Services;

internal static class DeviceRecoveryPolicy
{
    public static AudioDevice? SelectNextInput(IEnumerable<AudioDevice> devices, string failedDeviceId)
    {
        AudioDevice[] available = devices
            .Where(device => !VirtualCableMatcher.IsExternalVirtualEndpoint(device.ToDescriptor()))
            .ToArray();
        return available.FirstOrDefault(device =>
                device.Id != failedDeviceId && device.IsDefault) ??
            available.FirstOrDefault(device => device.Id != failedDeviceId) ??
            available.FirstOrDefault(device => device.Id == failedDeviceId) ??
            available.FirstOrDefault();
    }
}
