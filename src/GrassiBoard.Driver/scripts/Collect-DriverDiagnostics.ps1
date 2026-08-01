[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $env:TEMP "GrassiBoard-driver-diagnostics-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss')).txt")
)

. (Join-Path $PSScriptRoot 'DriverScript.Common.ps1')

$device = Get-GrassiBoardPnpDevice
$endpoints = @(Get-GrassiBoardEndpointDevices)
$signedDriver = Get-GrassiBoardSignedDriver -Device $device
$lines = [Collections.Generic.List[string]]::new()
$lines.Add("CollectedUtc: $([DateTime]::UtcNow.ToString('o'))")
$lines.Add("Windows: $([Environment]::OSVersion.VersionString)")
$lines.Add('')
$lines.Add('=== BCDEdit current entry ===')
$lines.Add((& bcdedit.exe /enum '{current}' 2>&1 | Out-String))
$lines.Add('=== Windows Audio services ===')
$lines.Add((Get-Service Audiosrv, AudioEndpointBuilder | Format-List Name, Status, StartType | Out-String))
$lines.Add('=== GrassiBoard PnP devices ===')
$lines.Add((@($device) + $endpoints | Format-List * | Out-String))
$lines.Add('=== Signed driver ===')
$lines.Add(($signedDriver | Format-List * | Out-String))
$lines.Add('=== Installer state ===')
if (Test-Path -LiteralPath $script:GrassiBoardStatePath) {
    $lines.Add((Get-Content -LiteralPath $script:GrassiBoardStatePath -Raw))
} else {
    $lines.Add('State file is absent.')
}
$lines.Add('=== Recent System events (audio/driver only) ===')
$events = Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=(Get-Date).AddHours(-2)} -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -match 'Kernel-PnP|Audio' -or $_.Message -match 'GrassiBoard|GRASSIBOARD_VIRTUAL_AUDIO' } |
    Select-Object -First 100 TimeCreated, Id, LevelDisplayName, ProviderName, Message
$lines.Add(($events | Format-List | Out-String))

$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Diagnostics written to $OutputPath"
