# Cài plugin MepPanel vào AutoCAD ApplicationPlugins.
#
# Cách 1 — Plugin build tu repo (khuyên dùng, v0.14+):
#   .\scripts\build-plugin-from-repo.ps1
#
# Cách 2 — Chi build + cai dev loader:
#   .\scripts\install-plugin-bundle.ps1 -BuildDevLoader
#
# Cách 3 — Plugin release cu (MepPanel.AutoCAD.dll v0.13 trong bundle):
#   .\scripts\install-plugin-bundle.ps1

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipInstall,
    [switch]$BuildDevLoader
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$BundleRoot = Join-Path $Root "bundle\MepPanel.Plugin.bundle"
$PackageContentsPath = Join-Path $BundleRoot "PackageContents.xml"
$InstallDir = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle"

function Ensure-ConfigFile {
    $configExample = Join-Path $BundleContents "MepPanel.config.json.example"
    $configPath = Join-Path $BundleContents "MepPanel.config.json"
    if (-not (Test-Path $configPath) -and (Test-Path $configExample)) {
        Copy-Item $configExample $configPath
        Write-Host "==> Created MepPanel.config.json from example"
    }
}

function Update-PackageContentsForDevLoader {
    if (-not (Test-Path $PackageContentsPath)) { return }
    [xml]$xml = Get-Content $PackageContentsPath
    $entry = $xml.ApplicationPackage.Components.ComponentEntry
    $entry.ModuleName = "./Contents/MepPanel.Plugin.dll"
    $entry.AppDescription = "MepPanel MEP Plugin (repo dev loader)"
    $xml.Save($PackageContentsPath)
    Write-Host "==> PackageContents.xml -> MepPanel.Plugin.dll"
}

function Install-Bundle {
    param([string[]]$RequiredFiles)

    foreach ($file in $RequiredFiles) {
        $path = Join-Path $BundleContents $file
        if (-not (Test-Path $path)) {
            throw "Missing bundle file: $path"
        }
        Unblock-File $path -ErrorAction SilentlyContinue
    }

    Ensure-ConfigFile

    if ($SkipInstall) {
        Write-Host "==> Skip install (-SkipInstall). Bundle ready at: $BundleRoot"
        return
    }

    Write-Host "==> Install bundle to $InstallDir"
    if (Test-Path $InstallDir) {
        # Xoa file .gitkeep truoc (co the bi khoa quyen)
        Get-ChildItem $InstallDir -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq ".gitkeep" -or $_.Attributes -band [IO.FileAttributes]::ReadOnly } |
            ForEach-Object { $_.Attributes = "Normal" }
        Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
        # Neu van con, copy de
        if (Test-Path $InstallDir) {
            Write-Host "   (Thu muc cu van ton tai, se copy de)"
        }
    }
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Copy-Item (Join-Path $BundleRoot "*") $InstallDir -Recurse -Force

    $oldBundle = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanelMvp.bundle"
    if (Test-Path $oldBundle) {
        Write-Host "==> Disable old bundle: MepPanelMvp.bundle -> .OFF"
        Rename-Item $oldBundle "MepPanelMvp.bundle.OFF" -Force -ErrorAction SilentlyContinue
    }
}

$repoLoaderMain = Join-Path $BundleContents "MepPanel.Plugin.dll"
$releaseMain = Join-Path $BundleContents "MepPanel.AutoCAD.dll"

$repoRequired = @(
    "MepPanel.Plugin.dll",
    "MepPanel.AutoCAD.Licensing.dll",
    "MepPanel.Blocks.AutoCAD.dll",
    "MepPanel.Core.dll"
)

$releaseRequired = @(
    "MepPanel.AutoCAD.dll",
    "MepPanel.Core.dll"
)

if ($BuildDevLoader) {
    Write-Host "==> Build dev loader ($Configuration x64)"
    & (Join-Path $Root "scripts\build-plugin-from-repo.ps1") -Configuration $Configuration -SkipInstall
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Update-PackageContentsForDevLoader
    Install-Bundle -RequiredFiles $repoRequired
    Write-Host ""
    Write-Host "Done (dev loader). Restart AutoCAD."
    Write-Host "Commands: MEPSTATUS, MEPLOGIN, MEPDB, MEPHVAC, MEPLOGOUT"
    Write-Host "devMode=true trong MepPanel.config.json -> khong can license server"
    exit 0
}

if (Test-Path $repoLoaderMain) {
    Write-Host "==> Install MepPanel repo build (MepPanel.Plugin.dll v0.14+)"
    Install-Bundle -RequiredFiles $repoRequired
    Write-Host ""
    Write-Host "Done! Restart AutoCAD - plugin loads automatically."
    Write-Host "Command: MEPDB -> MEP DRAWING TOOL panel"
    Write-Host "devMode=true: bo qua license server (xem MepPanel.config.json)"
    exit 0
}

if (-not (Test-Path $releaseMain)) {
    throw @"
Khong tim thay plugin trong bundle.

Hay build tu repo (Windows + AutoCAD 2021):
  .\scripts\build-plugin-from-repo.ps1

Hoac git pull de lay MepPanel.AutoCAD.dll release v0.13.
"@
}

Write-Host "==> Install MepPanel release plugin v0.13.0 (MepPanel.AutoCAD.dll)"
Install-Bundle -RequiredFiles $releaseRequired

Write-Host ""
Write-Host "Done! Restart AutoCAD - plugin loads automatically."
Write-Host "Plugin: chi lenh MEPDB tren command line. Chuc nang phu khoa/mo trong panel + /admin"
Write-Host "License Server: chay F5, OTP test 123456 (TestMode=true)"
