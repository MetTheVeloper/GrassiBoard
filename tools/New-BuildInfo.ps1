[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$CommitSha,

    [Parameter(Mandatory = $true)]
    [string]$RunNumber,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$Configuration = 'Release',
    [string]$TargetArchitecture = 'x64',
    [string]$WdkVersion = '10.0.26100.6584',
    [string]$SdkVersion = '10.0.26100.0',
    [string]$DotNetVersion = '8.0.423',
    [string]$PitchBackendVersion = 'signalsmith-stretch-1.3.2+57b93f4e'
)

$ErrorActionPreference = 'Stop'

$parentDirectory = Split-Path -Parent $OutputPath
if ($parentDirectory) {
    New-Item -ItemType Directory -Path $parentDirectory -Force | Out-Null
}

$buildInfo = [ordered]@{
    Version = $Version
    CommitSha = $CommitSha
    BuildDate = [DateTime]::UtcNow.ToString('o')
    WorkflowRunNumber = $RunNumber
    Configuration = $Configuration
    TargetArchitecture = $TargetArchitecture
    WdkVersion = $WdkVersion
    SdkVersion = $SdkVersion
    DotNetVersion = $DotNetVersion
    PitchBackendVersion = $PitchBackendVersion
}

$buildInfo | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Build information written to $OutputPath"
