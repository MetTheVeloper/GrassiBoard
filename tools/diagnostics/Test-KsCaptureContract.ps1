[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if (-not ('GrassiBoard.Diagnostics.KsNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GrassiBoard.Diagnostics
{
    public sealed class KsQueryResult
    {
        public bool Success;
        public int Error;
        public int BytesReturned;
        public byte[] Data;
    }

    public static class KsNative
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint IoctlKsProperty = 0x002F0003;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint controlCode,
            IntPtr input,
            int inputSize,
            IntPtr output,
            int outputSize,
            out int bytesReturned,
            IntPtr overlapped);

        [DllImport("ksuser.dll", SetLastError = true)]
        private static extern uint KsCreatePin(
            SafeFileHandle filter,
            IntPtr connect,
            uint desiredAccess,
            out SafeFileHandle connection);

        public static SafeFileHandle Open(string path)
        {
            SafeFileHandle handle = CreateFile(
                path,
                GenericRead | GenericWrite,
                ShareRead | ShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open KS filter " + path);
            }

            return handle;
        }

        public static KsQueryResult Query(SafeFileHandle handle, byte[] request, int outputCapacity)
        {
            IntPtr input = Marshal.AllocHGlobal(request.Length);
            IntPtr output = Marshal.AllocHGlobal(outputCapacity);
            try
            {
                Marshal.Copy(request, 0, input, request.Length);
                int bytesReturned;
                bool success = DeviceIoControl(
                    handle,
                    IoctlKsProperty,
                    input,
                    request.Length,
                    output,
                    outputCapacity,
                    out bytesReturned,
                    IntPtr.Zero);
                int error = success ? 0 : Marshal.GetLastWin32Error();
                int copyLength = Math.Max(0, Math.Min(bytesReturned, outputCapacity));
                byte[] data = new byte[copyLength];
                if (copyLength > 0)
                {
                    Marshal.Copy(output, data, 0, copyLength);
                }

                return new KsQueryResult
                {
                    Success = success,
                    Error = error,
                    BytesReturned = bytesReturned,
                    Data = data
                };
            }
            finally
            {
                Marshal.FreeHGlobal(output);
                Marshal.FreeHGlobal(input);
            }
        }

        public static KsQueryResult Set(SafeFileHandle handle, byte[] request, byte[] propertyData)
        {
            IntPtr input = Marshal.AllocHGlobal(request.Length);
            IntPtr output = Marshal.AllocHGlobal(propertyData.Length);
            try
            {
                Marshal.Copy(request, 0, input, request.Length);
                Marshal.Copy(propertyData, 0, output, propertyData.Length);
                int bytesReturned;
                bool success = DeviceIoControl(
                    handle,
                    IoctlKsProperty,
                    input,
                    request.Length,
                    output,
                    propertyData.Length,
                    out bytesReturned,
                    IntPtr.Zero);
                int error = success ? 0 : Marshal.GetLastWin32Error();
                return new KsQueryResult
                {
                    Success = success,
                    Error = error,
                    BytesReturned = bytesReturned,
                    Data = new byte[0]
                };
            }
            finally
            {
                Marshal.FreeHGlobal(output);
                Marshal.FreeHGlobal(input);
            }
        }

        public static uint CreatePin(SafeFileHandle filter, byte[] connectAndFormat)
        {
            IntPtr connect = Marshal.AllocHGlobal(connectAndFormat.Length);
            try
            {
                Marshal.Copy(connectAndFormat, 0, connect, connectAndFormat.Length);
                SafeFileHandle connection;
                uint result = KsCreatePin(filter, connect, GenericRead, out connection);
                if (result == 0 && connection != null)
                {
                    connection.Dispose();
                }
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(connect);
            }
        }
    }
}
'@
}

