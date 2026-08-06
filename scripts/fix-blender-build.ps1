# Fix nhanh: patch Blender vao MepCabinetRenderService + cai Python + build AutoCAD (khong build Blocks rieng).
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $Root "plugin.local.json"

if (-not (Test-Path $LocalConfig)) {
    throw "Chua co plugin.local.json"
}
$config = Get-Content $LocalConfig -Raw | ConvertFrom-Json
$PluginSourceRoot = $config.pluginSourceRoot
if (-not $PluginSourceRoot) { throw "Thieu pluginSourceRoot trong plugin.local.json" }

Write-Host "==> 1/4 Patch MepCabinetRenderService (Blender args + timeout 600s)"
& (Join-Path $Root "scripts\apply-cabinet-render-patch.ps1") -PluginSourceRoot $PluginSourceRoot

Write-Host "==> 2/4 Repair CS0234 Blocks (exclude file loi)"
& (Join-Path $Root "scripts\repair-blocks-cs0234.ps1") -PluginSourceRoot $PluginSourceRoot

Write-Host "==> 3/4 Cai Python renderer + anh AI"
& (Join-Path $Root "scripts\install-renderer-devices.ps1")

Write-Host "==> 4/4 Build plugin (MepPanel.AutoCAD)"
& (Join-Path $Root "scripts\build-plugin-release.ps1")

Write-Host ""
Write-Host "Xong. Restart AutoCAD -> Render tu dien."
Write-Host "Command line Python se co: --quality blender"
