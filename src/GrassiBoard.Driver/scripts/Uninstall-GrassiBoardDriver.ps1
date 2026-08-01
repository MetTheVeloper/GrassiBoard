[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'DriverScript.Common.ps1')
Assert-GrassiBoardAdministrator

$state = $null
if (Test-Path -LiteralPath $script:GrassiBoardStatePath) {
    $state = Get-Content -LiteralPath $script:GrassiBoardStatePath -Raw | ConvertFrom-Json
}

$device = Get-GrassiBoardPnpDevice
$signedDriver = Get-GrassiBoardSignedDriver -Device $device
$installedSigner = Get-GrassiBoardInstalledSignerCertificate
$infName = if ($signedDriver -and $signedDriver.InfName) { $signedDriver.InfName } elseif ($state -and $state.InfName) { $state.InfName } else { $null }
if ($infName -notmatch '^oem\d+\.inf$') {
    throw 'The installed OEM INF identity could not be determined. No removal was attempted; run Collect-DriverDiagnostics.ps1.'
}
$packageCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Get-GrassiBoardPackageFile 'GrassiBoard-TestCertificate.cer'))
if ($packageCertificate.Subject -ne $script:GrassiBoardCertificateSubject -or $packageCertificate.HasPrivateKey) {
    throw 'The packaged test certificate identity or key material is invalid.'
}
$thumbprint = if ($installedSigner) {
    $installedSigner.Thumbprint
} elseif ($state -and $state.CertificateThumbprint) {
    [string]$state.CertificateThumbprint
} else {
    $packageCertificate.Thumbprint
}
if ($thumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'The installed certificate thumbprint is invalid.' }

$exitCode = Invoke-GrassiBoardDeviceTool @('remove')
$packageRebootRequired = $false

& pnputil.exe /delete-driver $infName /uninstall
if ($LASTEXITCODE -notin 0, 3010) {
    throw "PnPUtil could not delete $infName (exit code $LASTEXITCODE). The device was removed; delete the package manually from an elevated prompt."
}
$packageRebootRequired = $LASTEXITCODE -eq 3010

foreach ($store in 'Root', 'TrustedPublisher') {
    $path = "Cert:\LocalMachine\$store\$thumbprint"
    if (Test-Path -LiteralPath $path) {
        $installedCertificate = Get-Item -LiteralPath $path
        if ($installedCertificate.Subject -eq $script:GrassiBoardCertificateSubject) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

if (Test-Path -LiteralPath $script:GrassiBoardStatePath) {
    Remove-Item -LiteralPath $script:GrassiBoardStatePath -Force
}

$deadline = [DateTime]::UtcNow.AddSeconds(20)
do {
    Start-Sleep -Milliseconds 500
    $remainingDevice = Get-GrassiBoardPnpDevice
    $remainingEndpoints = @(Get-GrassiBoardEndpointDevices)
} until ((-not $remainingDevice -and $remainingEndpoints.Count -eq 0) -or [DateTime]::UtcNow -ge $deadline)

if (($remainingDevice -or $remainingEndpoints.Count -ne 0) -and $exitCode -ne 10 -and -not $packageRebootRequired) {
    throw 'Removal did not fully settle: a GrassiBoard device or endpoint is still present. Run Collect-DriverDiagnostics.ps1.'
}

Write-Host 'GrassiBoard virtual-audio device and its trusted test certificate were removed.'
Write-Host "Removed endpoints: $(2 - $remainingEndpoints.Count) of 2."
Write-Host 'TESTSIGNING was not changed. Run Disable-TestSigning.ps1 only after confirming the endpoints are gone.'
if ($exitCode -eq 10 -or $packageRebootRequired) { Write-Warning 'Windows requested a reboot to finish removal.' }