function New-KsPinRequest {
    param(
        [Parameter(Mandatory)] [uint32] $PropertyId,
        [Parameter(Mandatory)] [uint32] $PinId,
        [uint32] $Flags = 1
    )

    $bytes = New-Object byte[] 32
    [Guid]::Parse('8c134960-51ad-11cf-878a-94f801c10000').ToByteArray().CopyTo($bytes, 0)
    [BitConverter]::GetBytes($PropertyId).CopyTo($bytes, 16)
    [BitConverter]::GetBytes($Flags).CopyTo($bytes, 20)
    [BitConverter]::GetBytes($PinId).CopyTo($bytes, 24)
    return $bytes
}

function New-ExactCableKsFormat {
    $bytes = New-Object byte[] 104
    [BitConverter]::GetBytes([uint32]104).CopyTo($bytes, 0)
    [Guid]::Parse('73647561-0000-0010-8000-00aa00389b71').ToByteArray().CopyTo($bytes, 16) # AUDIO
    [Guid]::Parse('00000001-0000-0010-8000-00aa00389b71').ToByteArray().CopyTo($bytes, 32) # PCM
    [Guid]::Parse('05589f81-c356-11ce-bf01-00aa0055595a').ToByteArray().CopyTo($bytes, 48) # WAVEFORMATEX
    [BitConverter]::GetBytes([uint16]0xFFFE).CopyTo($bytes, 64)
    [BitConverter]::GetBytes([uint16]2).CopyTo($bytes, 66)
    [BitConverter]::GetBytes([uint32]48000).CopyTo($bytes, 68)
    [BitConverter]::GetBytes([uint32]192000).CopyTo($bytes, 72)
    [BitConverter]::GetBytes([uint16]4).CopyTo($bytes, 76)
    [BitConverter]::GetBytes([uint16]16).CopyTo($bytes, 78)
    [BitConverter]::GetBytes([uint16]22).CopyTo($bytes, 80)
    [BitConverter]::GetBytes([uint16]16).CopyTo($bytes, 82)
    [BitConverter]::GetBytes([uint32]3).CopyTo($bytes, 84)
    [Guid]::Parse('00000001-0000-0010-8000-00aa00389b71').ToByteArray().CopyTo($bytes, 88)
    return $bytes
}

function New-ProposeDataformat2Request {
    param(
        [Parameter(Mandatory)] [uint32] $PinId,
        [Parameter(Mandatory)] [Guid] $Mode
    )

    $bytes = New-Object byte[] 80
    (New-KsPinRequest -PropertyId 15 -PinId $PinId -Flags 1).CopyTo($bytes, 0)
    [BitConverter]::GetBytes([uint32]48).CopyTo($bytes, 32) # bytes after KSP_PIN
    [BitConverter]::GetBytes([uint32]1).CopyTo($bytes, 36)
    [BitConverter]::GetBytes([uint32]40).CopyTo($bytes, 40)
    [BitConverter]::GetBytes([uint32]0).CopyTo($bytes, 44)
    [Guid]::Parse('e1f89eb5-5f46-419b-967b-ff6770b98401').ToByteArray().CopyTo($bytes, 48)
    $Mode.ToByteArray().CopyTo($bytes, 64)
    return $bytes
}

function New-KsCapturePinConnect {
    param([Guid] $Mode)

    $format = New-ExactCableKsFormat
    $hasMode = $PSBoundParameters.ContainsKey('Mode')
    $attributeBytes = if ($hasMode) { 48 } else { 0 }
    $bytes = New-Object byte[] (72 + $format.Length + $attributeBytes)
    [Guid]::Parse('1a8766a0-62ce-11cf-a5d6-28db04c10000').ToByteArray().CopyTo($bytes, 0)
    [BitConverter]::GetBytes([uint32]1).CopyTo($bytes, 16) # KSINTERFACE_STANDARD_STREAMING
    [Guid]::Parse('4747b320-62ce-11cf-a5d6-28db04c10000').ToByteArray().CopyTo($bytes, 24)
    [BitConverter]::GetBytes([uint32]0).CopyTo($bytes, 40) # KSMEDIUM_TYPE_ANYINSTANCE
    [BitConverter]::GetBytes([uint32]1).CopyTo($bytes, 48) # capture streaming pin
    [BitConverter]::GetBytes([uint32]0x40000000).CopyTo($bytes, 64) # KSPRIORITY_NORMAL
    [BitConverter]::GetBytes([uint32]1).CopyTo($bytes, 68)
    $format.CopyTo($bytes, 72)
    if ($hasMode) {
        [BitConverter]::GetBytes([uint32]2).CopyTo($bytes, 76) # KSDATAFORMAT_ATTRIBUTES
        [BitConverter]::GetBytes([uint32]48).CopyTo($bytes, 176)
        [BitConverter]::GetBytes([uint32]1).CopyTo($bytes, 180)
        [BitConverter]::GetBytes([uint32]40).CopyTo($bytes, 184)
        [Guid]::Parse('e1f89eb5-5f46-419b-967b-ff6770b98401').ToByteArray().CopyTo($bytes, 192)
        $Mode.ToByteArray().CopyTo($bytes, 208)
    }
    return $bytes
}

