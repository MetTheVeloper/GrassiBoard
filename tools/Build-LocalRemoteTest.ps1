[CmdletBinding()]
param(
    [string]$NativeDll = '',
    [switch]$RestoreWeb,
    [switch]$RebuildNative,
    [switch]$RunSmokeTests,
    [switch]$RemoteMonitorSpike,
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
$nativeSpikeBuildPath = Join-Path $repositoryRoot 'out\build\windows-x64-remote-monitor-spike\src\GrassiBoard.AudioEngine\Release\GrassiBoard.AudioEngine.dll'
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



function Resolve-CMakeTool([string]$ToolName) {
    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    # Visual Studio's bundled CMake is not guaranteed to be on PATH.
    # Resolve the actual VS/Build Tools installation first so custom install
    # locations and BuildTools installations are handled reliably.
    $vswhereCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($vswhere in $vswhereCandidates) {
        try {
            $matches = @(
                & $vswhere `
                    -products * `
                    -latest `
                    -requires Microsoft.VisualStudio.Component.VC.CMake.Project `
                    -find "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\$ToolName.exe" 2>$null
            )

            foreach ($match in $matches) {
                $candidate = "$match".Trim()
                if ($candidate -and (Test-Path -LiteralPath $candidate)) {
                    return $candidate
                }
            }

            $installPath = (
                & $vswhere `
                    -products * `
                    -latest `
                    -requires Microsoft.VisualStudio.Component.VC.CMake.Project `
                    -property installationPath 2>$null |
                Select-Object -First 1
            )

            if ($installPath) {
                $candidate = Join-Path "$installPath".Trim() "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\$ToolName.exe"
                if (Test-Path -LiteralPath $candidate) { return $candidate }
            }
        }
        catch {
            # Fall through to explicit well-known-path probing below.
        }
    }

    $programRoots = @(
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        $env:ProgramW6432
    ) | Where-Object { $_ } | Select-Object -Unique

    $editions = @('BuildTools', 'Community', 'Professional', 'Enterprise')
    foreach ($programRoot in $programRoots) {
        foreach ($edition in $editions) {
            $candidate = Join-Path $programRoot "Microsoft Visual Studio\2022\$edition\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\$ToolName.exe"
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }

    return $null
}

function Ensure-NativeSubmodules {
    $linearCmake = Join-Path $repositoryRoot 'third_party\signalsmith-linear\CMakeLists.txt'
    $stretchCmake = Join-Path $repositoryRoot 'third_party\signalsmith-stretch\CMakeLists.txt'
    if ((Test-Path -LiteralPath $linearCmake) -and (Test-Path -LiteralPath $stretchCmake)) { return }

    Require-Command 'git'
    Write-Host 'Native DSP submodules are missing. Initializing git submodules (one-time download)...' -ForegroundColor Yellow
    Push-Location $repositoryRoot
    try {
        & git submodule update --init --recursive | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw 'git submodule update failed. Make GitHub reachable, then rerun the spike build.'
        }
    }
    finally { Pop-Location }

    if (-not (Test-Path -LiteralPath $linearCmake) -or -not (Test-Path -LiteralPath $stretchCmake)) {
        throw 'Native DSP submodules are still incomplete after git submodule update.'
    }
}

function Resolve-NativeDll {
    if ($NativeDll) {
        $candidate = [IO.Path]::GetFullPath($NativeDll)
        if (-not (Test-Path -LiteralPath $candidate)) { throw "Native DLL not found: $candidate" }
        return $candidate
    }

    $cmakeExe = Resolve-CMakeTool 'cmake'
    if (-not $cmakeExe) {
        throw 'GrassiBoard v1.3 Gate 2 requires the ABI-10 native build, but CMake was not found. Install Visual Studio 2022 Build Tools with Desktop development with C++ (including CMake tools), then rerun this command.'
    }

    Ensure-NativeSubmodules

    $preset = if ($RemoteMonitorSpike) { 'windows-x64-remote-monitor-spike' } else { 'windows-x64-release' }
    $expectedPath = if ($RemoteMonitorSpike) { $nativeSpikeBuildPath } else { $nativeBuildPath }

    Write-Host "Building GrassiBoard v1.3 Gate 2 native ABI-10 engine with preset '$preset'..." -ForegroundColor Cyan
    Push-Location $repositoryRoot
    try {
        & $cmakeExe --preset $preset | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Native v1.3 Gate 2 configure failed for preset '$preset'."
        }
        & $cmakeExe --build --preset $preset -- /m | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Native v1.3 Gate 2 build failed for preset '$preset'."
        }
    }
    finally { Pop-Location }

    if (-not (Test-Path -LiteralPath $expectedPath)) {
        throw "Native v1.3 Gate 2 build completed but DLL was not found at $expectedPath"
    }
    return $expectedPath
}

