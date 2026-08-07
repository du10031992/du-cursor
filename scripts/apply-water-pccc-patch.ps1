# Copy he nuoc / PCCC (ve, phan tich, render PNG) vao MepPanelMvp.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PatchRoot = Join-Path $Root "patches\MepPanelMvp\src"

$CoreCalcSrc = Join-Path $PatchRoot "MepPanel.Core\Calculations"
$CoreStdSrc = Join-Path $PatchRoot "MepPanel.Core\Standards"
$DrawSrc = Join-Path $PatchRoot "MepPanel.Blocks.AutoCAD\Drawing"
$FeaturesSrc = Join-Path $PatchRoot "MepPanel.Core\PluginFeatures.cs"
$GateSrc = Join-Path $PatchRoot "MepPanel.Core\PluginFeatureGate.cs"
$DbPanelDst = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\MepDbToolPanel.cs"
$DbPanelSrc = Join-Path $PatchRoot "MepPanel.Blocks.AutoCAD\MepDbToolPanel.cs"

$CoreCalcDst = Join-Path $PluginSourceRoot "src\MepPanel.Core\Calculations"
$CoreStdDst = Join-Path $PluginSourceRoot "src\MepPanel.Core\Standards"
$DrawDst = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\Drawing"
$FeaturesDst = Join-Path $PluginSourceRoot "src\MepPanel.Core\PluginFeatures.cs"
$GateDst = Join-Path $PluginSourceRoot "src\MepPanel.Core\PluginFeatureGate.cs"

if (-not (Test-Path $DrawSrc)) {
    throw "Thieu patch Drawing: $DrawSrc"
}

Write-Host "==> Apply patch He nuoc / PCCC (ve + phan tich + render PNG)"

if (Test-Path $CoreCalcSrc) {
    New-Item -ItemType Directory -Force -Path $CoreCalcDst | Out-Null
    Copy-Item (Join-Path $CoreCalcSrc "*.cs") $CoreCalcDst -Force
    Write-Host "   OK Calculations (Water/Fire)"
}

if (Test-Path $CoreStdSrc) {
    New-Item -ItemType Directory -Force -Path $CoreStdDst | Out-Null
    Copy-Item (Join-Path $CoreStdSrc "*.cs") $CoreStdDst -Force
    Write-Host "   OK Standards catalog"
}

if (Test-Path $FeaturesSrc) {
    New-Item -ItemType Directory -Force -Path (Split-Path $FeaturesDst) | Out-Null
    Copy-Item $FeaturesSrc $FeaturesDst -Force
    Write-Host "   OK PluginFeatures (MEPDBWATER / MEPDBSMOKE)"
}

if (Test-Path $GateSrc) {
    # Chi them stub neu chua co gate (tranh ghi de ban Licensing day du).
    if (-not (Test-Path $GateDst)) {
        Copy-Item $GateSrc $GateDst -Force
        Write-Host "   OK PluginFeatureGate stub"
    }
    else {
        Write-Host "   (giu PluginFeatureGate hien co)"
    }
}

if (Test-Path (Split-Path $DrawDst)) {
    New-Item -ItemType Directory -Force -Path $DrawDst | Out-Null
    $drawFiles = @(
        "MepDbDrawingService.cs",
        "MepHvacDrawingService.cs",
        "MepWaterDrawingService.cs",
        "MepFireDrawingService.cs",
        "MepPipeSystem.cs",
        "MepPipeFittingKind.cs",
        "MepPipeLibraryService.cs",
        "MepPluginConfig.cs",
        "MepKnowledgeService.cs",
        "MepPipeRenderService.cs",
        "MepDrawingHelper.cs",
        "MepCabinetRenderService.cs"
    )
    foreach ($name in $drawFiles) {
        $src = Join-Path $DrawSrc $name
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $DrawDst $name) -Force
            Write-Host "   OK $name"
        }
    }
}
else {
    Write-Host "   (bo qua Drawing - khong co MepPanel.Blocks.AutoCAD)"
}

if ((Test-Path $DbPanelSrc) -and (Test-Path (Split-Path $DbPanelDst))) {
    Copy-Item $DbPanelSrc $DbPanelDst -Force
    Write-Host "   OK MepDbToolPanel.cs (He nuoc / PCCC)"
}

Write-Host "   Xong water/PCCC patch."
