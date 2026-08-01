[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'DriverScript.Common.ps1')
Assert-GrassiBoardAdministrator

$state = $null
if (Test-Path -LiteralPath $script:GrassiBoardStatePath) {
    $state = Get-Content -LiteralPath $script:GrassiBoardStatePath -Raw | ConvertFrom-Json
}

$signedDriver = Get-CimInstance Win32_PnPSignedDriver |
    Where-Object { $_.DeviceID -like 'ROOT\GRASSIBOARDVIRTUALAUDIO*' } |
    Select-Object -First 1
$infName = if ($signedDriver.InfName) { $signedDriver.InfName } elseif ($state) { $state.InfName } else { $null }

$exitCode = Invoke-GrassiBoardDeviceTool @('remove')
$packageRebootRequired = $false

if ($infName -and $infName -match '^oem\d+\.inf$') {
    & pnputil.exe /delete-driver $infName /uninstall
    if ($LASTEXITCODE -notin 0, 3010) {
        throw "PnPUtil could not delete $infName (exit code $LASTEXITCODE). The device was removed; delete the package manually from an elevated prompt."
    }
    $packageRebootRequired = $LASTEXITCODE -eq 3010
}
else {
    Write-Warning 'The OEM INF name was not found. The device is removed, but its package may remain in Driver Store.'
}

$thumbprint = if ($state) { $state.CertificateThumbprint } else { ([Security.Cryptography.X509Certificates.X509Certificate2]::new((Get-GrassiBoardPackageFile 'GrassiBoard-TestCertificate.cer'))).Thumbprint }
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

Write-Host 'GrassiBoard virtual-audio device and its trusted test certificate were removed.'
Write-Host 'TESTSIGNING was not changed. Run Disable-TestSigning.ps1 only after confirming the endpoints are gone.'
if ($exitCode -eq 10 -or $packageRebootRequired) { Write-Warning 'Windows requested a reboot to finish removal.' }
