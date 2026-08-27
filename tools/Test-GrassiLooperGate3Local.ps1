[CmdletBinding()]
param(
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildScript = Join-Path $PSScriptRoot 'Build-LocalRemoteTest.ps1'
$appExe = Join-Path $repositoryRoot 'artifacts\local-test\GrassiBoard\GrassiBoard.exe'

function Require-Text([string]$Path, [string[]]$Needles, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label source file is missing: $Path"
    }
    $text = Get-Content -LiteralPath $Path -Raw
    foreach ($needle in $Needles) {
        if (-not $text.Contains($needle, [StringComparison]::Ordinal)) {
            throw "$Label contract is missing: $needle"
        }
    }
}

function Resolve-Ctest {
    $command = Get-Command ctest -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $vswhereCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($vswhere in $vswhereCandidates) {
        try {
            $match = & $vswhere `
                -products * `
                -latest `
                -requires Microsoft.VisualStudio.Component.VC.CMake.Project `
                -find 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe' 2>$null |
                Select-Object -First 1
            if ($match) {
                $candidate = "$match".Trim()
                if (Test-Path -LiteralPath $candidate) { return $candidate }
            }
        }
        catch { }
    }

    throw 'ctest was not found. Install Visual Studio 2022 Build Tools with Desktop development with C++ / CMake tools.'
}

Push-Location $repositoryRoot
try {
    $branch = (& git rev-parse --abbrev-ref HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'feature/grassilooper-v1.4') {
        throw "Run this test from feature/grassilooper-v1.4. Current branch: '$branch'"
    }

    Write-Host '=== GrassiLooper Gate 3 LOCAL validation ===' -ForegroundColor Cyan
    Write-Host '1/4 Building native engine, Remote Web and local WPF package...' -ForegroundColor Cyan
    & $buildScript

    Write-Host '2/4 Running native deterministic tests locally...' -ForegroundColor Cyan
    $ctest = Resolve-Ctest
    & $ctest --preset windows-x64-release --output-on-failure
    if ($LASTEXITCODE -ne 0) {
        throw "Native ctest failed with exit code $LASTEXITCODE."
    }

    Write-Host '3/4 Compiling the managed smoke dependency graph locally...' -ForegroundColor Cyan
    & dotnet build .\tests\GrassiBoard.App.SmokeTests\GrassiBoard.App.SmokeTests.csproj `
        --configuration Release `
        --no-incremental
    if ($LASTEXITCODE -ne 0) {
        throw "Managed smoke dependency build failed with exit code $LASTEXITCODE."
    }

    Write-Host '4/4 Checking Gate 3 ABI / Record Tap source contracts...' -ForegroundColor Cyan

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Services\NativeAudioEngine.cs') `
        @('ExpectedApiVersion = 11U') `
        'Managed ABI 11'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Services\NativeAudioEngine.Looper.cs') `
        @(
            'gb_looper_record_start',
            'gb_looper_record_stop',
            'gb_looper_record_read',
            'gb_looper_record_get_state',
            'LooperRecordNativeState'
        ) `
        'Managed Looper Record ABI'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.AudioEngine\src\audio_engine.cpp') `
        @(
            'constexpr std::uint32_t kApiVersion = 11',
            '1.4.0-gate3'
        ) `
        'Native ABI 11'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.AudioEngine\include\grassiboard\audio_engine.h') `
        @(
            'gb_looper_record_start',
            'gb_looper_record_stop',
            'gb_looper_record_read',
            'gb_looper_record_get_state'
        ) `
        'Native Looper Record ABI'

    $workerPath = Join-Path $repositoryRoot 'src\GrassiBoard.AudioEngine\src\wasapi_engine.cpp'
    $worker = Get-Content -LiteralPath $workerPath -Raw
    $recordTapIndex = $worker.IndexOf('looper_record_tap_.Push(recorded, recorded)', [StringComparison]::Ordinal)
    $muteIndex = $worker.IndexOf('Program Mic Mute happens after the dedicated Looper Record Tap', [StringComparison]::Ordinal)
    if ($recordTapIndex -lt 0 -or $muteIndex -lt 0 -or $recordTapIndex -ge $muteIndex) {
        throw 'Gate 3 Record Tap is not verifiably before Program Mic Mute.'
    }
    if (-not $worker.Contains('looper_record_source_changed_.store(true', [StringComparison]::Ordinal)) {
        throw 'Gate 3 source-change fail-safe is missing from the native worker.'
    }

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Services\Looper\LooperRecordService.cs') `
        @(
            '_drainedFrames',
            'The Take was discarded instead of combining two inputs',
            'audioState.Running == 0U',
            'MaxCaptureMinutes = 10'
        ) `
        'Managed Looper Record service'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Views\Looper\LooperView.xaml') `
        @(
            'Record First Loop',
            'Gate 3 processed microphone capture'
        ) `
        'Gate 3 Looper UI'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Views\Looper\LooperView.xaml.cs') `
        @(
            'LooperRecordService',
            'WriteTakeWaveAsync'
        ) `
        'Gate 3 record-to-editor handoff'

    Write-Host ''
    Write-Host 'GATE 3 LOCAL AUTOMATED VALIDATION: PASS' -ForegroundColor Green
    Write-Host 'Next: perform the real microphone checklist from ChatGPT.' -ForegroundColor Green

    if ($Run) {
        if (-not (Test-Path -LiteralPath $appExe)) {
            throw "Local test executable was not found at $appExe"
        }
        Write-Host 'Launching local GrassiBoard build...' -ForegroundColor Cyan
        Start-Process -FilePath $appExe -WorkingDirectory (Split-Path $appExe -Parent)
    }
}
finally {
    Pop-Location
}
