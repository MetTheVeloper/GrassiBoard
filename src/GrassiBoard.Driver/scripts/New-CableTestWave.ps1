[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'GrassiBoard-cable-test.wav')
)

$ErrorActionPreference = 'Stop'
$sampleRate = 48000
$channels = 2
$bitsPerSample = 16
$blockAlign = $channels * ($bitsPerSample / 8)
$segments = @(
    @{ Frequency = 440.0; Seconds = 1.0 },
    @{ Frequency = 0.0; Seconds = 0.5 },
    @{ Frequency = 660.0; Seconds = 1.0 },
    @{ Frequency = 0.0; Seconds = 0.5 },
    @{ Frequency = 880.0; Seconds = 1.0 },
    @{ Frequency = 0.0; Seconds = 2.0 }
)
$totalSeconds = 0.0
foreach ($segment in $segments) { $totalSeconds += $segment.Seconds }
$totalFrames = [int]($totalSeconds * $sampleRate)
$dataBytes = $totalFrames * $blockAlign
$directory = Split-Path -Parent $OutputPath
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
    $writer.Write([int](36 + $dataBytes))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVE'))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('fmt '))
    $writer.Write([int]16)
    $writer.Write([int16]1)
    $writer.Write([int16]$channels)
    $writer.Write([int]$sampleRate)
    $writer.Write([int]($sampleRate * $blockAlign))
    $writer.Write([int16]$blockAlign)
    $writer.Write([int16]$bitsPerSample)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
    $writer.Write([int]$dataBytes)

    foreach ($segment in $segments) {
        $frames = [int]($segment.Seconds * $sampleRate)
        $fadeFrames = [Math]::Min([int](0.01 * $sampleRate), [int]($frames / 2))
        for ($frame = 0; $frame -lt $frames; $frame++) {
            $gain = 0.0
            if ($segment.Frequency -gt 0) {
                $fade = [Math]::Min(1.0, [Math]::Min(($frame + 1) / $fadeFrames, ($frames - $frame) / $fadeFrames))
                $gain = 0.35 * $fade
            }
            $sample = [int16]([Math]::Round(
                [Math]::Sin(2.0 * [Math]::PI * $segment.Frequency * $frame / $sampleRate) *
                $gain * [int16]::MaxValue))
            $writer.Write($sample)
            $writer.Write($sample)
        }
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "Created $OutputPath (48 kHz, 16-bit, stereo; 440/660/880 Hz with silence)."
