# Sua nhanh PanelCommands.cs bi CS1519 — chay doc lap neu build van loi.
param(
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $Root "plugin.local.json"

if (-not $PluginSourceRoot -and (Test-Path $LocalConfig)) {
    $cfg = Get-Content $LocalConfig -Raw | ConvertFrom-Json
    if ($cfg.pluginSourceRoot) { $PluginSourceRoot = $cfg.pluginSourceRoot }
}

if (-not $PluginSourceRoot) {
    throw "Truyen duong dan: .\scripts\fix-panel-commands-now.ps1 -PluginSourceRoot `"C:\...\MepPanelMvp`""
}

$Repair = Join-Path $Root "scripts\repair-corrupted-command-methods.ps1"
& $Repair -PluginSourceRoot $PluginSourceRoot -TryGitRestoreFirst

Write-Host ""
Write-Host "Tiep theo: dong AutoCAD, chay .\scripts\build-plugin-release.ps1"
