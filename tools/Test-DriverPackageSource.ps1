[CmdletBinding()]
param(
    [string]$DriverSource = (Join-Path $PSScriptRoot '..\src\GrassiBoard.Driver')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $DriverSource).Path
$inf = Get-Content -LiteralPath (Join-Path $root 'GrassiBoardVirtualAudio.inf') -Raw
$minipairs = Get-Content -LiteralPath (Join-Path $root 'Sysvad\GrassiBoardVirtualAudio\minipairs.h') -Raw

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

$sensitive = Get-ChildItem -LiteralPath $root -Recurse -File |
    Where-Object { $_.Extension -in '.pfx', '.pvk', '.key' }
if ($sensitive) { throw "Private key material must never be committed: $($sensitive.FullName -join ', ')" }

$placeholder = Join-Path $root 'README-NOT-A-DRIVER.txt'
if (Test-Path -LiteralPath $placeholder) { throw 'Milestone 4 still contains the old driver placeholder.' }

$upstream = Get-Content -LiteralPath (Join-Path $root 'UPSTREAM.md') -Raw
if ($upstream -notmatch 'ef7c3074748ab05726c3a9161d3256118efd76e2') {
    throw 'The SysVAD upstream commit is not pinned.'
}

Write-Host 'Driver source contract passed: unique IDs, two endpoints, pinned provenance, and no private key material.'
