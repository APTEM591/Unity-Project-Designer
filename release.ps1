#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Release a new version of com.gamespear.project-designer.
.PARAMETER Version
    Semver string, e.g. "1.2.2"
.PARAMETER CommitTitle
    Short description appended to the release commit, e.g. "fix canvas overlap"
.EXAMPLE
    .\release.ps1 -Version 1.2.2 -CommitTitle "fix canvas overlap"
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$CommitTitle
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$releaseMsg  = "Release ${Version}: $CommitTitle"
$repoUrl     = "https://github.com/APTEM591/Unity-Project-Designer"
$projectRoot = Split-Path (Split-Path $PSScriptRoot)
$pkgOutput   = Join-Path $projectRoot "ProjectDesigner-${Version}.unitypackage"

function Invoke-McpCli {
    if (Get-Command unity-mcp-cli -ErrorAction SilentlyContinue) {
        unity-mcp-cli @args
    } else {
        npx unity-mcp-cli @args
    }
}

Write-Host "==> Releasing $Version`: $CommitTitle"

# ---- 1. Bump package.json ----
$pkgPath = "package.json"
(Get-Content $pkgPath -Raw) `
    -replace '"version":\s*"[^"]*"', "`"version`": `"$Version`"" |
    Set-Content $pkgPath -Encoding utf8NoBOM

# ---- 2. Record previous tag before any new commits ----
$prevTag = git describe --tags --abbrev=0 2>$null

# ---- 3. Stage and commit everything as Release X.Y.Z ----
git add -A
git commit -m $releaseMsg
$releaseHash = (git rev-parse --short HEAD).Trim()

# ---- 4. Update CHANGELOG ----
$changelogPath = "CHANGELOG.md"
$cl   = Get-Content $changelogPath -Raw
$date = Get-Date -Format "yyyy-MM-dd"

$section = "## [$Version] - $date`n- $releaseHash - $releaseMsg"
$link    = "[$Version]: $repoUrl/releases/tag/$Version"

# Insert new section before the first existing "## [" block
$i = $cl.IndexOf("`n## [")
$cl = if ($i -ge 0) {
    $cl.Substring(0, $i + 1) + $section + "`n`n" + $cl.Substring($i + 1)
} else {
    $cl.TrimEnd() + "`n`n$section`n"
}

# Insert new link before the first existing "[x.y.z]: " line
$j = $cl.IndexOf("`n[")
$cl = if ($j -ge 0) {
    $cl.Substring(0, $j + 1) + $link + "`n" + $cl.Substring($j + 1)
} else {
    $cl.TrimEnd() + "`n$link`n"
}

[System.IO.File]::WriteAllText(
    (Resolve-Path $changelogPath).Path,
    $cl,
    (New-Object System.Text.UTF8Encoding $false)
)

# ---- 5. Commit CHANGELOG hash update ----
git add CHANGELOG.md
git commit -m "Add $Version commit hash to CHANGELOG"

# ---- 6. Tag and push main + tag ----
git tag $Version
git push origin main
git push origin $Version

# ---- 7. Export .unitypackage via unity-mcp-cli (Unity must be open) ----
Write-Host "  Exporting .unitypackage..."
$csharpCode = "AssetDatabase.ExportPackage(`"Packages/com.gamespear.project-designer`", @`"$pkgOutput`", ExportPackageOptions.Recurse); Debug.Log(`"Exported: $pkgOutput`");"
@{ isMethodBody = $true; csharpCode = $csharpCode } |
    ConvertTo-Json -Compress |
    Invoke-McpCli run-tool script-execute --input-file -

if (-not (Test-Path $pkgOutput)) {
    Write-Warning "Export may have failed - $pkgOutput not found. Attach it to the release manually."
}

# ---- 8. Build release notes in commit-id format ----
$commits = if ($prevTag) {
    git log "${prevTag}..HEAD" --format="%h - %s"
} else {
    git log --format="%h - %s"
}
$notes = ($commits | ForEach-Object { "- $_" }) -join "`n"

# ---- 9. Create GitHub release ----
gh release create $Version $pkgOutput --title $Version --notes $notes

Write-Host "==> Done! $repoUrl/releases/tag/$Version"
