[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = Join-Path $root "artifacts\hotfix36-backup\$timestamp"

function Backup-File([string]$RelativePath) {
    $source = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $source)) { return }
    $destination = Join-Path $backupRoot $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

function Replace-Text([string]$RelativePath, [scriptblock]$Transform) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file is missing: $RelativePath"
    }
    Backup-File $RelativePath
    $text = [IO.File]::ReadAllText($path)
    $newText = & $Transform $text
    if ([string]::IsNullOrWhiteSpace($newText)) {
        throw "Hotfix transform produced empty content for $RelativePath"
    }
    [IO.File]::WriteAllText($path, $newText, [Text.UTF8Encoding]::new($true))
}

Write-Host 'Applying GrassiBoard v1.2 personal-stable production metadata...' -ForegroundColor Cyan

# The user's actual App.csproj contains the conditional WebRTC package/constant
# definitions that were proven by the successful spike builds. Hotfix 36 turns
# that existing switch on globally instead of replacing the live project file.
$appProject = Join-Path $root 'src\GrassiBoard.App\GrassiBoard.App.csproj'
$appProjectText = [IO.File]::ReadAllText($appProject)
$requiredProjectTokens = @(
    'EnableRemoteMonitorSpike',
    'REMOTE_MONITOR_SPIKE',
    'SIPSorcery',
    '10.0.13',
    'Concentus',
    '2.2.2',
    'Microsoft.AspNetCore.App',
    'GrassiBoard.RemoteWeb'
)
foreach ($token in $requiredProjectTokens) {
    if ($appProjectText.IndexOf($token, [StringComparison]::Ordinal) -lt 0) {
        throw "The live GrassiBoard.App.csproj is missing expected v1.2 token '$token'. Do not continue from an older project baseline."
    }
}

Replace-Text 'src\GrassiBoard.Installer\InstallationService.cs' {
    param($text)
    [Regex]::Replace($text, 'internal const string ProductVersion = "[^"]+";', 'internal const string ProductVersion = "1.2.0";', 1)
}

Replace-Text 'src\GrassiBoard.Installer\MainWindow.xaml' {
    param($text)
    [Regex]::Replace($text, 'Ready to install GrassiBoard [0-9]+\.[0-9]+\.[0-9]+', 'Ready to install GrassiBoard 1.2.0', 1)
}

