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

function Get-GrassiBoardPnpDevice {
    Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { @($_.HardwareID) -icontains $script:GrassiBoardHardwareId } |
        Select-Object -First 1
}

function Get-GrassiBoardSignedDriver {
    param([object]$Device = (Get-GrassiBoardPnpDevice))

    if (-not $Device) { return }
    $instanceId = [string]$Device.InstanceId
    Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue |
        Where-Object { $_.DeviceID -ieq $instanceId } |
        Select-Object -First 1
}

function Get-GrassiBoardEndpointDevices {
    Get-PnpDevice -Class AudioEndpoint -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FriendlyName -like '*GrassiBoard Virtual Audio*' -or
            $_.FriendlyName -like '*GrassiBoard Virtual Cable Input*' -or
            $_.FriendlyName -like '*GrassiBoard Virtual Microphone*'
        }
}

function Get-GrassiBoardInstalledSignerCertificate {
    $service = Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\GrassiBoardVirtualAudio' -ErrorAction SilentlyContinue
    if (-not $service -or [string]::IsNullOrWhiteSpace([string]$service.ImagePath)) { return }

    $imagePath = [Environment]::ExpandEnvironmentVariables([string]$service.ImagePath).Trim([char]0x22)
    $systemRootPrefix = '\SystemRoot\'
    $ntPathPrefix = '\??\'
    if ($imagePath.StartsWith($systemRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $imagePath = Join-Path $env:SystemRoot $imagePath.Substring($systemRootPrefix.Length)
    }
    elseif ($imagePath.StartsWith('System32\', [StringComparison]::OrdinalIgnoreCase)) {
        $imagePath = Join-Path $env:SystemRoot $imagePath
    }
    elseif ($imagePath.StartsWith($ntPathPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $imagePath = $imagePath.Substring($ntPathPrefix.Length)
    }

    if (-not (Test-Path -LiteralPath $imagePath -PathType Leaf)) { return }
    $signature = Get-AuthenticodeSignature -LiteralPath $imagePath
    if ($signature.SignerCertificate -and $signature.SignerCertificate.Subject -eq $script:GrassiBoardCertificateSubject) {
        return $signature.SignerCertificate
    }
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