function Invoke-KsPinQuery {
    param(
        [Parameter(Mandatory)] $Handle,
        [Parameter(Mandatory)] [uint32] $PropertyId,
        [Parameter(Mandatory)] [uint32] $PinId,
        [int] $Capacity = 65536
    )

    $request = New-KsPinRequest -PropertyId $PropertyId -PinId $PinId
    return [GrassiBoard.Diagnostics.KsNative]::Query($Handle, $request, $Capacity)
}

function Get-EndpointKsPath {
    $captureRoot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture'
    foreach ($endpoint in Get-ChildItem $captureRoot) {
        $propertiesPath = Join-Path $endpoint.PSPath 'Properties'
        $properties = Get-ItemProperty $propertiesPath -ErrorAction SilentlyContinue
        if ($properties.'{a45c254e-df1c-4efd-8020-67d146a850e0},2' -ne 'GrassiBoard Virtual Microphone') {
            continue
        }

        $rawPath = $properties.'{233164c8-1b2c-4c7d-bc68-b671687a2567},1'
        if ($rawPath -match '(\\\\\?\\.*)$') {
            return $Matches[1]
        }

        throw "The GrassiBoard endpoint has no usable KS filter path: $rawPath"
    }

    throw 'The GrassiBoard capture endpoint was not found.'
}

