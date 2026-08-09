# Copy anh thiet bi AI + renderer Pillow (tu dien + he nuoc/PCCC) vao bundle plugin.
# KHONG can pip de chay script nay - chi copy file.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$SrcDevices = Join-Path $Root "renderer\devices"
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$DstDevices = Join-Path $BundleContents "devices"

$RendererScripts = @(
    "render_cabinet.py",
    "render_cabinet_interior.py",
    "render_photoreal.py",
    "render_pipe_system.py"
)

if (-not (Test-Path $SrcDevices)) {
    throw @"
Khong tim thay thu muc renderer\devices.

Chay:
  git fetch origin
  git checkout cursor/water-pccc-visual-cc24
  git pull origin cursor/water-pccc-visual-cc24
"@
}

$pngCount = (Get-ChildItem $SrcDevices -Filter *.png).Count
if ($pngCount -eq 0) {
    throw "Khong co file PNG trong $SrcDevices"
}

Write-Host "==> Cai renderer Pillow + anh thiet bi ($pngCount PNG)"

New-Item -ItemType Directory -Force -Path $DstDevices | Out-Null
Copy-Item (Join-Path $SrcDevices "*.png") $DstDevices -Force
foreach ($name in $RendererScripts) {
    $src = Join-Path $Root "renderer\$name"
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $BundleContents $name) -Force
        Write-Host "   -> $name"
    }
}
Write-Host "   -> bundle: $DstDevices"

. (Join-Path $PSScriptRoot "plugin-paths.ps1")
$installContents = Get-PluginInstallContentsPath -RepoRoot $Root
if (Test-Path $installContents) {
    $autoDevices = Join-Path $installContents "devices"
    New-Item -ItemType Directory -Force -Path $autoDevices | Out-Null
    Copy-Item (Join-Path $SrcDevices "*.png") $autoDevices -Force
    foreach ($name in $RendererScripts) {
        $src = Join-Path $Root "renderer\$name"
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $installContents $name) -Force
        }
    }
    Write-Host "   -> plugin: $autoDevices"
}
else {
    Write-Host "   (Chua cai plugin - chay install-plugin-bundle.ps1 truoc.)"
}

Write-Host ""
Write-Host "Xong! Khoi dong lai AutoCAD, thu Render tu dien / he nuoc / PCCC."
Write-Host ""

. (Join-Path $PSScriptRoot "Find-MepPython.ps1")
$py = Get-MepPython
if ($py) {
    $run = if ($py.Prefix) { "$($py.Exe) $($py.Prefix)".Trim() } else { $py.Exe }
    Write-Host "Python: $run"
    Write-Host "Test render:"
    Write-Host ("  {0} renderer\render_cabinet.py --demo --quality photoreal --output cabinet.png" -f $run)
    Write-Host ("  {0} renderer\render_pipe_system.py --system water --demo --output water.png" -f $run)
    Write-Host ("  {0} renderer\render_pipe_system.py --system fire --demo --output fire.png" -f $run)
}
else {
    Write-Host "Neu muon test ngoai AutoCAD: cai Python 3 + pillow"
    Write-Host "  python -m pip install pillow"
    Write-Host "  python renderer\render_pipe_system.py --system water --demo --output water.png"
}
