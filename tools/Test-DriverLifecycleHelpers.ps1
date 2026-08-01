[CmdletBinding()]
param(
    [string]$DriverSource = (Join-Path $PSScriptRoot '..\src\GrassiBoard.Driver')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $DriverSource 'scripts\DriverScript.Common.ps1')

$rootDeviceFixture = [pscustomobject]@{
    Class = 'MEDIA'
    FriendlyName = 'GrassiBoard Virtual Audio'
    HardwareID = @('ROOT\GrassiBoardVirtualAudio')
    InstanceId = 'ROOT\GRASSIBOARD_VIRTUAL_AUDIO\0000'
    Status = 'OK'
}
$otherDeviceFixture = [pscustomobject]@{
    Class = 'MEDIA'
    FriendlyName = 'Unrelated device'
    HardwareID = @('ROOT\UnrelatedDevice')
    InstanceId = 'ROOT\UNRELATED_DEVICE\0000'
    Status = 'OK'
}
$endpointFixtures = @(
    [pscustomobject]@{ FriendlyName = 'Speakers (GrassiBoard Virtual Audio)'; Status = 'OK'; InstanceId = 'SWD\MMDEVAPI\render' },
    [pscustomobject]@{ FriendlyName = 'GrassiBoard Virtual Microphone (GrassiBoard Virtual Audio)'; Status = 'OK'; InstanceId = 'SWD\MMDEVAPI\capture' },
    [pscustomobject]@{ FriendlyName = 'Speakers (Unrelated device)'; Status = 'OK'; InstanceId = 'SWD\MMDEVAPI\other' }
)
$signedDriverFixtures = @(
    [pscustomobject]@{ DeviceID = 'ROOT\GRASSIBOARD_VIRTUAL_AUDIO\0000'; InfName = 'oem42.inf'; IsSigned = $true },
    [pscustomobject]@{ DeviceID = 'ROOT\UNRELATED_DEVICE\0000'; InfName = 'oem7.inf'; IsSigned = $true }
)

function Get-PnpDevice {
    [CmdletBinding()]
    param([string]$Class, [switch]$PresentOnly)
    if ($Class -eq 'AudioEndpoint') { return $endpointFixtures }
    return @($otherDeviceFixture, $rootDeviceFixture)
}

function Get-CimInstance {
    [CmdletBinding()]
    param([Parameter(Position = 0)][string]$ClassName)
    if ($ClassName -ne 'Win32_PnPSignedDriver') { throw "Unexpected CIM class: $ClassName" }
    return $signedDriverFixtures
}

$device = Get-GrassiBoardPnpDevice
if (-not $device -or $device.InstanceId -ne 'ROOT\GRASSIBOARD_VIRTUAL_AUDIO\0000') {
    throw 'Hardware-ID discovery did not resolve the generated root-device instance ID.'
}

$signedDriver = Get-GrassiBoardSignedDriver -Device $device
if (-not $signedDriver -or $signedDriver.InfName -ne 'oem42.inf') {
    throw 'Signed-driver discovery did not map the actual instance ID to its OEM INF.'
}

$endpoints = @(Get-GrassiBoardEndpointDevices)
if ($endpoints.Count -ne 2 -or @($endpoints | Where-Object Status -ne 'OK').Count -ne 0) {
    throw 'Endpoint discovery did not isolate the healthy GrassiBoard render and capture endpoints.'
}

Write-Host 'Driver lifecycle helper tests passed: HardwareID, generated instance ID, OEM INF, and endpoints.'
