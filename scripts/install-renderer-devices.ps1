# Copy anh thiet bi AI vao bundle plugin (Windows PowerShell).
# KHONG can pip de chay script nay - chi copy file PNG.
# pip/pillow chi can khi test render_cabinet.py ngoai AutoCAD.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$SrcDevices = Join-Path $Root "renderer\devices"
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$DstDevices = Join-Path $BundleContents "devices"
$DstScript = Join-Path $BundleContents "render_cabinet.py"

if (-not (Test-Path $SrcDevices)) {
    throw @"
Khong tim thay thu muc renderer\devices.

Chay:
  git fetch origin
  git checkout cursor/license-admin-device-control-cc24
  git pull origin cursor/license-admin-device-control-cc24
"@
}

$pngCount = (Get-ChildItem $SrcDevices -Filter *.png).Count
if ($pngCount -eq 0) {
    throw "Khong co file PNG trong $SrcDevices"
}

Write-Host "==> Cai anh thiet bi AI ($pngCount file PNG)"

New-Item -ItemType Directory -Force -Path $DstDevices | Out-Null
Copy-Item (Join-Path $SrcDevices "*.png") $DstDevices -Force
Copy-Item (Join-Path $Root "renderer\render_cabinet.py") $DstScript -Force
$DstInterior = Join-Path $BundleContents "render_cabinet_interior.py"
$DstPhotoreal = Join-Path $BundleContents "render_photoreal.py"
$DstBlenderWrap = Join-Path $BundleContents "render_blender.py"
$DstBlenderDir = Join-Path $BundleContents "blender"
Copy-Item (Join-Path $Root "renderer\render_cabinet_interior.py") $DstInterior -Force
Copy-Item (Join-Path $Root "renderer\render_photoreal.py") $DstPhotoreal -Force
Copy-Item (Join-Path $Root "renderer\render_blender.py") $DstBlenderWrap -Force
New-Item -ItemType Directory -Force -Path $DstBlenderDir | Out-Null
Copy-Item (Join-Path $Root "renderer\blender\build_scene.py") (Join-Path $DstBlenderDir "build_scene.py") -Force
Write-Host "   -> bundle: $DstDevices"

$installDir = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle\Contents"
if (Test-Path $installDir) {
    $autoDevices = Join-Path $installDir "devices"
    New-Item -ItemType Directory -Force -Path $autoDevices | Out-Null
    Copy-Item (Join-Path $SrcDevices "*.png") $autoDevices -Force
    Copy-Item (Join-Path $Root "renderer\render_cabinet.py") (Join-Path $installDir "render_cabinet.py") -Force
    Copy-Item (Join-Path $Root "renderer\render_cabinet_interior.py") (Join-Path $installDir "render_cabinet_interior.py") -Force
    Copy-Item (Join-Path $Root "renderer\render_photoreal.py") (Join-Path $installDir "render_photoreal.py") -Force
    Copy-Item (Join-Path $Root "renderer\render_blender.py") (Join-Path $installDir "render_blender.py") -Force
    $autoBlender = Join-Path $installDir "blender"
    New-Item -ItemType Directory -Force -Path $autoBlender | Out-Null
    Copy-Item (Join-Path $Root "renderer\blender\build_scene.py") (Join-Path $autoBlender "build_scene.py") -Force
    Write-Host "   -> AutoCAD: $autoDevices"
}
else {
    Write-Host "   (Chua cai plugin AutoCAD - chay install-plugin-bundle.ps1 truoc.)"
}

Write-Host ""
Write-Host "Xong! Khoi dong lai AutoCAD, thu Render tu dien."
Write-Host ""
Write-Host "Test render ngoai AutoCAD (tuy chon):"
Write-Host '  py -m pip install pillow'
Write-Host ""
Write-Host 'Render 3D (can Blender - xem scripts\install-blender-render.ps1):'
Write-Host '  py renderer\render_cabinet.py --demo --quality blender --samples 256 --output cabinet.png'
Write-Host ""
Write-Host 'Fallback nhanh (Pillow, khong can Blender):'
Write-Host '  py renderer\render_cabinet.py --demo --quality photoreal --output cabinet.png'
