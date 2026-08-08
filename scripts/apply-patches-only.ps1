# Apply toan bo patches vao MepPanelMvp -- KHONG build.
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
    throw "Chua cau hinh pluginSourceRoot. Truyen tham so: .\scripts\apply-patches-only.ps1 -PluginSourceRoot C:\MepPanel\MepPanelMvp"
}

Write-Host "==> Apply patches -> $PluginSourceRoot"

function Run([string]$Script) {
    $path = Join-Path $Root "scripts\$Script"
    if (Test-Path $path) {
        & $path -PluginSourceRoot $PluginSourceRoot
    }
}

Run "apply-blocks-project-patch.ps1"
Run "repair-blocks-cs0234.ps1"

$cabinet = Join-Path $Root "scripts\apply-cabinet-render-patch.ps1"
if (Test-Path $cabinet) { & $cabinet -PluginSourceRoot $PluginSourceRoot }

Run "apply-water-pccc-patch.ps1"
Run "apply-enable-water-fire-ui.ps1"
Run "apply-licensing-types-patch.ps1"
Run "repair-plugin-source.ps1"
Run "repair-corrupted-command-methods.ps1"
Run "apply-ui-dispatcher-patch.ps1"
Run "apply-single-entry-patch.ps1"
Run "apply-feature-guard-patch.ps1"

Write-Host ""
Write-Host "==> Xong patches."
Write-Host "    Mo Visual Studio -> Ctrl+Shift+B"
Write-Host "    Build xong -> Restart AutoCAD -> MEPDB"
