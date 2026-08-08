# Ghi de AutoCadCommandDispatcher + MepInternalCommandAttribute (moi build).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\UI"

$dst = $null
foreach ($c in @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui")
)) {
    if (Test-Path $c) { $dst = $c; break }
}
if (-not $dst) {
    $dst = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
}

Write-Host "==> Copy UI dispatcher (AutoCadCommandDispatcher)"
foreach ($name in @("MepInternalCommandAttribute.cs", "AutoCadCommandDispatcher.cs", "MepDocumentContext.cs")) {
    $src = Join-Path $srcDir $name
    if (-not (Test-Path $src)) {
        if ($name -eq "MepDocumentContext.cs") { continue }
        throw "Thieu patch: $src"
    }
    Copy-Item $src (Join-Path $dst $name) -Force
    Write-Host "   OK $name"
}

$disp = Join-Path $dst "AutoCadCommandDispatcher.cs"
$text = Get-Content $disp -Raw -Encoding UTF8
if ($text -match '\bApplication\.DocumentManager\b' -and $text -notmatch 'AcApp\.DocumentManager') {
    throw "AutoCadCommandDispatcher van dung Application.DocumentManager (CS0104). Pull repo moi."
}
if ($text -match 'using\s+Autodesk\.AutoCAD\.ApplicationServices\.Core\s*;' -and
    $text -match 'using\s+Autodesk\.AutoCAD\.ApplicationServices\s*;') {
    throw "AutoCadCommandDispatcher co 2 namespace Application (CS0104). Pull repo moi."
}

Write-Host "   Dispatcher OK (AcApp, khong ambiguous Application)"
