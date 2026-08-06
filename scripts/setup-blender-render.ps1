# Setup nhanh Blender render - KHONG bat buoc build thanh cong.
# Neu DLL cu da co Render: chi can Python + timeout patch.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $Root "plugin.local.json"

Write-Host "==> Cai Python renderer (mac dinh --quality blender)"
& (Join-Path $Root "scripts\install-renderer-devices.ps1")

if (Test-Path $LocalConfig) {
    $config = Get-Content $LocalConfig -Raw | ConvertFrom-Json
    if ($config.pluginSourceRoot) {
        Write-Host "==> Patch C# timeout + Blender args"
        & (Join-Path $Root "scripts\apply-cabinet-render-patch.ps1") -PluginSourceRoot $config.pluginSourceRoot
        Write-Host "==> Repair CS0234"
        & (Join-Path $Root "scripts\repair-blocks-cs0234.ps1") -PluginSourceRoot $config.pluginSourceRoot
    }
}

Write-Host ""
Write-Host "Tiep theo: .\scripts\build-plugin-release.ps1"
Write-Host "Hoac mot lenh: .\scripts\fix-blender-build.ps1"
