Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:GrassiBoardHardwareId = 'ROOT\GrassiBoardVirtualAudio'
$script:GrassiBoardCertificateSubject = 'CN=GrassiBoard Milestone 4 Test Driver'
$script:GrassiBoardStateDirectory = Join-Path $env:ProgramData 'GrassiBoard'
$script:GrassiBoardStatePath = Join-Path $script:GrassiBoardStateDirectory 'driver-state.json'

function Assert-GrassiBoardAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell window (Run as administrator).'
    }
}

function Get-GrassiBoardPackageFile {
    param([Parameter(Mandatory = $true)][string]$Name)
    $path = Join-Path (Split-Path $PSScriptRoot -Parent) $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Driver package is incomplete; missing $Name"
    }
    return (Resolve-Path -LiteralPath $path).Path
}

function Invoke-GrassiBoardDeviceTool {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $tool = Get-GrassiBoardPackageFile 'GrassiBoard.DeviceTool.exe'
    $process = Start-Process -FilePath $tool -ArgumentList $Arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -notin 0, 10) {
        throw "GrassiBoard.DeviceTool failed with exit code $($process.ExitCode)."
    }
    return $process.ExitCode
}
