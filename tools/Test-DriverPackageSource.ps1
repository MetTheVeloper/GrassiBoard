[CmdletBinding()]
param(
    [string]$DriverSource = (Join-Path $PSScriptRoot '..\src\GrassiBoard.Driver')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $DriverSource).Path
$inf = Get-Content -LiteralPath (Join-Path $root 'GrassiBoardVirtualAudio.inf') -Raw
$minipairs = Get-Content -LiteralPath (Join-Path $root 'Sysvad\GrassiBoardVirtualAudio\minipairs.h') -Raw
$captureWaveTable = Get-Content -LiteralPath (Join-Path $root 'Sysvad\GrassiBoardVirtualAudio\micinwavtable.h') -Raw
$captureTopologyTable = Get-Content -LiteralPath (Join-Path $root 'Sysvad\GrassiBoardVirtualAudio\micintoptable.h') -Raw
$transportHeader = Get-Content -LiteralPath (Join-Path $root 'Sysvad\EndpointsCommon\cabletransport.h') -Raw
$endpointProject = Get-Content -LiteralPath (Join-Path $root 'Sysvad\EndpointsCommon\GrassiBoard.EndpointsCommon.vcxproj') -Raw
$streamSource = Get-Content -LiteralPath (Join-Path $root 'Sysvad\EndpointsCommon\minwavertstream.cpp') -Raw
$transportSource = Get-Content -LiteralPath (Join-Path $root 'Sysvad\EndpointsCommon\cabletransport.cpp') -Raw
$convertHeader = Get-Content -LiteralPath (Join-Path $root 'Sysvad\EndpointsCommon\pcmconvert.h') -Raw
$ringSource = Get-Content -LiteralPath (Join-Path $root 'Sysvad\EndpointsCommon\pcmring.h') -Raw
$scriptsDirectory = Join-Path $root 'scripts'
$commonScript = Get-Content -LiteralPath (Join-Path $scriptsDirectory 'DriverScript.Common.ps1') -Raw
$installScript = Get-Content -LiteralPath (Join-Path $scriptsDirectory 'Install-GrassiBoardDriver.ps1') -Raw
$diagnosticsScript = Get-Content -LiteralPath (Join-Path $scriptsDirectory 'Collect-DriverDiagnostics.ps1') -Raw
$uninstallScript = Get-Content -LiteralPath (Join-Path $scriptsDirectory 'Uninstall-GrassiBoardDriver.ps1') -Raw

$requiredInfText = @(
    'ROOT\GrassiBoardVirtualAudio',
    'GrassiBoard Virtual Cable Input',
    'GrassiBoard Virtual Microphone',
    'CatalogFile=GrassiBoardVirtualAudio.cat'
)
foreach ($text in $requiredInfText) {
    if (-not $inf.Contains($text)) { throw "Driver INF is missing: $text" }
}

if (($minipairs | Select-String -Pattern '&GrassiBoardRenderMiniports' -AllMatches).Matches.Count -ne 1 -or
    ($minipairs | Select-String -Pattern '&GrassiBoardCaptureMiniports' -AllMatches).Matches.Count -ne 1) {
    throw 'The driver must define and register exactly one render and one capture endpoint.'
}

$requiredTransportText = @(
    'GrassiBoardCableTransport::SetRenderActive',
    'GrassiBoardCableTransport::SetCaptureActive',
    'GrassiBoardCableTransport::Write',
    'GrassiBoardCableTransport::Read'
)
foreach ($text in $requiredTransportText) {
    if (-not $streamSource.Contains($text)) { throw "WaveRT stream does not route PCM through: $text" }
}
if (-not $endpointProject.Contains('cabletransport.cpp') -or
    -not $endpointProject.Contains('pcmconvert.h') -or
    -not $transportSource.Contains('TransportPreRollBytes') -or
    -not $ringSource.Contains('m_underruns') -or
    -not $ringSource.Contains('m_overruns') -or
    -not $ringSource.Contains('m_generation') -or
    -not $ringSource.Contains('TOps::Zero(destination, byteCount)')) {
    throw 'The driver PCM ring must provide pre-roll, silence, stale-data invalidation, and underrun/overrun accounting.'
}
if (-not $minipairs.Contains('GrassiBoardCableRenderPcmFormats') -or
    -not $minipairs.Contains('GrassiBoardCableCapturePcmFormats') -or
    -not $minipairs.Contains('GrassiBoardCableTransport::SampleRate') -or
    -not $minipairs.Contains('GrassiBoardCableTransport::RenderBlockAlign') -or
    -not $minipairs.Contains('GrassiBoardCableTransport::CaptureBlockAlign') -or
    -not $minipairs.Contains('GrassiBoardRenderFormatsAndModes') -or
    -not $minipairs.Contains('GrassiBoardCaptureFormatsAndModes')) {
    throw 'Render and capture endpoints must advertise their fixed stereo-render and mono-capture PCM formats.'
}
if ($captureWaveTable -notmatch '#define\s+MICIN_DEVICE_MAX_CHANNELS\s+1' -or
    -not $captureTopologyTable.Contains('KSAUDIO_SPEAKER_MONO') -or
    -not $transportHeader.Contains('CaptureChannelCount = 1') -or
    -not $minipairs.Contains('KSAUDIO_SPEAKER_MONO')) {
    throw 'The capture format, data range, and topology jack must preserve the reference mono SysVAD MicIn contract.'
}
if (-not $transportHeader.Contains('RenderChannelCount = 2') -or
    -not $convertHeader.Contains('GrassiBoardDownmixStereo16ToMono16') -or
    -not $transportSource.Contains('GrassiBoardDownmixStereo16ToMono16')) {
    throw 'The stereo render stream must be explicitly downmixed into the mono capture ring.'
}
if ($captureWaveTable -notmatch '#define\s+MICIN_MAX_INPUT_STREAMS\s+5') {
    throw 'The capture pin must retain the SysVAD reference instance capacity.'
}

