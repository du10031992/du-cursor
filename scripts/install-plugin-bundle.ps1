# Install MepPanel plugin bundle to AutoCAD ApplicationPlugins.
# Default: dung plugin release san (MepPanel.AutoCAD.dll + MepPanel.Core.dll).
# Dev loader: .\scripts\install-plugin-bundle.ps1 -BuildDevLoader

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$SkipInstall,
    [switch]$BuildDevLoader
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$BundleRoot = Join-Path $Root "bundle\MepPanel.Plugin.bundle"
$InstallDir = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle"

function Ensure-ConfigFile {
    $configExample = Join-Path $BundleContents "MepPanel.config.json.example"
    $configPath = Join-Path $BundleContents "MepPanel.config.json"
    if (-not (Test-Path $configPath) -and (Test-Path $configExample)) {
        Copy-Item $configExample $configPath
        Write-Host "==> Created MepPanel.config.json from example"
    }
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
        Remove-Item $InstallDir -Recurse -Force
    }
    Copy-Item $BundleRoot $InstallDir -Recurse -Force

    $oldBundle = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanelMvp.bundle"
    if (Test-Path $oldBundle) {
        Write-Host "==> Disable old bundle: MepPanelMvp.bundle -> .OFF"
        Rename-Item $oldBundle "MepPanelMvp.bundle.OFF" -Force -ErrorAction SilentlyContinue
    }
}

$releaseRequired = @(
    "MepPanel.AutoCAD.dll",
    "MepPanel.Core.dll"
)

$blocksDll = Join-Path $BundleContents "MepPanel.Blocks.AutoCAD.dll"
if (Test-Path $blocksDll) {
    $releaseRequired += "MepPanel.Blocks.AutoCAD.dll"
}

$releaseMain = Join-Path $BundleContents "MepPanel.AutoCAD.dll"

if ($BuildDevLoader) {
    Write-Host "==> Build dev loader ($Configuration x64)"
    $PluginProj = Join-Path $Root "src\MepPanel.AutoCAD\MepPanel.AutoCAD.csproj"
    $OutDir = Join-Path $Root "src\MepPanel.AutoCAD\bin\$Configuration"

    Push-Location $Root
    dotnet build $PluginProj -c $Configuration -p:Platform=x64
    Pop-Location

    $devRequired = @(
        "MepPanel.Plugin.dll",
        "MepPanel.AutoCAD.Licensing.dll",
        "MepPanel.Blocks.AutoCAD.dll"
    )

    foreach ($file in $devRequired) {
        $path = Join-Path $OutDir $file
        if (-not (Test-Path $path)) {
            throw "Missing build output: $path"
        }
    }

    Write-Host "==> Copy dev loader DLLs to bundle"
    New-Item -ItemType Directory -Force -Path $BundleContents | Out-Null
    foreach ($file in $devRequired) {
        Copy-Item (Join-Path $OutDir $file) (Join-Path $BundleContents $file) -Force
    }

    $coreDll = Join-Path $OutDir "MepPanel.Core.dll"
    if (Test-Path $coreDll) {
        Copy-Item $coreDll (Join-Path $BundleContents "MepPanel.Core.dll") -Force
    }

    # Dev loader uses MepPanel.Plugin.dll entry in PackageContents - restore if needed
    Write-Host "==> Dev mode: NETLOAD MepPanel.Plugin.dll or update PackageContents manually"
    Install-Bundle -RequiredFiles ($devRequired + @("MepPanel.Core.dll"))

    Write-Host ""
    Write-Host "Done (dev loader). Commands: MEPSTATUS, MEPLOGIN, MEPDB, MEPHVAC, MEPLOGOUT"
    exit 0
}

if (-not (Test-Path $releaseMain)) {
    throw "Khong tim thay MepPanel.AutoCAD.dll trong bundle. Hay git pull hoac dat file vao bundle\Contents\"
}

Write-Host "==> Install MepPanel release plugin v0.13.0 (MepPanel.AutoCAD.dll)"
Install-Bundle -RequiredFiles $releaseRequired

Write-Host ""
Write-Host "Done! Restart AutoCAD - plugin loads automatically."
Write-Host "Plugin: chi lenh MEPDB tren command line. Chuc nang phu khoa/mo trong panel + /admin"
Write-Host "License Server: chay F5, OTP test 123456 (TestMode=true)"
