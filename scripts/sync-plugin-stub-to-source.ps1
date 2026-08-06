# Dong bo stub plugin (src/) sang thu muc MepPanelMvp truoc khi build release.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$StubBlocks = Join-Path $Root "src\MepPanel.Blocks.AutoCAD"
$TargetBlocks = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD"

if (-not (Test-Path $StubBlocks)) {
    Write-Host "Bo qua sync stub - khong co $StubBlocks"
    exit 0
}

Write-Host "==> Sync MepPanel.Blocks.AutoCAD stub -> $TargetBlocks"
New-Item -ItemType Directory -Force -Path $TargetBlocks | Out-Null
Copy-Item (Join-Path $StubBlocks "*") $TargetBlocks -Recurse -Force

$StubCoreGate = Join-Path $Root "src\MepPanel.Core\PluginFeatureGate.cs"
$TargetCore = Join-Path $PluginSourceRoot "src\MepPanel.Core"
if (Test-Path $StubCoreGate) {
    New-Item -ItemType Directory -Force -Path $TargetCore | Out-Null
    Copy-Item $StubCoreGate (Join-Path $TargetCore "PluginFeatureGate.cs") -Force
    Write-Host "   OK PluginFeatureGate.cs (stub dev - ban release dung Licensing patch)"
}

Write-Host "   OK sync stub plugin features"
