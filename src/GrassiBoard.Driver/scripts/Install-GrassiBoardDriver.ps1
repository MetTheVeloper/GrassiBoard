[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'DriverScript.Common.ps1')
Assert-GrassiBoardAdministrator

$bcd = (& bcdedit.exe /enum '{current}' 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $bcd -notmatch '(?im)^\s*testsigning\s+Yes\s*$') {
    throw 'Windows is not booted in test-signing mode. Run Enable-TestSigning.ps1, reboot, and try again.'
}

$infPath = Get-GrassiBoardPackageFile 'GrassiBoardVirtualAudio.inf'
$sysPath = Get-GrassiBoardPackageFile 'GrassiBoardVirtualAudio.sys'
$catPath = Get-GrassiBoardPackageFile 'GrassiBoardVirtualAudio.cat'
$cerPath = Get-GrassiBoardPackageFile 'GrassiBoard-TestCertificate.cer'

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($cerPath)
if ($certificate.Subject -ne $script:GrassiBoardCertificateSubject -or $certificate.HasPrivateKey) {
    throw 'The test certificate identity or key material is invalid.'
}

Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null

foreach ($signedFile in $sysPath, $catPath) {
    $signature = Get-AuthenticodeSignature -LiteralPath $signedFile
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
        throw "Signature validation failed for $(Split-Path $signedFile -Leaf): $($signature.Status)"
    }
}

$existing = & (Get-GrassiBoardPackageFile 'GrassiBoard.DeviceTool.exe') status 2>&1 | Out-String
if ($existing -notmatch 'present=false') {
    throw 'A GrassiBoard virtual-audio device already exists. Uninstall it before reinstalling.'
}

$exitCode = Invoke-GrassiBoardDeviceTool @('install', $infPath)

$deadline = [DateTime]::UtcNow.AddSeconds(20)
do {
    Start-Sleep -Milliseconds 500
    $device = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -like 'ROOT\GRASSIBOARDVIRTUALAUDIO*' } |
        Select-Object -First 1
} until ($device -or [DateTime]::UtcNow -ge $deadline)

if (-not $device -or $device.Status -ne 'OK') {
    throw 'The driver was installed but Device Manager does not report an OK device. Run Collect-DriverDiagnostics.ps1, then uninstall.'
}

$signedDriver = Get-CimInstance Win32_PnPSignedDriver |
    Where-Object { $_.DeviceID -like 'ROOT\GRASSIBOARDVIRTUALAUDIO*' } |
    Select-Object -First 1

New-Item -ItemType Directory -Path $script:GrassiBoardStateDirectory -Force | Out-Null
@{
    HardwareId = $script:GrassiBoardHardwareId
    CertificateThumbprint = $certificate.Thumbprint
    InfName = $signedDriver.InfName
    InstalledUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath $script:GrassiBoardStatePath -Encoding UTF8

$endpoints = Get-PnpDevice -Class AudioEndpoint -PresentOnly -ErrorAction SilentlyContinue |
    Where-Object { $_.FriendlyName -like '*GrassiBoard Virtual Cable Input*' -or $_.FriendlyName -like '*GrassiBoard Virtual Microphone*' }

Write-Host "Driver installed. Device Manager status: $($device.Status)."
Write-Host "Detected GrassiBoard endpoints: $(@($endpoints).Count) of 2."
if ($exitCode -eq 10) { Write-Warning 'Windows requested a reboot to finish installation.' }
if (@($endpoints).Count -ne 2) { Write-Warning 'Endpoint discovery is still settling. Check Sound settings after a few seconds.' }
