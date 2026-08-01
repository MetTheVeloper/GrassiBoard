[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'DriverScript.Common.ps1')
Assert-GrassiBoardAdministrator

$status = & (Get-GrassiBoardPackageFile 'GrassiBoard.DeviceTool.exe') status 2>&1 | Out-String
if ($status -notmatch 'present=false') {
    throw 'The GrassiBoard device still appears to be installed. Run Uninstall-GrassiBoardDriver.ps1 first.'
}

& bcdedit.exe /set '{current}' testsigning off
if ($LASTEXITCODE -ne 0) {
    throw "BCDEdit failed with exit code $LASTEXITCODE."
}

Write-Host 'TESTSIGNING is disabled for the next boot. Reboot Windows manually to apply the change.'
