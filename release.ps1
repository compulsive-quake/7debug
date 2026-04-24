# Build, package, and publish a GitHub release for the 7debug mod
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModName   = "7debug"
$Repo      = "compulsive-quake/7debug"
$Remote    = "7Debug"

# --- Read version from ModInfo.xml ---
$ModInfoPath = Join-Path $ScriptDir "ModInfo.xml"
[xml]$modInfo = Get-Content $ModInfoPath
$Version = $modInfo.xml.Version.value
if (-not $Version) { throw "Could not read version from ModInfo.xml" }

$Tag = "v$Version"
Write-Host "=== $ModName Release $Tag ===" -ForegroundColor Cyan

# --- Check for uncommitted changes ---
$status = git -C $ScriptDir status --porcelain
if ($status) {
    Write-Host "ERROR: You have uncommitted changes. Commit or stash them first." -ForegroundColor Red
    git -C $ScriptDir status --short
    exit 1
}

# --- Fetch remote tags so we catch tags pushed from another machine ---
Write-Host "Fetching remote tags..." -ForegroundColor DarkGray
git -C $ScriptDir fetch --tags --quiet 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARN: git fetch --tags failed (offline?); checking local tags only." -ForegroundColor DarkYellow
}

# --- Check tag doesn't already exist; offer to bump + commit if so ---
while (git -C $ScriptDir tag -l $Tag) {
    $parts = @($Version -split '\.')
    $last  = $parts[-1]
    if ($last -notmatch '^\d+$') {
        Write-Host "ERROR: Tag $Tag already exists and last version segment '$last' is not numeric; cannot auto-bump." -ForegroundColor Red
        exit 1
    }
    $parts[-1] = [string]([int]$last + 1)
    $NextVersion = $parts -join '.'

    Write-Host ""
    Write-Host "Tag $Tag already exists." -ForegroundColor Yellow
    $answer = Read-Host "Bump version to $NextVersion and commit? [Y/n]"
    if ($answer -and $answer -notmatch '^(y|yes)$') {
        Write-Host "Aborted." -ForegroundColor Red
        exit 1
    }

    # String-replace <Version value="..."> to preserve existing XML formatting.
    $modInfoText = [System.IO.File]::ReadAllText($ModInfoPath)
    $updated = $modInfoText -replace '(<Version\s+value=")[^"]+(")', ('${1}' + $NextVersion + '${2}')
    if ($updated -eq $modInfoText) { throw "Failed to update <Version value=...> in $ModInfoPath" }
    [System.IO.File]::WriteAllText($ModInfoPath, $updated)

    git -C $ScriptDir add $ModInfoPath
    git -C $ScriptDir commit -m "Bump version to $NextVersion"
    if ($LASTEXITCODE -ne 0) { throw "Failed to commit version bump" }

    $Version = $NextVersion
    $Tag     = "v$NextVersion"
    Write-Host "Version bumped to $Version (committed)." -ForegroundColor Green
}

# --- Extract changelog for this version (optional) ---
$ChangelogPath = Join-Path $ScriptDir "CHANGELOG.md"
if (Test-Path $ChangelogPath) {
    $changelog = Get-Content $ChangelogPath -Raw
    $escaped = [regex]::Escape($Version)
    # Match "## 1.1.0" or "## [1.1.0]" headings up to the next "## " or EOF
    $pattern = "(?ms)^##\s+\[?$escaped\]?.*?(?=^##\s+|\z)"
    $match = [regex]::Match($changelog, $pattern)
    $releaseNotes = if ($match.Success) { $match.Value.Trim() } else { "Release $Tag" }
} else {
    $releaseNotes = "Release $Tag"
}

Write-Host ""
Write-Host "Release notes:" -ForegroundColor Yellow
Write-Host $releaseNotes
Write-Host ""

# --- Build ---
$BuildScript = Join-Path $ScriptDir "build.ps1"
if (Test-Path $BuildScript) {
    Write-Host "Building via build.ps1..." -ForegroundColor Yellow
    & $BuildScript
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
} else {
    throw "build.ps1 not found"
}

$DllPath = Join-Path $ScriptDir "$ModName.dll"
if (-not (Test-Path $DllPath)) {
    throw "Built DLL not found at $DllPath"
}

# --- Package into zip ---
$ZipName    = "$ModName-$Tag.zip"
$ZipPath    = Join-Path $ScriptDir $ZipName
$StagingDir = Join-Path $env:TEMP "$ModName-release-staging"

if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
$ModStaging = Join-Path $StagingDir $ModName
New-Item -ItemType Directory -Path $ModStaging -Force | Out-Null

# Mirror deploy.ps1's mod-folder file list (Worlds/ is deployed to Data/Worlds, not the mod folder, so it is excluded)
Copy-Item (Join-Path $ScriptDir "ModInfo.xml") $ModStaging -Force
Copy-Item $DllPath                             $ModStaging -Force

$ConfigDir = Join-Path $ScriptDir "Config"
if (Test-Path $ConfigDir) {
    Copy-Item $ConfigDir $ModStaging -Recurse -Force
}

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Write-Host "Packaging $ZipName..." -ForegroundColor Yellow
Compress-Archive -Path "$StagingDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Remove-Item $StagingDir -Recurse -Force

$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
Write-Host "Created $ZipName ($zipSize MB)" -ForegroundColor Green

# --- Tag + GitHub release ---
Write-Host "Creating tag $Tag..." -ForegroundColor Yellow
git -C $ScriptDir tag -a $Tag -m "Release $Tag"
git -C $ScriptDir push $Remote $Tag

Write-Host "Creating GitHub release..." -ForegroundColor Yellow
$notesFile = Join-Path $env:TEMP "$ModName-release-notes.md"
Set-Content $notesFile $releaseNotes -Encoding UTF8
gh release create $Tag $ZipPath --title "$ModName $Tag" --notes-file $notesFile --repo $Repo
Remove-Item $notesFile -Force -ErrorAction SilentlyContinue
Remove-Item $ZipPath  -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Released $Tag successfully!" -ForegroundColor Green
