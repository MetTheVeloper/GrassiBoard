[CmdletBinding()]
param(
    [string] $EndpointName = 'GrassiBoard Virtual Microphone'
)

$ErrorActionPreference = 'Stop'

if (-not ('GrassiBoard.Diagnostics.WasapiProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace GrassiBoard.Diagnostics
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    internal enum EDataFlow { Render, Capture, All }
    internal enum ERole { Console, Multimedia, Communications }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice endpoint);
        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, uint classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig]
        int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig]
        int GetBufferSize(out uint frames);
        [PreserveSig]
        int GetStreamLatency(out long latency);
        [PreserveSig]
        int GetCurrentPadding(out uint frames);
        [PreserveSig]
        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig]
        int GetMixFormat(out IntPtr format);
        [PreserveSig]
        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig]
        int Start();
        [PreserveSig]
        int Stop();
        [PreserveSig]
        int Reset();
        [PreserveSig]
        int SetEventHandle(IntPtr eventHandle);
        [PreserveSig]
        int GetService(ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct WaveFormatExtensible
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSecond;
        public uint AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
        public ushort ValidBitsPerSample;
        public uint ChannelMask;
        public Guid SubFormat;
    }

    public sealed class WasapiProbeResult
    {
        public string EndpointId;
        public uint EndpointState;
        public int ActivateResult;
        public int EndpointVolumeActivateResult;
        public int MeterActivateResult;
        public int GetMixFormatResult;
        public string MixFormat;
        public int ExactSharedSupportResult;
        public int ExactExclusiveSupportResult;
        public string ClosestFormat;
        public int InitializeSharedResult;
        public int InitializeExclusiveResult;
    }

    public static class WasapiProbe
    {
        private const uint ClsctxAll = 23;
        private static readonly Guid AudioClientId = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
        private static readonly Guid PcmSubFormat = new Guid("00000001-0000-0010-8000-00AA00389B71");

        private static string DescribeFormat(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            WaveFormatExtensible format = (WaveFormatExtensible)Marshal.PtrToStructure(pointer, typeof(WaveFormatExtensible));
            return string.Format("tag=0x{0:X4} channels={1} rate={2} avgBytes={3} align={4} bits={5} cbSize={6} validBits={7} mask=0x{8:X8} sub={9}",
                format.FormatTag, format.Channels, format.SamplesPerSecond, format.AverageBytesPerSecond,
                format.BlockAlign, format.BitsPerSample, format.ExtraSize, format.ValidBitsPerSample,
                format.ChannelMask, format.SubFormat);
        }

        public static WasapiProbeResult Run(string endpointId)
        {
            IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice device;
            int getDevice = enumerator.GetDevice(endpointId, out device);
            if (getDevice < 0) Marshal.ThrowExceptionForHR(getDevice);

            WasapiProbeResult result = new WasapiProbeResult();
            result.EndpointId = endpointId;
            device.GetState(out result.EndpointState);

            object instance;
            Guid iid = AudioClientId;
            result.ActivateResult = device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out instance);
            if (result.ActivateResult < 0) return result;

            object endpointVolume;
            Guid endpointVolumeId = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
            result.EndpointVolumeActivateResult = device.Activate(ref endpointVolumeId, ClsctxAll, IntPtr.Zero, out endpointVolume);
            object meter;
            Guid meterId = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
            result.MeterActivateResult = device.Activate(ref meterId, ClsctxAll, IntPtr.Zero, out meter);

            IAudioClient client = (IAudioClient)instance;
            IntPtr mix = IntPtr.Zero;
            result.GetMixFormatResult = client.GetMixFormat(out mix);
            try { result.MixFormat = DescribeFormat(mix); }
            finally { if (mix != IntPtr.Zero) Marshal.FreeCoTaskMem(mix); }

            WaveFormatExtensible exact = new WaveFormatExtensible
            {
                FormatTag = 0xFFFE,
                Channels = 2,
                SamplesPerSecond = 48000,
                AverageBytesPerSecond = 192000,
                BlockAlign = 4,
                BitsPerSample = 16,
                ExtraSize = 22,
                ValidBitsPerSample = 16,
                ChannelMask = 3,
                SubFormat = PcmSubFormat
            };

            IntPtr exactPointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WaveFormatExtensible)));
            IntPtr closest = IntPtr.Zero;
            try
            {
                Marshal.StructureToPtr(exact, exactPointer, false);
                result.ExactSharedSupportResult = client.IsFormatSupported(0, exactPointer, out closest);
                result.ClosestFormat = DescribeFormat(closest);
                result.InitializeSharedResult = client.Initialize(0, 0, 1000000, 0, exactPointer, IntPtr.Zero);
                result.ExactExclusiveSupportResult = client.IsFormatSupported(1, exactPointer, out closest);

                object exclusiveInstance;
                Guid exclusiveIid = AudioClientId;
                int exclusiveActivate = device.Activate(ref exclusiveIid, ClsctxAll, IntPtr.Zero, out exclusiveInstance);
                if (exclusiveActivate >= 0)
                {
                    IAudioClient exclusiveClient = (IAudioClient)exclusiveInstance;
                    result.InitializeExclusiveResult = exclusiveClient.Initialize(1, 0, 100000, 100000, exactPointer, IntPtr.Zero);
                }
                else
                {
                    result.InitializeExclusiveResult = exclusiveActivate;
                }
            }
            finally
            {
                if (closest != IntPtr.Zero) Marshal.FreeCoTaskMem(closest);
                Marshal.FreeHGlobal(exactPointer);
            }

            return result;
        }
    }
}
'@
}

$captureRoot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture'
$matches = @()
foreach ($endpoint in Get-ChildItem $captureRoot) {
    $properties = Get-ItemProperty (Join-Path $endpoint.PSPath 'Properties') -ErrorAction SilentlyContinue
    $friendlyName = $properties.'{a45c254e-df1c-4efd-8020-67d146a850e0},2'
    $deviceName = $properties.'{b3f8fa53-0004-438e-9003-51a46e139bfc},6'
    if ($friendlyName -like $EndpointName -or $deviceName -like $EndpointName) {
        $matches += [pscustomobject]@{
            FriendlyName = $friendlyName
            DeviceName = $deviceName
            EndpointId = "{0.0.1.00000000}.$($endpoint.PSChildName)"
        }
    }
}

if ($matches.Count -eq 0) {
    throw "No capture endpoint matched '$EndpointName'."
}

function Format-HResult([int] $Value) {
    return ('0x{0:X8}' -f ([int64]$Value -band 0xFFFFFFFFL))
}
foreach ($match in $matches) {
    $result = [GrassiBoard.Diagnostics.WasapiProbe]::Run($match.EndpointId)
    [pscustomobject]@{
        FriendlyName = $match.FriendlyName
        DeviceName = $match.DeviceName
        EndpointId = $result.EndpointId
        EndpointState = $result.EndpointState
        Activate = Format-HResult $result.ActivateResult
        EndpointVolumeActivate = Format-HResult $result.EndpointVolumeActivateResult
        MeterActivate = Format-HResult $result.MeterActivateResult
        GetMixFormat = Format-HResult $result.GetMixFormatResult
        MixFormat = $result.MixFormat
        ExactSharedSupport = Format-HResult $result.ExactSharedSupportResult
        ExactExclusiveSupport = Format-HResult $result.ExactExclusiveSupportResult
        ClosestFormat = $result.ClosestFormat
        InitializeShared = Format-HResult $result.InitializeSharedResult
        InitializeExclusive = Format-HResult $result.InitializeExclusiveResult
    } | Format-List
}
