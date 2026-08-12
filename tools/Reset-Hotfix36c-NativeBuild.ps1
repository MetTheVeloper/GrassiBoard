[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

$engineSource = Join-Path $root 'src\GrassiBoard.AudioEngine\src\audio_engine.cpp'
$testSource = Join-Path $root 'tests\GrassiBoard.AudioEngine.Tests\audio_engine_tests.cpp'
$releaseBuild = Join-Path $root 'out\build\windows-x64-release'

foreach ($path in @($engineSource, $testSource)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Hotfix 36c file is missing: $path"
    }
}

$engineText = [IO.File]::ReadAllText($engineSource)
$testText = [IO.File]::ReadAllText($testSource)

if ($engineText.IndexOf('constexpr char kEngineVersion[] = "1.2.0";', [StringComparison]::Ordinal) -lt 0) {
    throw 'Runtime audio_engine.cpp does not report v1.2.0. Re-extract Hotfix 36c and retry.'
}

if ($testText.IndexOf('constexpr const char* expectedEngineVersion = "1.2.0";', [StringComparison]::Ordinal) -lt 0) {
    throw 'ABI smoke test does not expect v1.2.0. Re-extract Hotfix 36c and retry.'
}

Write-Host 'Hotfix 36c source/test version strings are synchronized at v1.2.0.' -ForegroundColor Green

if (Test-Path -LiteralPath $releaseBuild) {
    Write-Host "Removing stale native release build cache: $releaseBuild" -ForegroundColor Cyan
    Remove-Item -LiteralPath $releaseBuild -Recurse -Force
}

Write-Host 'Native release cache cleared. The next build will be a clean ABI-9 compile.' -ForegroundColor Green
Write-Host ''
Write-Host 'Run:' -ForegroundColor Cyan
Write-Host 'powershell -ExecutionPolicy Bypass -File .\tools\Build-LocalRemoteTest.ps1 -Run -RunSmokeTests'
