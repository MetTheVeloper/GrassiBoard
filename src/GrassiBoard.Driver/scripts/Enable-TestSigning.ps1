[CmdletBinding()]
param(
    [switch]$AcknowledgeBitLockerRecoveryRisk
)

. (Join-Path $PSScriptRoot 'DriverScript.Common.ps1')
Assert-GrassiBoardAdministrator

try {
    if (Confirm-SecureBootUEFI) {
        throw 'Secure Boot is enabled. Windows will reject changing TESTSIGNING. Disable Secure Boot in firmware only if you understand the security impact.'
    }
}
catch {
    if ($_.Exception.Message -like 'Secure Boot is enabled*') { throw }
    Write-Warning "Secure Boot state could not be read: $($_.Exception.Message)"
}

$bitLocker = if (Get-Command Get-BitLockerVolume -ErrorAction SilentlyContinue) {
    Get-BitLockerVolume -MountPoint $env:SystemDrive -ErrorAction SilentlyContinue
} else { $null }
if ($bitLocker -and $bitLocker.ProtectionStatus -eq 'On' -and -not $AcknowledgeBitLockerRecoveryRisk) {
    throw 'BitLocker protection is on. Save the recovery key, then rerun with -AcknowledgeBitLockerRecoveryRisk. This script never suspends BitLocker.'
}

& bcdedit.exe /set '{current}' testsigning on
if ($LASTEXITCODE -ne 0) {
    throw "BCDEdit failed with exit code $LASTEXITCODE."
}

Write-Host 'TESTSIGNING is enabled for the next boot. Reboot Windows manually, then run Install-GrassiBoardDriver.ps1.'
Write-Host 'This script did not reboot, disable Secure Boot, or change BitLocker protection.'
