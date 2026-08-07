# Install MepPanel plugin bundle to AutoCAD ApplicationPlugins.
# Default: dung plugin release san (MepPanel.AutoCAD.dll + MepPanel.Core.dll).
# Dev loader: .\scripts\install-plugin-bundle.ps1 -BuildDevLoader
#
# Neu gap "Access denied" tren DLL: DONG AutoCAD roi chay lai.

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

function Test-AutoCadRunning {
    $procs = Get-Process -Name "acad" -ErrorAction SilentlyContinue
    return $null -ne $procs
}

function Test-FileLocked {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    try {
        $fs = [System.IO.File]::Open($Path, 'Open', 'ReadWrite', 'None')
        $fs.Close()
        return $false
    }
    catch {
        return $true
    }
}

function Get-LockedInstallDlls {
    $contents = Join-Path $InstallDir "Contents"
    if (-not (Test-Path $contents)) { return @() }
    $locked = @()
    Get-ChildItem $contents -Filter "*.dll" -ErrorAction SilentlyContinue | ForEach-Object {
        if (Test-FileLocked $_.FullName) {
            $locked += $_.Name
        }
    }
    return $locked
}

function Ensure-ConfigFile {
    $configExample = Join-Path $BundleContents "MepPanel.config.json.example"
    $configPath = Join-Path $BundleContents "MepPanel.config.json"
    if (-not (Test-Path $configPath) -and (Test-Path $configExample)) {
        Copy-Item $configExample $configPath
        Write-Host "==> Created MepPanel.config.json from example"
    }
}

function Copy-BundleOverwrite {
    # Copy tung file; khong xoa ca thu muc (tranh Access denied khi DLL dang mo).
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $dstContents = Join-Path $InstallDir "Contents"
    New-Item -ItemType Directory -Force -Path $dstContents | Out-Null

    # PackageContents.xml o root bundle
    $pkg = Join-Path $BundleRoot "PackageContents.xml"
    if (Test-Path $pkg) {
        Copy-Item $pkg (Join-Path $InstallDir "PackageContents.xml") -Force
    }

    Get-ChildItem $BundleContents -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($BundleContents.Length).TrimStart('\', '/')
        $dest = Join-Path $dstContents $rel
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Force -Path $destDir | Out-Null
        }
        try {
            Copy-Item $_.FullName $dest -Force
        }
        catch {
            throw @"
Khong ghi de duoc: $dest

Nguyen nhan thuong gap: AutoCAD dang mo va khoa DLL.

Cach xu ly:
  1. Dong tat ca cua so AutoCAD
  2. (Neu van loi) Task Manager -> End task "acad.exe"
  3. Chay lai:
       .\scripts\install-plugin-bundle.ps1

Chi tiet: $($_.Exception.Message)
"@
        }
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

    if (Test-AutoCadRunning) {
        throw @"
AutoCAD dang chay — khong the ghi de MepPanel.AutoCAD.dll (file dang bi khoa).

Dong AutoCAD roi chay lai:
  .\scripts\install-plugin-bundle.ps1
"@
    }

    $locked = Get-LockedInstallDlls
    if ($locked.Count -gt 0) {
        throw @"
DLL plugin dang bi khoa: $($locked -join ', ')

Dong AutoCAD (hoac process dang giu file) roi chay lai:
  .\scripts\install-plugin-bundle.ps1
"@
    }

    Write-Host "==> Install bundle to $InstallDir"
    # Thu xoa cu; neu fail thi van copy de ghi de.
    if (Test-Path $InstallDir) {
        try {
            Remove-Item $InstallDir -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Host "==> Khong xoa duoc bundle cu (co the file dang mo). Thu ghi de tung file..."
            Copy-BundleOverwrite
            Write-Host "==> Da ghi de bundle (khong xoa thu muc cu)."
            Disable-OldBundle
            return
        }
    }

    Copy-Item $BundleRoot $InstallDir -Recurse -Force
    Disable-OldBundle
}

function Disable-OldBundle {
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

    Write-Host "==> Dev mode: NETLOAD MepPanel.Plugin.dll or update PackageContents manually"
    Install-Bundle -RequiredFiles ($devRequired + @("MepPanel.Core.dll"))

    Write-Host ""
    Write-Host "Done (dev loader). Commands: MEPSTATUS, MEPLOGIN, MEPDB, MEPHVAC, MEPLOGOUT"
    exit 0
}

if (-not (Test-Path $releaseMain)) {
    throw "Khong tim thay MepPanel.AutoCAD.dll trong bundle. Hay git pull hoac dat file vao bundle\Contents\"
}

Write-Host "==> Install MepPanel release plugin (MepPanel.AutoCAD.dll)"
Install-Bundle -RequiredFiles $releaseRequired

Write-Host ""
Write-Host "Done! Restart AutoCAD - plugin loads automatically."
Write-Host "Plugin: chi lenh MEPDB tren command line. Chuc nang phu khoa/mo trong panel + /admin"
Write-Host "License Server: chay F5, OTP test 123456 (TestMode=true)"
