[CmdletBinding()]
param(
    [string]$DriverSource = (Join-Path $PSScriptRoot '..\src\GrassiBoard.Driver')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $DriverSource).Path
$inf = Get-Content -LiteralPath (Join-Path $root 'GrassiBoardVirtualAudio.inf') -Raw
$minipairs = Get-Content -LiteralPath (Join-Path $root 'Sysvad\GrassiBoardVirtualAudio\minipairs.h') -Raw
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

Write-Host 'Driver source contract passed: unique IDs, two endpoints, hardware-ID lifecycle recovery, pinned provenance, and no private key material.'
