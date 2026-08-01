[CmdletBinding()]
param(
    [string]$DriverSource = (Join-Path $PSScriptRoot '..\src\GrassiBoard.Driver')
)

$ErrorActionPreference = 'Stop'
$resolvedSource = (Resolve-Path -LiteralPath $DriverSource).Path

$forbidden = Get-ChildItem -LiteralPath $resolvedSource -Recurse -File |
    Where-Object { $_.Extension -in '.inf', '.cat', '.sys', '.cer', '.pfx' }

if ($forbidden) {
    throw "Milestone 0 driver placeholder contains installable or sensitive driver files: $($forbidden.FullName -join ', ')"
}

$marker = Join-Path $resolvedSource 'README-NOT-A-DRIVER.txt'
if (-not (Test-Path -LiteralPath $marker)) {
    throw 'Driver placeholder safety marker is missing.'
}

Write-Host 'Driver placeholder validation passed: no installable driver is present.'
