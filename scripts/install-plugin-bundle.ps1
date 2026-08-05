# Build plugin + copy DLL vào bundle, rồi cài vào AutoCAD ApplicationPlugins.
# Chạy trên Windows (PowerShell) tại thư mục repo.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

$PluginProj = Join-Path $Root "src\MepPanel.AutoCAD\MepPanel.AutoCAD.csproj"
$OutDir = Join-Path $Root "src\MepPanel.AutoCAD\bin\$Configuration"
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$BundleRoot = Join-Path $Root "bundle\MepPanel.Plugin.bundle"
$InstallDir = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle"

Write-Host "==> Build MepPanel plugin ($Configuration x64)"
Push-Location $Root
dotnet build $PluginProj -c $Configuration -p:Platform=x64
Pop-Location

$required = @(
    "MepPanel.Plugin.dll",
    "MepPanel.AutoCAD.Licensing.dll",
    "MepPanel.Blocks.AutoCAD.dll"
)

foreach ($file in $required) {
    $path = Join-Path $OutDir $file
    if (-not (Test-Path $path)) {
        throw "Thiếu file build: $path"
    }
}

Write-Host "==> Copy DLL vào bundle"
New-Item -ItemType Directory -Force -Path $BundleContents | Out-Null
foreach ($file in $required) {
    Copy-Item (Join-Path $OutDir $file) (Join-Path $BundleContents $file) -Force
    Unblock-File (Join-Path $BundleContents $file) -ErrorAction SilentlyContinue
}

$coreDll = Join-Path $OutDir "MepPanel.Core.dll"
if (Test-Path $coreDll) {
    Copy-Item $coreDll (Join-Path $BundleContents "MepPanel.Core.dll") -Force
}

if ($SkipInstall) {
    Write-Host "==> Bỏ qua cài đặt (-SkipInstall). Bundle sẵn tại: $BundleRoot"
    exit 0
}

Write-Host "==> Cài bundle vào $InstallDir"
if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
}
Copy-Item $BundleRoot $InstallDir -Recurse -Force

$oldBundle = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanelMvp.bundle"
if (Test-Path $oldBundle) {
    Write-Host "==> Đổi tên bundle cũ MepPanelMvp.bundle -> .OFF"
    Rename-Item $oldBundle "MepPanelMvp.bundle.OFF" -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Xong! Mở lại AutoCAD — plugin tự load, không popup đăng nhập."
Write-Host "Gõ MEPSTATUS / MEPDB / MEPHVAC để dùng."
