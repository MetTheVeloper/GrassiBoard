[CmdletBinding()]
param(
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gate3 = Join-Path $PSScriptRoot 'Test-GrassiLooperGate3Local.ps1'
$appExe = Join-Path $repositoryRoot 'artifacts\local-test\GrassiBoard\GrassiBoard.exe'

function Require-Text([string]$Path, [string[]]$Needles, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Label source file is missing: $Path" }
    $text = Get-Content -LiteralPath $Path -Raw
    foreach ($needle in $Needles) {
        if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
            throw "$Label contract is missing: $needle"
        }
    }
}

Push-Location $repositoryRoot
try {
    $branch = (& git rev-parse --abbrev-ref HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'feature/grassilooper-v1.4') {
        throw "Run this test from feature/grassilooper-v1.4. Current branch: '$branch'"
    }

    Write-Host '=== GrassiLooper Gate 4 LOCAL validation ===' -ForegroundColor Cyan
    Write-Host 'Running build/native tests plus Looper ModuleInitializer smoke tests (including Gate 4)...' -ForegroundColor Cyan
    & $gate3
    if ($LASTEXITCODE -ne 0) { throw "Gate 3 baseline / shared Looper smoke validation failed with exit code $LASTEXITCODE." }

    Write-Host 'Checking Gate 4 child-layer source/UI contracts...' -ForegroundColor Cyan
    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.AudioEngine\include\grassiboard\audio_engine.h') `
        @('gb_looper_track_set_audio', 'gb_looper_track_remove', 'gb_looper_track_set_mix') `
        'Gate 4 native child Track ABI'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.AudioEngine\src\looper_engine.cpp') `
        @('MaxChildTrackBytes', 'anySolo', 'track.samples', 'previousFrames != frameCount') `
        'Gate 4 native child Track engine'

    # Gate 4's deterministic ModuleInitializer smoke test executes the actual
    # One Cycle / Loop Replace / Overdub composer behavior above. These source
    # checks intentionally verify only stable structural markers instead of
    # requiring an implementation-specific literal branch name.
    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Services\Looper\LooperLayerComposer.cs') `
        @('LooperLayerRecordMode.OneCycle', 'LooperLayerRecordMode.Overdub', 'frame % loopLength') `
        'Gate 4 layer mode composer'

    Require-Text `
        (Join-Path $repositoryRoot 'tests\GrassiBoard.App.SmokeTests\LooperGate4Smoke.cs') `
        @('LooperLayerRecordMode.LoopReplace', 'expectedReplace', 'expectedOverdub') `
        'Gate 4 deterministic recording-mode smoke'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Views\Looper\LooperView.xaml') `
        @('Add Layer', 'One Cycle', 'Loop Replace', 'Overdub', 'Cancel / Discard', 'Undo') `
        'Gate 4 UI'

    Require-Text `
        (Join-Path $repositoryRoot 'src\GrassiBoard.App\Views\Looper\LooperView.xaml.cs') `
        @('LooperLayerRecordState.Armed', 'FinishLayerRecordingAsync', 'UndoSamples', 'LooperLayerComposer.Compose') `
        'Gate 4 recording controller'

    Write-Host ''
    Write-Host 'GATE 4 LOCAL AUTOMATED VALIDATION: PASS' -ForegroundColor Green
    Write-Host 'Next: perform the real multi-layer One Cycle / Replace / Overdub checklist from ChatGPT.' -ForegroundColor Green

    if ($Run) {
        if (-not (Test-Path -LiteralPath $appExe)) { throw "Local test executable was not found at $appExe" }
        Start-Process -FilePath $appExe -WorkingDirectory (Split-Path $appExe -Parent)
    }
}
finally {
    Pop-Location
}
