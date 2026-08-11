[CmdletBinding()]
param(
    [string]$NativeDll = '',
    [switch]$RestoreWeb,
    [switch]$RebuildNative,
    [switch]$RunSmokeTests,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$remoteWebRoot = Join-Path $repositoryRoot 'src\GrassiBoard.RemoteWeb'
$appProject = Join-Path $repositoryRoot 'src\GrassiBoard.App\GrassiBoard.App.csproj'
$buildInfoPath = Join-Path $repositoryRoot 'src\GrassiBoard.App\BuildInfo.json'
$localRoot = Join-Path $repositoryRoot 'artifacts\local-test'
$publishRoot = Join-Path $localRoot 'GrassiBoard'
$archivePath = Join-Path $localRoot 'GrassiBoard-local-test.zip'
$nativeBuildPath = Join-Path $repositoryRoot 'out\build\windows-x64-release\src\GrassiBoard.AudioEngine\Release\GrassiBoard.AudioEngine.dll'
$installedNativePath = Join-Path $env:LOCALAPPDATA 'Programs\GrassiBoard\GrassiBoard.AudioEngine.dll'

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Ensure-RequiredDotNetSdk {
    $globalJsonPath = Join-Path $repositoryRoot 'global.json'
    if (-not (Test-Path -LiteralPath $globalJsonPath)) {
        throw "global.json was not found at $globalJsonPath"
    }

    $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
    $requiredVersion = [string]$globalJson.sdk.version
    if ([string]::IsNullOrWhiteSpace($requiredVersion)) {
        throw 'global.json does not define sdk.version.'
    }

    function Test-DotNetSdk([string]$DotNetExe) {
        if (-not (Test-Path -LiteralPath $DotNetExe) -and $DotNetExe -ne 'dotnet') { return $false }
        try {
            $versions = @(& $DotNetExe --list-sdks 2>$null)
            if ($LASTEXITCODE -ne 0) { return $false }
            return @($versions | Where-Object { $_ -match ('^' + [Regex]::Escape($requiredVersion) + '\s') }).Count -gt 0
        }
        catch { return $false }
    }

    $systemDotNet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($systemDotNet -and (Test-DotNetSdk 'dotnet')) {
        Write-Host "Using installed .NET SDK $requiredVersion." -ForegroundColor DarkGray
        return
    }

    $localSdkRoot = Join-Path $repositoryRoot 'artifacts\local-sdk\dotnet'
    $localDotNetExe = Join-Path $localSdkRoot 'dotnet.exe'
    if (Test-DotNetSdk $localDotNetExe) {
        $env:DOTNET_ROOT = $localSdkRoot
        $env:PATH = "$localSdkRoot;$env:PATH"
        Write-Host "Using project-local .NET SDK $requiredVersion from $localSdkRoot" -ForegroundColor DarkGray
        return
    }

    Write-Host ".NET SDK $requiredVersion was not found. Bootstrapping a project-local SDK (one-time download)..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $localSdkRoot -Force | Out-Null
    $installerDir = Join-Path $repositoryRoot 'artifacts\local-sdk'
    New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
    $installerPath = Join-Path $installerDir 'dotnet-install.ps1'

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installerPath
    }
    catch {
        throw "Unable to download Microsoft's dotnet-install.ps1. Install .NET SDK $requiredVersion manually or make https://dot.net reachable, then rerun this script. $($_.Exception.Message)"
    }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installerPath `
        -Version $requiredVersion `
        -InstallDir $localSdkRoot `
        -Architecture x64 `
        -NoPath
    if ($LASTEXITCODE -ne 0) {
        throw "Project-local .NET SDK $requiredVersion installation failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-DotNetSdk $localDotNetExe)) {
        throw "dotnet-install completed, but required SDK $requiredVersion was not found in $localSdkRoot."
    }

    $env:DOTNET_ROOT = $localSdkRoot
    $env:PATH = "$localSdkRoot;$env:PATH"
    Write-Host "Project-local .NET SDK $requiredVersion is ready." -ForegroundColor Green
}