$renderTopologySection = [regex]::Match(
    $inf,
    '(?ms)^\[GrassiBoard\.I\.TopologyRender\.AddReg\]\s*(.*?)(?=^\[)'
).Groups[1].Value
$captureTopologySection = [regex]::Match(
    $inf,
    '(?ms)^\[GrassiBoard\.I\.TopologyCapture\.AddReg\]\s*(.*?)(?=^\[)'
).Groups[1].Value
if (-not $renderTopologySection.Contains('PKEY_AudioEndpoint_Supports_EventDriven_Mode')) {
    throw 'The render endpoint must retain event-driven WaveRT support.'
}
if (-not $captureTopologySection.Contains('PKEY_AudioEndpoint_Supports_EventDriven_Mode')) {
    throw 'The capture endpoint must preserve the reference SysVAD event-driven WaveRT contract.'
}

$requiredScriptText = @(
    'function Get-GrassiBoardPnpDevice',
    '@($_.HardwareID) -icontains $script:GrassiBoardHardwareId',
    'function Get-GrassiBoardSignedDriver',
    'function Get-GrassiBoardInstalledSignerCertificate'
)
foreach ($text in $requiredScriptText) {
    if (-not $commonScript.Contains($text)) { throw "Driver helpers are missing: $text" }
}
if (-not $installScript.Contains("`$device.Status -eq 'OK'") -or
    -not $diagnosticsScript.Contains('Get-GrassiBoardSignedDriver') -or
    -not $uninstallScript.Contains('Get-GrassiBoardInstalledSignerCertificate')) {
    throw 'Driver lifecycle scripts do not use the hardware-ID based identity and recovery helpers.'
}

$scriptCorpus = $commonScript + $installScript + $diagnosticsScript + $uninstallScript
if ($scriptCorpus -match '(?i)\.(?:InstanceId|DeviceID)\s+-like\s+[''"]ROOT\\') {
    throw 'Driver scripts must not assume that a generated PnP instance ID matches the hardware ID.'
}

foreach ($scriptFile in Get-ChildItem -LiteralPath $scriptsDirectory -Filter '*.ps1' -File) {
    $tokens = $null
    $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($scriptFile.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count) { throw "PowerShell parse failure in $($scriptFile.Name): $($errors[0].Message)" }
}

$sensitive = Get-ChildItem -LiteralPath $root -Recurse -File |
    Where-Object { $_.Extension -in '.pfx', '.pvk', '.key' }
if ($sensitive) { throw "Private key material must never be committed: $($sensitive.FullName -join ', ')" }

$placeholder = Join-Path $root 'README-NOT-A-DRIVER.txt'
if (Test-Path -LiteralPath $placeholder) { throw 'Milestone 4 still contains the old driver placeholder.' }

$upstream = Get-Content -LiteralPath (Join-Path $root 'UPSTREAM.md') -Raw
if ($upstream -notmatch 'ef7c3074748ab05726c3a9161d3256118efd76e2') {
    throw 'The SysVAD upstream commit is not pinned.'
}

Write-Host 'Driver source contract passed: unique IDs, two endpoints, reference mono MicIn contract, fixed-format PCM ring transport, lifecycle recovery, pinned provenance, and no private key material.'
