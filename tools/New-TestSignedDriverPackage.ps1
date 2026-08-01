[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BuildDirectory,
    [Parameter(Mandatory = $true)][string]$DeviceToolDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$WdkPackageDirectory,
    [Parameter(Mandatory = $true)][string]$SdkBuildToolsPackageDirectory,
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $output.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Driver package output must remain inside the repository.'
}

$sysSource = Get-ChildItem -LiteralPath $BuildDirectory -Filter 'GrassiBoardVirtualAudio.sys' -Recurse -File | Select-Object -First 1
$toolSource = Get-ChildItem -LiteralPath $DeviceToolDirectory -Filter 'GrassiBoard.DeviceTool.exe' -Recurse -File | Select-Object -First 1
$infSource = Join-Path $repositoryRoot 'src\GrassiBoard.Driver\GrassiBoardVirtualAudio.inf'
if (-not $sysSource -or -not $toolSource -or -not (Test-Path -LiteralPath $infSource)) {
    throw 'Driver, INF, or device tool build output is missing.'
}

$signTool = Get-ChildItem -LiteralPath (Join-Path $SdkBuildToolsPackageDirectory 'bin') -Filter 'signtool.exe' -Recurse -File |
    Where-Object { $_.FullName -match '[\\/]x64[\\/]' } | Select-Object -First 1
$inf2Cat = Get-ChildItem -LiteralPath (Join-Path $WdkPackageDirectory 'c\bin') -Filter 'inf2cat.exe' -Recurse -File |
    Where-Object { $_.FullName -match '[\\/]x86[\\/]' } | Select-Object -First 1
if (-not $signTool -or -not $inf2Cat) {
    throw 'The restored SDK/WDK NuGet packages do not contain x64 SignTool and x86 Inf2Cat.'
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
Get-ChildItem -LiteralPath $output -Force | Remove-Item -Recurse -Force

$sysPath = Join-Path $output 'GrassiBoardVirtualAudio.sys'
$infPath = Join-Path $output 'GrassiBoardVirtualAudio.inf'
$toolPath = Join-Path $output 'GrassiBoard.DeviceTool.exe'
Copy-Item -LiteralPath $sysSource.FullName -Destination $sysPath -Force
Copy-Item -LiteralPath $infSource -Destination $infPath -Force
Copy-Item -LiteralPath $toolSource.FullName -Destination $toolPath -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\GrassiBoard.Driver\DRIVER-TESTING.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\GrassiBoard.Driver\UPSTREAM.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\GrassiBoard.Driver\THIRD-PARTY-MS-PL.txt') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\GrassiBoard.Driver\scripts') -Destination $output -Recurse -Force

$certificate = $null
$temporaryRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }
$pfxPath = Join-Path $temporaryRoot "GrassiBoard-TestSigning-$([Guid]::NewGuid().ToString('N')).pfx"
$cerPath = Join-Path $output 'GrassiBoard-TestCertificate.cer'
$passwordText = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(36))
$password = ConvertTo-SecureString $passwordText -AsPlainText -Force

try {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=GrassiBoard Milestone 4 Test Driver' `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddDays(90)

    Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $password | Out-Null
    Export-Certificate -Cert $certificate -FilePath $cerPath -Type CERT | Out-Null

    & $signTool.FullName sign /v /fd SHA256 /f $pfxPath /p $passwordText $sysPath
    if ($LASTEXITCODE -ne 0) { throw "SignTool failed to sign SYS (exit $LASTEXITCODE)." }

    & $inf2Cat.FullName "/driver:$output" /os:10_X64
    if ($LASTEXITCODE -ne 0) { throw "Inf2Cat failed (exit $LASTEXITCODE)." }

    $catPath = Join-Path $output 'GrassiBoardVirtualAudio.cat'
    & $signTool.FullName sign /v /fd SHA256 /f $pfxPath /p $passwordText $catPath
    if ($LASTEXITCODE -ne 0) { throw "SignTool failed to sign CAT (exit $LASTEXITCODE)." }

    foreach ($file in $sysPath, $catPath) {
        $signature = Get-AuthenticodeSignature -LiteralPath $file
        $invalidStatuses = @('NotSigned', 'HashMismatch', 'NotSupportedError')
        if (-not $signature.SignerCertificate -or
            $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint -or
            $signature.Status.ToString() -in $invalidStatuses) {
            throw "Packaged signature verification failed for $(Split-Path $file -Leaf)."
        }
    }

    $files = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Name -ne 'manifest.json' }
    $manifest = [ordered]@{
        Product = 'GrassiBoard Virtual Audio'
        Version = $Version
        Architecture = 'x64'
        HardwareId = 'ROOT\GrassiBoardVirtualAudio'
        RenderEndpoint = 'GrassiBoard Virtual Cable Input'
        CaptureEndpoint = 'GrassiBoard Virtual Microphone'
        CertificateThumbprint = $certificate.Thumbprint
        CertificateNotAfterUtc = $certificate.NotAfter.ToUniversalTime().ToString('o')
        SysvadCommit = 'ef7c3074748ab05726c3a9161d3256118efd76e2'
        Files = @($files | Sort-Object FullName | ForEach-Object {
            [ordered]@{
                Path = $_.FullName.Substring($output.Length).TrimStart('\').Replace('\', '/')
                Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                Size = $_.Length
            }
        })
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
}
finally {
    if ($certificate) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $pfxPath) { Remove-Item -LiteralPath $pfxPath -Force }
    $passwordText = $null
    $password = $null
}

$privateMaterial = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Extension -in '.pfx', '.pvk', '.key' }
if ($privateMaterial) { throw 'Private key material leaked into the driver package.' }
$requiredFiles = @(
    'GrassiBoardVirtualAudio.sys',
    'GrassiBoardVirtualAudio.inf',
    'GrassiBoardVirtualAudio.cat',
    'GrassiBoard-TestCertificate.cer',
    'GrassiBoard.DeviceTool.exe',
    'DRIVER-TESTING.md',
    'manifest.json',
    'scripts\Install-GrassiBoardDriver.ps1',
    'scripts\Uninstall-GrassiBoardDriver.ps1',
    'scripts\Collect-DriverDiagnostics.ps1'
)
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $relativePath) -PathType Leaf)) {
        throw "Driver package is missing $relativePath"
    }
}
Write-Host "Created test-signed driver package at $output"
