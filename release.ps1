#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Creates a new release by bumping version, committing, tagging, and pushing.

.PARAMETER Bump
    Version bump type: major, minor, or patch

.PARAMETER Message
    What was done in this release (used in commit message)

.EXAMPLE
    .\release.ps1 -Bump patch -Message "Fix installer sc.exe quoting"
    .\release.ps1 -Bump minor -Message "Add new terminal feature"
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump,

    [Parameter(Mandatory=$true)]
    [string]$Message
)

$ErrorActionPreference = "Stop"

# Files to update
$versionJsonPath = "$PSScriptRoot\version.json"
$webCsprojPath = "$PSScriptRoot\Ai.Tlbx.MiddleManager\Ai.Tlbx.MiddleManager.csproj"
$ttyHostCsprojPath = "$PSScriptRoot\Ai.Tlbx.MiddleManager.TtyHost\Ai.Tlbx.MiddleManager.TtyHost.csproj"
$ttyHostProgramPath = "$PSScriptRoot\Ai.Tlbx.MiddleManager.TtyHost\Program.cs"

# Read current version from version.json
$versionJson = Get-Content $versionJsonPath | ConvertFrom-Json
$currentVersion = $versionJson.web
Write-Host "Current version: $currentVersion" -ForegroundColor Cyan

# Parse and bump version
$parts = $currentVersion.Split('.')
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

switch ($Bump) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
}

$newVersion = "$major.$minor.$patch"
Write-Host "New version: $newVersion" -ForegroundColor Green

# Update version.json
$versionJson.web = $newVersion
$versionJson.pty = $newVersion
$versionJson | ConvertTo-Json | Set-Content $versionJsonPath
Write-Host "  Updated: version.json" -ForegroundColor Gray

# Update web csproj (use flexible regex to handle version mismatch)
$content = Get-Content $webCsprojPath -Raw
$content = $content -replace "<Version>\d+\.\d+\.\d+</Version>", "<Version>$newVersion</Version>"
Set-Content $webCsprojPath $content -NoNewline
Write-Host "  Updated: Ai.Tlbx.MiddleManager.csproj" -ForegroundColor Gray

# Update TtyHost csproj
$content = Get-Content $ttyHostCsprojPath -Raw
$content = $content -replace "<Version>\d+\.\d+\.\d+</Version>", "<Version>$newVersion</Version>"
$content = $content -replace "<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>", "<FileVersion>$newVersion.0</FileVersion>"
Set-Content $ttyHostCsprojPath $content -NoNewline
Write-Host "  Updated: Ai.Tlbx.MiddleManager.TtyHost.csproj" -ForegroundColor Gray

# Update TtyHost Program.cs
$content = Get-Content $ttyHostProgramPath -Raw
$content = $content -replace 'public const string Version = "\d+\.\d+\.\d+"', "public const string Version = `"$newVersion`""
Set-Content $ttyHostProgramPath $content -NoNewline
Write-Host "  Updated: Ai.Tlbx.MiddleManager.TtyHost\Program.cs" -ForegroundColor Gray

# Git operations
Write-Host ""
Write-Host "Committing and tagging..." -ForegroundColor Cyan

git add -A
if ($LASTEXITCODE -ne 0) { throw "git add failed" }

git commit -m "v${newVersion}: $Message"
if ($LASTEXITCODE -ne 0) { throw "git commit failed" }

git tag -a "v$newVersion" -m "v${newVersion}: $Message"
if ($LASTEXITCODE -ne 0) { throw "git tag failed" }

git push origin main
if ($LASTEXITCODE -ne 0) { throw "git push main failed" }

git push origin "v$newVersion"
if ($LASTEXITCODE -ne 0) { throw "git push tag failed" }

Write-Host ""
Write-Host "Released v$newVersion" -ForegroundColor Green
Write-Host "Monitor build: https://github.com/AiTlbx/MiddleManager/actions" -ForegroundColor Cyan
