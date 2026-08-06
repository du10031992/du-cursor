# Copy anh thiet bi AI vao bundle plugin (Windows PowerShell).
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$SrcDevices = Join-Path $Root "renderer\devices"
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$DstDevices = Join-Path $BundleContents "devices"
$DstScript = Join-Path $BundleContents "render_cabinet.py"

if (-not (Test-Path $SrcDevices)) {
    throw "Khong tim thay $SrcDevices"
}

New-Item -ItemType Directory -Force -Path $DstDevices | Out-Null
Copy-Item (Join-Path $SrcDevices "*.png") $DstDevices -Force
Copy-Item (Join-Path $Root "renderer\render_cabinet.py") $DstScript -Force

$installDir = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle\Contents"
if (Test-Path $installDir) {
    New-Item -ItemType Directory -Force -Path (Join-Path $installDir "devices") | Out-Null
    Copy-Item (Join-Path $SrcDevices "*.png") (Join-Path $installDir "devices") -Force
    Copy-Item (Join-Path $Root "renderer\render_cabinet.py") (Join-Path $installDir "render_cabinet.py") -Force
    Write-Host "Da cap nhat devices + render_cabinet.py -> $installDir"
}
else {
    Write-Host "Da copy vao bundle. Chay install-plugin-bundle.ps1 de cai AutoCAD."
}

Write-Host "Xong. Khoi dong lai AutoCAD va thu Render tu dien."
