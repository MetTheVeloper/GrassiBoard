[CmdletBinding()]
param(
    [string]$TargetDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\GrassiBoard'),
    [switch]$RestoreWeb,
    [switch]$NoStopApp
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$remoteWebRoot = Join-Path $repositoryRoot 'src\GrassiBoard.RemoteWeb'
$output = Join-Path $remoteWebRoot '.output\public'
$target = [IO.Path]::GetFullPath($TargetDirectory)
$targetRemote = Join-Path $target 'RemoteWeb'

function Stop-GrassiBoardForDeploy {
    $running = @(Get-Process GrassiBoard -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) { return }

    $pids = ($running | ForEach-Object { $_.Id }) -join ', '
    Write-Host "Stopping GrassiBoard before local RemoteWeb deploy (PID: $pids)..." -ForegroundColor Yellow
    $running | Stop-Process -Force

    foreach ($process in $running) {
        try { Wait-Process -Id $process.Id -Timeout 5 -ErrorAction Stop }
        catch { }
    }

    $stillRunning = @(Get-Process GrassiBoard -ErrorAction SilentlyContinue)
    if ($stillRunning.Count -gt 0) {
        $remaining = ($stillRunning | ForEach-Object { $_.Id }) -join ', '
        throw "Unable to stop GrassiBoard.exe (PID: $remaining). Close it manually and rerun the script."
    }
}

if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) { throw "Required command 'pnpm' was not found in PATH." }
if (-not (Test-Path -LiteralPath (Join-Path $target 'GrassiBoard.exe'))) {
    throw "GrassiBoard.exe was not found in target directory: $target"
}

if (-not $NoStopApp) {
    Stop-GrassiBoardForDeploy
}

Push-Location $remoteWebRoot
try {
    if ($RestoreWeb -or -not (Test-Path -LiteralPath (Join-Path $remoteWebRoot 'node_modules'))) {
        & pnpm install --no-frozen-lockfile
        if ($LASTEXITCODE -ne 0) { throw 'pnpm install failed.' }
    }
    & pnpm generate
    if ($LASTEXITCODE -ne 0) { throw 'pnpm generate failed.' }
}
finally { Pop-Location }

if (-not (Test-Path -LiteralPath (Join-Path $output 'index.html'))) { throw 'Nuxt output is missing index.html.' }

# Guard against auto-relaunch or a second instance appearing while the web build was running.
if (-not $NoStopApp) {
    Stop-GrassiBoardForDeploy
}
else {
    $running = @(Get-Process GrassiBoard -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        $pids = ($running | ForEach-Object { $_.Id }) -join ', '
        throw "GrassiBoard.exe is still running (PID: $pids). Stop it before deploying RemoteWeb, or rerun without -NoStopApp."
    }
}

if (Test-Path -LiteralPath $targetRemote) { Remove-Item -LiteralPath $targetRemote -Recurse -Force }
Copy-Item -LiteralPath $output -Destination $targetRemote -Recurse -Force
Write-Host "RemoteWeb updated in installed GrassiBoard: $targetRemote" -ForegroundColor Green
Write-Host 'Restart GrassiBoard and test the phone Remote. No .NET/native rebuild was performed.' -ForegroundColor DarkGray
