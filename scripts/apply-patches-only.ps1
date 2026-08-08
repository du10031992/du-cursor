# Apply toan bo patches vao MepPanelMvp — KHONG build.
# Chay mot lan sau git pull. Sau do build trong Visual Studio.
param(
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$ConfirmPreference = "None"
$Root = Split-Path -Parent $PSScriptRoot

$LocalConfig = Join-Path $Root "plugin.local.json"
if (-not $PluginSourceRoot) {
    if (Test-Path $LocalConfig) {
        $cfg = Get-Content $LocalConfig -Raw | ConvertFrom-Json
        if ($cfg.pluginSourceRoot) { $PluginSourceRoot = $cfg.pluginSourceRoot }
    }
}

if (-not $PluginSourceRoot -or -not (Test-Path $PluginSourceRoot)) {
    throw @"
Chua cau hinh pluginSourceRoot.
Tao file plugin.local.json hoac truyen tham so:
  .\scripts\apply-patches-only.ps1 -PluginSourceRoot C:\MepPanel\MepPanelMvp
"@
}

Write-Host ""
Write-Host "==> Apply patches -> $PluginSourceRoot"
Write-Host "    (Khong build — sau do mo Visual Studio, Ctrl+Shift+B)"
Write-Host ""

function Run([string]$Script) {
    $path = Join-Path $Root "scripts\$Script"
    if (Test-Path $path) {
        & $path -PluginSourceRoot $PluginSourceRoot
    }
}

# 1. Dong bo toan bo Blocks project
Run "apply-blocks-project-patch.ps1"

# 2. Xoa file legacy gay loi compile
Run "repair-blocks-cs0234.ps1"

# 3. Cabinet render
$cabinet = Join-Path $Root "scripts\apply-cabinet-render-patch.ps1"
if (Test-Path $cabinet) { & $cabinet -PluginSourceRoot $PluginSourceRoot }

# 4. He nuoc / PCCC
Run "apply-water-pccc-patch.ps1"

# 5. Enable nut He nuoc / PCCC tren panel WPF
Run "apply-enable-water-fire-ui.ps1"

# 6. License types
Run "apply-licensing-types-patch.ps1"

# 7. Sua source bi hong (guard inject sai)
Run "repair-plugin-source.ps1"

# 8. Sua corrupted command methods
Run "repair-corrupted-command-methods.ps1"

# 9. Dispatcher UI (tranh CS0104)
Run "apply-ui-dispatcher-patch.ps1"

# 10. Single-entry MEPDB
Run "apply-single-entry-patch.ps1"

# 11. Feature guard
Run "apply-feature-guard-patch.ps1"

Write-Host ""
Write-Host "==> Xong patches."
Write-Host "    Mo Visual Studio: C:\MepPanel\MepPanelMvp\[solution].sln"
Write-Host "    Ctrl+Shift+B -> Build Solution (Release | x64)"
Write-Host "    Build xong -> Restart AutoCAD -> MEPDB"
Write-Host ""
