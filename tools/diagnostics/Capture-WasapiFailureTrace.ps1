[CmdletBinding()]
param(
    [string] $OutputDirectory = "$env:USERPROFILE\Desktop\GrassiBoard-Wasapi-Trace"
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an Administrator PowerShell window.'
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$ksProbe = Join-Path $scriptDirectory 'Test-KsCaptureContract.ps1'
$wasapiProbe = Join-Path $scriptDirectory 'Test-WasapiCaptureEndpoint.ps1'
if (-not (Test-Path $ksProbe) -or -not (Test-Path $wasapiProbe)) {
    throw 'The companion diagnostic scripts are missing.'
}

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$etlPath = Join-Path $OutputDirectory 'wasapi-capture-failure.etl'
$csvPath = Join-Path $OutputDirectory 'wasapi-capture-failure.csv'
$probePath = Join-Path $OutputDirectory 'probe-results.txt'
$providersPath = Join-Path $OutputDirectory 'audio-providers.txt'
$sessionName = 'GrassiBoardAudioTrace'

# Windows 10's inbox WPR 10.0.19041 can fail to save the built-in Audio profile
# with RPC_E_CHANGED_MODE. Use a focused ETW session directly so no WPR merge is
# required. These providers come from Microsoft Windows Audio's public
# audio-info-manual.wprp profile.
@(
    '{6e7b1892-5288-5fe5-8f34-e3b0dc671fd2} 0xFFFFFFFFFFFFFFFF 5' # Client
    '{46df03df-beab-5f0b-7eb2-e471c1bea993} 0xFFFFFFFFFFFFFFFF 5' # Engine
    '{2c2677bd-bc9a-5a88-4772-6f17de82b2c7} 0xFFFFFFFFFFFFFFFF 5' # AudioEngineUtil
    '{64788b34-e8e5-5664-af50-45afb294a1dc} 0xFFFFFFFFFFFFFFFF 5' # DeviceGraph
    '{553ca39b-c608-566d-18ae-7b9a03a39acd} 0xFFFFFFFFFFFFFFFF 5' # KernelStreamingEndpoint
    '{0e7e8596-d648-5c94-adbd-3d780faa58e8} 0xFFFFFFFFFFFFFFFF 5' # EndpointBuilder
    '{ec6ede49-f40f-5f93-8651-a4edc18afe06} 0xFFFFFFFFFFFFFFFF 5' # EndpointCharacteristics
    '{3d2b6366-47a4-5e10-6974-e5f7de0d3f28} 0xFFFFFFFFFFFFFFFF 5' # Service
    '{d375f14e-9624-5a18-f53e-5bce2043a97f} 0xFFFFFFFFFFFFFFFF 5' # CrossProcess
    '{8d37b4a3-2c54-560f-7b2e-b992d023682a} 0xFFFFFFFFFFFFFFFF 5' # Pump
    '{a076f707-4472-5083-d14f-826d0ef75c78} 0xFFFFFFFFFFFFFFFF 5' # MultimediaDevice
    '{ae4bd3be-f36f-45b6-8d21-bdd6fb832853} 0xFFFFFFFFFFFFFFFF 5' # Microsoft-Windows-Audio
    '{e27950eb-1768-451f-96ac-cc4e14f6d3d0} 0x000000007FFFFFFF 5' # AudioTrace WPP
) | Set-Content -Encoding ASCII $providersPath

# A missing old session is the expected clean state. Keep native cleanup output
# from becoming a terminating Windows PowerShell 5 error.
$savedErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
logman stop $sessionName -ets 2>$null | Out-Null
logman delete $sessionName 2>$null | Out-Null
$ErrorActionPreference = $savedErrorActionPreference
$started = $false
try {
    logman create trace $sessionName -ow -o $etlPath -f bincirc -max 64 -bs 64 -nb 16 64 -pf $providersPath -ets -y
    if ($LASTEXITCODE -ne 0) {
        throw "ETW trace failed to start (exit $LASTEXITCODE)."
    }
    $started = $true

    @(
        "Captured: $(Get-Date -Format o)"
        "Windows: $([Environment]::OSVersion.VersionString)"
        ''
        '=== KS capture contract ==='
        (& $ksProbe | Out-String)
        '=== WASAPI capture endpoint ==='
        (& $wasapiProbe | Out-String)
    ) | Set-Content -Encoding UTF8 $probePath
}
finally {
    if ($started) {
        logman stop $sessionName -ets
        $stopExitCode = $LASTEXITCODE
        $savedErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        logman delete $sessionName 2>$null | Out-Null
        $ErrorActionPreference = $savedErrorActionPreference
        if ($stopExitCode -ne 0) {
            throw "ETW trace failed to stop cleanly (exit $stopExitCode)."
        }
    }
}

if (-not (Test-Path $etlPath)) {
    throw 'ETW did not create the ETL trace.'
}

tracerpt $etlPath -o $csvPath -of CSV -y | Out-Null

Write-Output "Trace complete: $OutputDirectory"
Get-ChildItem $OutputDirectory | Select-Object Name,Length,LastWriteTime | Format-Table -AutoSize
