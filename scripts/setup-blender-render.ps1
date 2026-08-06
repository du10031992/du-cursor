# Patch render Blender + cai script/anh vao plugin (khong build lai DLL).
# Sau khi chay xong van can build plugin neu DLL cu chua co MepCabinetRenderService.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $Root "plugin.local.json"

if (-not (Test-Path $LocalConfig)) {
    throw "Chua co plugin.local.json - copy tu plugin.local.json.example"
}
$config = Get-Content $LocalConfig -Raw | ConvertFrom-Json
$PluginSourceRoot = $config.pluginSourceRoot
if (-not $PluginSourceRoot) {
    throw "Sua pluginSourceRoot trong plugin.local.json"
}

Write-Host "==> Patch MepCabinetRenderService (Blender 3D)"
& (Join-Path $Root "scripts\apply-cabinet-render-patch.ps1") -PluginSourceRoot $PluginSourceRoot

Write-Host "==> Cai renderer Python + anh AI vao bundle/AutoCAD"
& (Join-Path $Root "scripts\install-renderer-devices.ps1")

Write-Host ""
Write-Host "Buoc tiep theo (BAT BUOC de Blender hien trong AutoCAD):"
Write-Host "  .\scripts\build-plugin-release.ps1"
Write-Host ""
Write-Host "Trong AutoCAD: Render tu -> chon Blender3D (mac dinh)"
