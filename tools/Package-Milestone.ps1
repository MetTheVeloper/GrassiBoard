[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ShortSha,
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [Parameter(Mandatory = $true)][string]$NativeDirectory,
    [Parameter(Mandatory = $true)][string]$DriverDirectory,
    [Parameter(Mandatory = $true)][string]$TestResultsDirectory,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\packages')
)

$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$stagingRoot = Join-Path $output 'staging'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

if (-not $output.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must remain inside the repository: $repositoryRoot"
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

$portableStage = Join-Path $stagingRoot 'portable'
$driverStage = Join-Path $stagingRoot 'driver'
$symbolsStage = Join-Path $stagingRoot 'symbols'
$testsStage = Join-Path $stagingRoot 'tests'
New-Item -ItemType Directory -Path $portableStage, $driverStage, $symbolsStage, $testsStage -Force | Out-Null

Copy-Item -Path (Join-Path $PublishDirectory '*') -Destination $portableStage -Recurse -Force
Copy-Item -Path (Join-Path $NativeDirectory 'GrassiBoard.AudioEngine.dll') -Destination $portableStage -Force
Copy-Item -Path (Join-Path $DriverDirectory '*') -Destination $driverStage -Recurse -Force
$portableDriverStage = Join-Path $portableStage 'driver-placeholder'
New-Item -ItemType Directory -Path $portableDriverStage -Force | Out-Null
Copy-Item -Path (Join-Path $DriverDirectory '*') -Destination $portableDriverStage -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\README.md') -Destination $portableStage -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\CHANGELOG.md') -Destination $portableStage -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\README-FIRST.txt') -Destination $portableStage -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\THIRD-PARTY-NOTICES.txt') -Destination $portableStage -Force

Get-ChildItem -Path $PublishDirectory, $NativeDirectory -Filter '*.pdb' -Recurse -ErrorAction SilentlyContinue |
    Copy-Item -Destination $symbolsStage -Force
Copy-Item -Path (Join-Path $TestResultsDirectory '*') -Destination $testsStage -Recurse -Force

$packages = [ordered]@{
    Portable = Join-Path $output "GrassiBoard-portable-win-x64-$Version-$ShortSha.zip"
    Driver = Join-Path $output "GrassiBoard-driver-x64-$Version-$ShortSha.zip"
    Symbols = Join-Path $output "GrassiBoard-symbols-$Version-$ShortSha.zip"
    Tests = Join-Path $output "GrassiBoard-test-results-$Version-$ShortSha.zip"
}

foreach ($archive in $packages.Values) {
    if (Test-Path -LiteralPath $archive) {
        Remove-Item -LiteralPath $archive -Force
    }
}

Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $packages.Portable -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $driverStage '*') -DestinationPath $packages.Driver -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $symbolsStage '*') -DestinationPath $packages.Symbols -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $testsStage '*') -DestinationPath $packages.Tests -CompressionLevel Optimal

$packages.Values | ForEach-Object { Write-Host "Created $_" }