function Read-UInt32 {
    param([byte[]] $Bytes, [int] $Offset)
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

$path = Get-EndpointKsPath
$handle = [GrassiBoard.Diagnostics.KsNative]::Open($path)
try {
    $pinTypes = Invoke-KsPinQuery -Handle $handle -PropertyId 1 -PinId 0 -Capacity 4
    if (-not $pinTypes.Success -or $pinTypes.Data.Length -lt 4) {
        throw "KSPROPERTY_PIN_CTYPES failed: win32=$($pinTypes.Error), returned=$($pinTypes.BytesReturned)"
    }

    $pinCount = Read-UInt32 $pinTypes.Data 0
    Write-Output "filter=$path"
    Write-Output "pinCount=$pinCount"

    for ($pin = 0; $pin -lt $pinCount; $pin++) {
        $flow = Invoke-KsPinQuery -Handle $handle -PropertyId 2 -PinId $pin -Capacity 4
        $communication = Invoke-KsPinQuery -Handle $handle -PropertyId 7 -PinId $pin -Capacity 4
        $instances = Invoke-KsPinQuery -Handle $handle -PropertyId 0 -PinId $pin -Capacity 8
        $ranges = Invoke-KsPinQuery -Handle $handle -PropertyId 3 -PinId $pin

        $flowValue = if ($flow.Success -and $flow.Data.Length -ge 4) { Read-UInt32 $flow.Data 0 } else { "error:$($flow.Error)" }
        $communicationValue = if ($communication.Success -and $communication.Data.Length -ge 4) { Read-UInt32 $communication.Data 0 } else { "error:$($communication.Error)" }
        $possible = if ($instances.Success -and $instances.Data.Length -ge 8) { Read-UInt32 $instances.Data 0 } else { "error:$($instances.Error)" }
        $current = if ($instances.Success -and $instances.Data.Length -ge 8) { Read-UInt32 $instances.Data 4 } else { '-' }

        Write-Output "pin=$pin flow=$flowValue communication=$communicationValue possibleInstances=$possible currentInstances=$current"

        if ($pin -eq 1) {
            $proposeRequest = New-KsPinRequest -PropertyId 14 -PinId $pin -Flags 2 # KSPROPERTY_TYPE_SET
            $propose = [GrassiBoard.Diagnostics.KsNative]::Set($handle, $proposeRequest, (New-ExactCableKsFormat))
            Write-Output "  proposeExact48kStereo=success:$($propose.Success) win32:$($propose.Error) returned:$($propose.BytesReturned)"

            $modes = [ordered]@{
                Raw = [Guid]::Parse('9e90ea20-b493-4fd1-a1a8-7e1361a956cf')
                Default = [Guid]::Parse('c18e2f7e-933d-4965-b7d1-1eef228d2af3')
                Speech = [Guid]::Parse('fc1cfc9b-b9d6-4cfa-b5e0-4bb2166878b2')
                Communications = [Guid]::Parse('98951333-b9cd-48b1-a0a3-ff40682d73f7')
                FarFieldSpeech = [Guid]::Parse('28941cba-3be6-4a78-9a76-30fd91559b64')
            }
            foreach ($mode in $modes.GetEnumerator()) {
                $modeRequest = New-ProposeDataformat2Request -PinId $pin -Mode $mode.Value
                $modeResult = [GrassiBoard.Diagnostics.KsNative]::Query($handle, $modeRequest, 256)
                Write-Output "  proposedMode=$($mode.Key) success:$($modeResult.Success) win32:$($modeResult.Error) returned:$($modeResult.BytesReturned)"
            }


            $createPinResult = [GrassiBoard.Diagnostics.KsNative]::CreatePin($handle, (New-KsCapturePinConnect))
            Write-Output "  createExactCapturePin=win32:$createPinResult"
            foreach ($mode in $modes.GetEnumerator()) {
                $modePinResult = [GrassiBoard.Diagnostics.KsNative]::CreatePin($handle, (New-KsCapturePinConnect -Mode $mode.Value))
                Write-Output "  createCapturePinMode=$($mode.Key) win32:$modePinResult"
            }
        }

        if (-not $ranges.Success -or $ranges.Data.Length -lt 8) {
            Write-Output "  dataRanges=error:$($ranges.Error) returned=$($ranges.BytesReturned)"
            continue
        }

        $totalSize = Read-UInt32 $ranges.Data 0
        $rangeCount = Read-UInt32 $ranges.Data 4
        Write-Output "  dataRanges=count:$rangeCount bytes:$totalSize"

        $offset = 8
        for ($index = 0; $index -lt $rangeCount -and ($offset + 64) -le $ranges.Data.Length; $index++) {
            $formatSize = Read-UInt32 $ranges.Data $offset
            $flags = Read-UInt32 $ranges.Data ($offset + 4)
            if (($offset + 84) -le $ranges.Data.Length -and $formatSize -ge 84) {
                $maxChannels = Read-UInt32 $ranges.Data ($offset + 64)
                $minBits = Read-UInt32 $ranges.Data ($offset + 68)
                $maxBits = Read-UInt32 $ranges.Data ($offset + 72)
                $minRate = Read-UInt32 $ranges.Data ($offset + 76)
                $maxRate = Read-UInt32 $ranges.Data ($offset + 80)
                Write-Output "  range=$index formatSize=$formatSize flags=0x$($flags.ToString('X8')) channels=1..$maxChannels bits=$minBits..$maxBits rate=$minRate..$maxRate"
            }
            else {
                Write-Output "  range=$index formatSize=$formatSize flags=0x$($flags.ToString('X8'))"
            }
            $offset += (($formatSize + 7) -band (-bnot 7))
        }
    }
}
finally {
    $handle.Dispose()
}