function Get-RunningGrassiBoardProcesses {
    return @(Get-Process -Name 'GrassiBoard' -ErrorAction SilentlyContinue)
}

function Wait-ForGrassiBoardExit([int]$TimeoutSeconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if ((Get-RunningGrassiBoardProcesses).Count -eq 0) { return $true }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    return (Get-RunningGrassiBoardProcesses).Count -eq 0
}

function Stop-GrassiBoardForLocalBuild {
    $running = Get-RunningGrassiBoardProcesses
    if ($running.Count -eq 0) { return }

    $pids = ($running | ForEach-Object { $_.Id }) -join ', '
    Write-Host "Stopping GrassiBoard before local test build (PID: $pids)..." -ForegroundColor Yellow

    # First try the ordinary PowerShell force-stop. This is enough once the
    # fixed local backend is in use, but older installed test builds may hang
    # during Remote WebSocket shutdown.
    foreach ($process in $running) {
        try { Stop-Process -Id $process.Id -Force -ErrorAction Stop }
        catch {
            Write-Host "Stop-Process could not terminate PID $($process.Id): $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    }
    if (Wait-ForGrassiBoardExit 3) { return }

    # taskkill /T also terminates any child process tree and is more reliable
    # for a half-shutdown WPF process.
    $remaining = Get-RunningGrassiBoardProcesses
    $remainingPids = ($remaining | ForEach-Object { $_.Id }) -join ', '
    Write-Host "GrassiBoard is still alive (PID: $remainingPids). Trying taskkill /F /T..." -ForegroundColor Yellow
    foreach ($process in $remaining) {
        try {
            & "$env:SystemRoot\System32\taskkill.exe" /F /T /PID $process.Id 2>$null | Out-Null
        }
        catch { }
    }
    if (Wait-ForGrassiBoardExit 3) { return }

    # If the installed app was elevated, a normal shell cannot terminate it.
    # Ask for elevation only for the kill operation, rather than requiring the
    # whole local development shell to run as Administrator.
    $remaining = Get-RunningGrassiBoardProcesses
    $remainingPids = ($remaining | ForEach-Object { $_.Id }) -join ', '
    Write-Host "GrassiBoard still cannot be terminated (PID: $remainingPids). Requesting Administrator permission for taskkill..." -ForegroundColor Yellow
    try {
        $arguments = @('/F', '/T')
        foreach ($process in $remaining) {
            $arguments += @('/PID', [string]$process.Id)
        }
        $elevatedKill = Start-Process -FilePath "$env:SystemRoot\System32\taskkill.exe" `
            -ArgumentList $arguments `
            -Verb RunAs `
            -Wait `
            -PassThru
        if ($elevatedKill.ExitCode -ne 0) {
            Write-Host "Elevated taskkill returned exit code $($elevatedKill.ExitCode)." -ForegroundColor DarkYellow
        }
    }
    catch {
        throw "GrassiBoard is still running and the elevated kill was cancelled or failed. Close it in Task Manager, then rerun the script. $($_.Exception.Message)"
    }

    if (-not (Wait-ForGrassiBoardExit 5)) {
        $remaining = Get-RunningGrassiBoardProcesses
        $details = ($remaining | ForEach-Object {
            $path = try { $_.Path } catch { '<unknown path>' }
            "PID=$($_.Id), Path=$path"
        }) -join '; '
        throw "Unable to stop GrassiBoard.exe even after elevated taskkill. Remaining process: $details. Restart Windows once, then rerun the script."
    }

    Write-Host 'GrassiBoard was terminated successfully.' -ForegroundColor Green
}

function Resolve-NativeDll {
    if ($NativeDll) {
        $candidate = [IO.Path]::GetFullPath($NativeDll)
        if (-not (Test-Path -LiteralPath $candidate)) { throw "Native DLL not found: $candidate" }
        return $candidate
    }

    if (-not $RebuildNative -and (Test-Path -LiteralPath $nativeBuildPath)) {
        return $nativeBuildPath
    }

    if (-not $RebuildNative -and (Test-Path -LiteralPath $installedNativePath)) {
        Write-Host "Reusing installed ABI-8 native engine: $installedNativePath" -ForegroundColor DarkGray
        return $installedNativePath
    }

    Require-Command 'cmake'
    Write-Host 'Building native engine because no reusable DLL was found (or -RebuildNative was requested)...' -ForegroundColor Cyan
    Push-Location $repositoryRoot
    try {
        & cmake --preset windows-x64-release
        if ($LASTEXITCODE -ne 0) { throw 'cmake configure failed.' }
        & cmake --build --preset windows-x64-release -- /m
        if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
    }
    finally { Pop-Location }

    if (-not (Test-Path -LiteralPath $nativeBuildPath)) {
        throw "Native build completed but DLL was not found at $nativeBuildPath"
    }
    return $nativeBuildPath
}

Ensure-RequiredDotNetSdk
Require-Command 'pnpm'
Stop-GrassiBoardForLocalBuild

Write-Host 'Generating Nuxt Remote SPA...' -ForegroundColor Cyan
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

$remoteIndex = Join-Path $remoteWebRoot '.output\public\index.html'
if (-not (Test-Path -LiteralPath $remoteIndex)) { throw 'Nuxt output index.html is missing.' }

$nativeSource = Resolve-NativeDll

if ($RunSmokeTests) {
    Write-Host 'Running managed smoke tests...' -ForegroundColor Cyan
    Push-Location $repositoryRoot
    try {
        & dotnet run --project .\tests\GrassiBoard.App.SmokeTests\GrassiBoard.App.SmokeTests.csproj --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Managed smoke tests failed.' }
    }
    finally { Pop-Location }
}

$originalBuildInfo = if (Test-Path -LiteralPath $buildInfoPath) { [IO.File]::ReadAllText($buildInfoPath) } else { $null }
try {
    $commit = 'local'
    if (Get-Command git -ErrorAction SilentlyContinue) {
        Push-Location $repositoryRoot
        try {
            $gitCommit = (& git rev-parse HEAD 2>$null)
            if ($LASTEXITCODE -eq 0 -and $gitCommit) { $commit = $gitCommit.Trim() }
        }
        finally { Pop-Location }
    }
    $dotnetVersion = (& dotnet --version).Trim()
    & (Join-Path $repositoryRoot 'tools\New-BuildInfo.ps1') `
        -Version '1.1.0' `
        -CommitSha $commit `
        -RunNumber 'local' `
        -OutputPath $buildInfoPath `
        -Configuration 'Release-LocalTest' `
        -TargetArchitecture 'x64' `
        -WdkVersion 'external-cable' `
        -SdkVersion 'local-windows' `
        -DotNetVersion $dotnetVersion

    if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

    Write-Host 'Publishing fast framework-dependent WPF test build...' -ForegroundColor Cyan
    Push-Location $repositoryRoot
    try {
        & dotnet publish $appProject `
            --configuration Release `
            --self-contained false `
            --output $publishRoot `
            -p:Platform=x64
        if ($LASTEXITCODE -ne 0) { throw 'Local WPF publish failed.' }
    }
    finally { Pop-Location }
}
finally {
    if ($null -ne $originalBuildInfo) { [IO.File]::WriteAllText($buildInfoPath, $originalBuildInfo) }
}

Copy-Item -LiteralPath $nativeSource -Destination (Join-Path $publishRoot 'GrassiBoard.AudioEngine.dll') -Force

if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'RemoteWeb\index.html'))) {
    throw 'Local publish is missing RemoteWeb/index.html.'
}
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'GrassiBoard.AudioEngine.dll'))) {
    throw 'Local publish is missing GrassiBoard.AudioEngine.dll.'
}

if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $archivePath -CompressionLevel Fastest

$exe = Join-Path $publishRoot 'GrassiBoard.exe'
Write-Host ''
Write-Host "Local test build ready: $exe" -ForegroundColor Green
Write-Host "ZIP: $archivePath" -ForegroundColor Green
Write-Host 'This local test build is framework-dependent for speed; GitHub Actions remains the authoritative self-contained release build.' -ForegroundColor DarkGray

if ($Run) { Start-Process -FilePath $exe }
