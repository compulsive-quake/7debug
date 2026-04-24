$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppId = "251570"  # 7 Days to Die

# --- Find 7DTD via Steam registry + libraryfolders.vdf ---
function Find-GameDir {
    $steamPath = $null
    foreach ($key in @(
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam",
        "HKCU:\SOFTWARE\Valve\Steam"
    )) {
        $reg = Get-ItemProperty -Path $key -Name InstallPath -ErrorAction SilentlyContinue
        if ($reg) { $steamPath = $reg.InstallPath; break }
    }
    if (-not $steamPath) {
        Write-Host "Steam not found in registry." -ForegroundColor Red
        exit 1
    }

    $vdf = Join-Path $steamPath "steamapps\libraryfolders.vdf"
    if (-not (Test-Path $vdf)) {
        Write-Host "libraryfolders.vdf not found at $vdf" -ForegroundColor Red
        exit 1
    }

    $content = Get-Content $vdf -Raw
    $libraries = [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' }

    foreach ($lib in $libraries) {
        $manifest = Join-Path $lib "steamapps\appmanifest_$AppId.acf"
        if (Test-Path $manifest) {
            $acf = Get-Content $manifest -Raw
            $m = [regex]::Match($acf, '"installdir"\s+"([^"]+)"')
            if ($m.Success) {
                $dir = Join-Path $lib "steamapps\common\$($m.Groups[1].Value)"
                if (Test-Path $dir) { return $dir }
            }
        }
    }

    Write-Host "7 Days to Die (AppId $AppId) not found in any Steam library." -ForegroundColor Red
    exit 1
}

$GameDir = Find-GameDir
Write-Host "Found 7DTD at: $GameDir" -ForegroundColor Yellow

# Build first
& "$ScriptDir\build.ps1"

# Deploy
$ModDest = Join-Path $GameDir "Mods\7debug"
Write-Host "Deploying to $ModDest..." -ForegroundColor Cyan

if (Test-Path $ModDest) {
    Remove-Item $ModDest -Recurse -Force
}

New-Item -ItemType Directory -Path $ModDest -Force | Out-Null

Copy-Item (Join-Path $ScriptDir "ModInfo.xml") $ModDest
Copy-Item (Join-Path $ScriptDir "7debug.dll") $ModDest

# Deploy Config (XML patches)
$ConfigDir = Join-Path $ScriptDir "Config"
if (Test-Path $ConfigDir) {
    Copy-Item $ConfigDir $ModDest -Recurse
}

# Deploy worlds
$WorldsDir = Join-Path $ScriptDir "Worlds"
if (Test-Path $WorldsDir) {
    foreach ($world in Get-ChildItem -Path $WorldsDir -Directory) {
        $worldDest = Join-Path $GameDir "Data\Worlds\$($world.Name)"
        Write-Host "Deploying world $($world.Name) to $worldDest..." -ForegroundColor Cyan
        if (Test-Path $worldDest) {
            Remove-Item $worldDest -Recurse -Force
        }
        Copy-Item $world.FullName $worldDest -Recurse
    }
}

Write-Host "Deployed successfully!" -ForegroundColor Green
Write-Host "Debug server will start on port 7860 when the game loads." -ForegroundColor Yellow
