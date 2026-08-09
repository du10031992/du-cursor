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
foreach ($name in @(
    "MepInternalCommandAttribute.cs",
    "AutoCadCommandDispatcher.cs",
    "MepDocumentContext.cs",
    "HvacConfigurationWindow.xaml.cs",
    "PanelConfigurationWindow.xaml.cs",
    "ElectricalToolControl.xaml.cs",
    "ElectricalToolControl.xaml"
)) {
    $src = Join-Path $srcDir $name
    if (-not (Test-Path $src)) {
        if ($name -eq "MepDocumentContext.cs") { continue }
        throw "Thieu patch: $src"
    }
    Copy-Item $src (Join-Path $dst $name) -Force
    Write-Host "   OK $name"
}

$featureUiSrc = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Licensing\PluginFeatureUi.cs"
if (Test-Path $featureUiSrc) {
    $featureUiDst = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Licensing"
    New-Item -ItemType Directory -Force -Path $featureUiDst | Out-Null
    Copy-Item $featureUiSrc (Join-Path $featureUiDst "PluginFeatureUi.cs") -Force
    Write-Host "   OK PluginFeatureUi.cs"
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

# --- Guard chong build code CU (repo working tree bi stale) ---
# Dispatcher phai di qua MepDocumentContext.Run de set scope depth 1 lan.
if ($text -notmatch 'MepDocumentContext\.Run\(\(\)\s*=>\s*InvokeCommand') {
    throw @"
AutoCadCommandDispatcher.cs la BAN CU (chua co MepDocumentContext.Run -> InvokeCommand).
=> Repo du-cursor dang stale. Chay:
     git fetch origin
     git checkout -B cursor/water-pccc-visual-cc24 origin/cursor/water-pccc-visual-cc24
     git reset --hard origin/cursor/water-pccc-visual-cc24
   roi apply lai.
"@
}

$ctx = Join-Path $dst "MepDocumentContext.cs"
if (Test-Path $ctx) {
    $ctxText = Get-Content $ctx -Raw -Encoding UTF8
    if ($ctxText -notmatch '\[ThreadStatic\]' -or $ctxText -notmatch '_depth') {
        throw @"
MepDocumentContext.cs la BAN CU (thieu guard _depth chong long command context).
=> Repo du-cursor dang stale. Chay:
     git fetch origin
     git checkout -B cursor/water-pccc-visual-cc24 origin/cursor/water-pccc-visual-cc24
     git reset --hard origin/cursor/water-pccc-visual-cc24
   roi apply lai.
"@
    }
    Write-Host "   MepDocumentContext OK (co guard _depth)"
}

Write-Host "   eLock guard OK: Queue -> 1 command context; khong nested LockDocument"
