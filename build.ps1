$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Join-Path $ScriptDir "src"
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

if (-not (Test-Path "$GameDir\7DaysToDie_Data\Managed")) {
    Write-Host "ERROR: Managed directory not found at: $GameDir" -ForegroundColor Red
    exit 1
}

Write-Host "Building 7debug..." -ForegroundColor Cyan
Push-Location $SrcDir
try {
    dotnet build -c Release /p:GameDir="$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "Build successful!" -ForegroundColor Green
} finally {
    Pop-Location
}