Ensure-RequiredDotNetSdk
Require-Command 'pnpm'
Stop-GrassiBoardForLocalBuild

if ($RemoteMonitorSpike) {
    Write-Host '-RemoteMonitorSpike remains a compatibility/diagnostic alias. Gate 2 keeps the accepted Remote Monitor path enabled by default.' -ForegroundColor DarkYellow
}
Write-Host 'GrassiBoard v1.3 Gate 2 development path is ENABLED (ABI 10 + Remote Phone Mic).' -ForegroundColor Green

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
if ($nativeSource -is [array]) {
    $nativeSource = @($nativeSource | Where-Object { $_ -and (Test-Path -LiteralPath ([string]$_) -PathType Leaf) } | Select-Object -Last 1)
    if ($nativeSource.Count -eq 1) { $nativeSource = [string]$nativeSource[0] }
}
if ([string]::IsNullOrWhiteSpace([string]$nativeSource) -or -not (Test-Path -LiteralPath ([string]$nativeSource) -PathType Leaf)) {
    throw "Native engine resolution returned an invalid DLL path: '$nativeSource'"
}

if ($RunSmokeTests) {
    Write-Host 'Running v1.3 Gate 2 ABI-10 native tests...' -ForegroundColor Cyan
    $ctestExe = Resolve-CMakeTool 'ctest'
    if (-not $ctestExe) { throw 'ctest was not found beside the CMake installation used for the v1.2 native build.' }
    $testPreset = if ($RemoteMonitorSpike) { 'windows-x64-remote-monitor-spike' } else { 'windows-x64-release' }
    Push-Location $repositoryRoot
    try {
        & $ctestExe --preset $testPreset
        if ($LASTEXITCODE -ne 0) { throw "v1.2 native tests failed for preset '$testPreset'." }
    }
    finally { Pop-Location }
}

if ($RunSmokeTests) {
    Write-Host 'Rebuilding managed v1.3 Gate 2 smoke-test dependency graph...' -ForegroundColor Cyan
    Push-Location $repositoryRoot
    try {
        & dotnet build .\tests\GrassiBoard.App.SmokeTests\GrassiBoard.App.SmokeTests.csproj `
            --configuration Release `
            --no-incremental
        if ($LASTEXITCODE -ne 0) { throw 'Managed v1.2 smoke-test rebuild failed.' }

        Write-Host 'Running managed v1.3 Gate 2 smoke tests...' -ForegroundColor Cyan
        & dotnet run `
            --project .\tests\GrassiBoard.App.SmokeTests\GrassiBoard.App.SmokeTests.csproj `
            --configuration Release `
            --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Managed v1.2 smoke tests failed.' }
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
    $localVersion = '1.2.0'
    $localConfiguration = if ($RemoteMonitorSpike) { 'Release-LocalV12Compat' } else { 'Release-LocalV12' }
    & (Join-Path $repositoryRoot 'tools\New-BuildInfo.ps1') `
        -Version $localVersion `
        -CommitSha $commit `
        -RunNumber 'local' `
        -OutputPath $buildInfoPath `
        -Configuration $localConfiguration `
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
Write-Host 'This is the v1.3 Gate 2 development build on the frozen v1.2.0 product baseline. It is framework-dependent for fast validation.' -ForegroundColor DarkGray

if ($Run) { Start-Process -FilePath $exe }
