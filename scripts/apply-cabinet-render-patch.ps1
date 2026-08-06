# Apply MepCabinetRenderService (Blender 3D) vao source MepPanelMvp.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PatchFile = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Blocks.AutoCAD\Drawing\MepCabinetRenderService.cs"
$TargetDir = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\Drawing"
$TargetFile = Join-Path $TargetDir "MepCabinetRenderService.cs"

if (-not (Test-Path $PatchFile)) {
    throw "Thieu patch: $PatchFile"
}

if (-not (Test-Path (Split-Path $TargetDir))) {
    Write-Host "   (bo qua - khong co MepPanel.Blocks.AutoCAD trong source)"
    exit 0
}

New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
Copy-Item $PatchFile $TargetFile -Force
Write-Host "   OK MepCabinetRenderService.cs"
