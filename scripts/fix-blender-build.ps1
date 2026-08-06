# Mot lenh: pull check + repair CS0234 + patch Blender + build (KHONG build Blocks rieng).
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $Root "plugin.local.json"
$BuildScript = Join-Path $Root "scripts\build-plugin-release.ps1"

# Kiem tra da pull ban moi chua
$buildText = Get-Content $BuildScript -Raw
if ($buildText -match 'Build MepPanel\.Blocks\.AutoCAD \(panel') {
    Write-Host "LOI: scripts\build-plugin-release.ps1 VAN LA BAN CU."
    Write-Host "Chay:"
    Write-Host "  git fetch origin"
    Write-Host "  git checkout cursor/license-admin-device-control-cc24"
    Write-Host "  git pull origin cursor/license-admin-device-control-cc24"
    throw "Can git pull truoc khi build."
}

if (-not (Test-Path $LocalConfig)) {
    throw "Chua co plugin.local.json"
}
$config = Get-Content $LocalConfig -Raw | ConvertFrom-Json
$PluginSourceRoot = $config.pluginSourceRoot
if (-not $PluginSourceRoot) { throw "Thieu pluginSourceRoot" }

Write-Host "==> Script OK (khong build Blocks rieng)"
Write-Host "==> 1/4 Repair CS0234 (rename file loi)"
& (Join-Path $Root "scripts\repair-blocks-cs0234.ps1") -PluginSourceRoot $PluginSourceRoot

Write-Host "==> 2/4 Patch MepCabinetRenderService (Blender)"
& (Join-Path $Root "scripts\apply-cabinet-render-patch.ps1") -PluginSourceRoot $PluginSourceRoot

Write-Host "==> 3/4 Cai Python renderer"
& (Join-Path $Root "scripts\install-renderer-devices.ps1")

Write-Host "==> 4/4 Build MepPanel.AutoCAD"
& $BuildScript

Write-Host ""
Write-Host "Xong. Restart AutoCAD -> Render tu dien."