$workflow = Join-Path $root '.github\workflows\build.yml'
if (Test-Path -LiteralPath $workflow) {
    Replace-Text '.github\workflows\build.yml' {
        param($text)
        $updated = [Regex]::Replace($text, 'GRASSIBOARD_VERSION:\s*[0-9]+\.[0-9]+\.[0-9]+', 'GRASSIBOARD_VERSION: 1.2.0', 1)
        if ($updated.IndexOf('pnpm', [StringComparison]::OrdinalIgnoreCase) -lt 0 -or
            $updated.IndexOf('RemoteWeb', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            Write-Warning 'build.yml does not visibly contain the accepted v1.1 RemoteWeb build step. Local v1.2 is still usable, but inspect CI before any future public release.'
        }
        $updated
    }
}

$currentStatusPath = Join-Path $root 'docs\current-status.md'
if (Test-Path -LiteralPath $currentStatusPath) {
    Replace-Text 'docs\current-status.md' {
        param($text)
        $section = @"

## v1.2.0 personal-stable production candidate — 2026-08-12

All Remote Monitor source/mix/UI gates are manually accepted on the real Windows + Android path. Hotfix 36 promotes ABI 9 + the existing tested WebRTC/Opus monitor stack into the normal v1.2 build defaults for current private/personal use. Public distribution is not currently planned and must reopen the SIPSorcery license review before release.

Final USER ACCEPTED status is pending one normal-build 30–60 minute soak plus packaging/CI verification. Program output remains external VB-CABLE and is unchanged.
"@
        if ($text.IndexOf('## v1.2.0 personal-stable production candidate', [StringComparison]::Ordinal) -ge 0) { return $text }
        return $text.TrimEnd() + "`r`n" + $section + "`r`n"
    }
}

$changelogPath = Join-Path $root 'CHANGELOG.md'
if (Test-Path -LiteralPath $changelogPath) {
    Replace-Text 'CHANGELOG.md' {
        param($text)
        if ($text.IndexOf('## [1.2.0]', [StringComparison]::Ordinal) -ge 0) { return $text }
        $entry = @"
## [1.2.0] - 2026-08-12

### Added
- GrassiMote Remote Monitor over same-LAN WebRTC/Opus.
- Independent Windows/Space, Soundboard, Media, and opt-in processed My Voice monitor sources.
- Automatic Media duplicate prevention.
- Phone-only source gains and monitor master.
- Brutal-minimal six-tile Monitor control surface with direct tap/drag levels.

### Changed
- Native audio ABI baseline advances to 9 for v1.2.
- Normal v1.2 builds enable the accepted Remote Monitor path by default.
- Native engine version reports 1.2.0.

### Notes
- Program/VB-CABLE routing is unchanged.
- Current v1.2 use is private/personal; dependency license review must be reopened before future public distribution.
- Final USER ACCEPTED status is pending the production-candidate soak/package gate.

"@
        $lines = $text -split "`r?`n", 0
        $insertAt = 0
        while ($insertAt -lt $lines.Length -and ($lines[$insertAt].StartsWith('#') -or [string]::IsNullOrWhiteSpace($lines[$insertAt]))) {
            $insertAt++
        }
        return ($lines[0..([Math]::Max(0,$insertAt-1))] -join "`r`n") + "`r`n`r`n" + $entry + (($lines[$insertAt..($lines.Length-1)] -join "`r`n"))
    }
}

$noticePath = Join-Path $root 'THIRD-PARTY-NOTICES.txt'
if (Test-Path -LiteralPath $noticePath) {
    Replace-Text 'THIRD-PARTY-NOTICES.txt' {
        param($text)
        if ($text.IndexOf('SIPSorcery 10.0.13', [StringComparison]::Ordinal) -ge 0) { return $text }
        $notice = @"

-------------------------------------------------------------------------------
SIPSorcery 10.0.13
Role: same-LAN WebRTC / ICE / SDP / RTP transport for GrassiBoard v1.2 Remote Monitor.
Copyright: Aaron Clauson and SIPSorcery contributors.
License: BSD 3-Clause plus the upstream Additional Use Restriction.
Upstream license: sipsorcery-org/sipsorcery LICENSE.md
Current GrassiBoard scope: private/personal use. Re-review before public distribution.

Concentus 2.2.2
Role: managed Opus codec used by the v1.2 Remote Monitor transport.
Copyright: various rights holders including Xiph.Org Foundation and contributors.
License: permissive Opus/Concentus redistribution terms; see lostromb/concentus LICENSE.
-------------------------------------------------------------------------------
"@
        return $text.TrimEnd() + "`r`n" + $notice + "`r`n"
    }
}

$licensesPath = Join-Path $root 'LICENSES.md'
if (Test-Path -LiteralPath $licensesPath) {
    Replace-Text 'LICENSES.md' {
        param($text)
        if ($text.IndexOf('### SIPSorcery 10.0.13', [StringComparison]::Ordinal) -ge 0) { return $text }
        $addition = @"

### SIPSorcery 10.0.13
Used for the v1.2 WebRTC transport. Upstream licensing is BSD 3-Clause plus an Additional Use Restriction. Current GrassiBoard use is private/personal; re-review the upstream license before any future public distribution.

### Concentus 2.2.2
Used for managed Opus encoding. See the upstream Concentus LICENSE and included third-party notices.
"@
        return $text.TrimEnd() + "`r`n" + $addition + "`r`n"
    }
}

Write-Host "Hotfix 36 metadata migration complete." -ForegroundColor Green
Write-Host "Backup: $backupRoot" -ForegroundColor DarkGray
Write-Host ''
Write-Host 'Next build command:' -ForegroundColor Cyan
Write-Host 'powershell -ExecutionPolicy Bypass -File .\tools\Build-LocalRemoteTest.ps1 -Run -RunSmokeTests'
