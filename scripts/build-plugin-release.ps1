# Build plugin MepPanel tu source goc (MepPanel.AutoCAD.dll) va cap nhat bundle.
# Chay tu thu muc repo tren Windows PowerShell.
#
# Lan dau:
#   copy plugin.local.json.example plugin.local.json
#   sua pluginSourceRoot tro toi thu muc MepPanelMvp co day du source
#
# Build + cai:
#   .\scripts\build-plugin-release.ps1
#
# Chi build, khong cai AutoCAD:
#   .\scripts\build-plugin-release.ps1 -SkipInstall

param(
    [string]$PluginSourceRoot,
    [string]$AutoCadDir,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version,
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $Root "plugin.local.json"
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$ManifestPath = Join-Path $BundleContents "plugin.manifest.json"
$PackageContentsPath = Join-Path $Root "bundle\MepPanel.Plugin.bundle\PackageContents.xml"

function Read-LocalConfig {
    if (-not (Test-Path $LocalConfig)) {
        return $null
    }
    return Get-Content $LocalConfig -Raw | ConvertFrom-Json
}

$config = Read-LocalConfig
if (-not $PluginSourceRoot -and $config -and $config.pluginSourceRoot) {
    $PluginSourceRoot = $config.pluginSourceRoot
}
if (-not $AutoCadDir -and $config -and $config.autoCadDir) {
    $AutoCadDir = $config.autoCadDir
}
if ($config -and $config.configuration -and -not $PSBoundParameters.ContainsKey("Configuration")) {
    $Configuration = $config.configuration
}

if (-not $PluginSourceRoot) {
    throw @"
Chua cau hinh duong dan source plugin.

1. copy plugin.local.json.example plugin.local.json
2. Sua pluginSourceRoot tro toi thu muc MepPanelMvp (co src\MepPanel.AutoCAD, src\MepPanel.Core)
   Vi du: C:\Users\DU_COMPUTER\Documents\Codex\2026-07-31\hay\work\MepPanelMvp

Hoac truyen truc tiep:
  .\scripts\build-plugin-release.ps1 -PluginSourceRoot `"D:\path\MepPanelMvp`"
"@
}

$PluginSourceRoot = (Resolve-Path $PluginSourceRoot).Path
$AutoCadProj = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\MepPanel.AutoCAD.csproj"
$CoreProj = Join-Path $PluginSourceRoot "src\MepPanel.Core\MepPanel.Core.csproj"

foreach ($proj in @($AutoCadProj, $CoreProj)) {
    if (-not (Test-Path $proj)) {
        throw "Khong tim thay project: $proj`nKiem tra pluginSourceRoot trong plugin.local.json"
    }
}

$buildArgs = @(
    "build",
    $AutoCadProj,
    "-c", $Configuration,
    "-p:Platform=x64"
)
if ($AutoCadDir) {
    $buildArgs += "-p:AutoCadDir=$AutoCadDir"
}

Write-Host "==> Build plugin tu source: $PluginSourceRoot"
Write-Host "==> Configuration: $Configuration x64"
Push-Location $PluginSourceRoot
dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Build MepPanel.AutoCAD that bai."
}

$coreBuildArgs = @(
    "build",
    $CoreProj,
    "-c", $Configuration,
    "-p:Platform=x64"
)
dotnet @coreBuildArgs
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Build MepPanel.Core that bai."
}
Pop-Location

$AutoCadOut = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\bin\$Configuration"
$CoreOut = Join-Path $PluginSourceRoot "src\MepPanel.Core\bin\$Configuration\net48"
if (-not (Test-Path (Join-Path $CoreOut "MepPanel.Core.dll"))) {
    $CoreOut = Join-Path $PluginSourceRoot "src\MepPanel.Core\bin\$Configuration"
}

$outputs = @(
    @{ Name = "MepPanel.AutoCAD.dll"; SourceDir = $AutoCadOut },
    @{ Name = "MepPanel.AutoCAD.pdb"; SourceDir = $AutoCadOut },
    @{ Name = "MepPanel.Core.dll"; SourceDir = $CoreOut },
    @{ Name = "MepPanel.Core.pdb"; SourceDir = $CoreOut }
)

Write-Host "==> Copy DLL vao bundle"
New-Item -ItemType Directory -Force -Path $BundleContents | Out-Null
foreach ($item in $outputs) {
    $src = Join-Path $item.SourceDir $item.Name
    if (-not (Test-Path $src)) {
        Write-Host "   (bo qua - khong co) $src"
        continue
    }
    $dest = Join-Path $BundleContents $item.Name
    Copy-Item $src $dest -Force
    Unblock-File $dest -ErrorAction SilentlyContinue
    Write-Host "   OK $($item.Name)"
}

if ($Version) {
    Write-Host "==> Cap nhat version bundle -> $Version"
    if (Test-Path $ManifestPath) {
        $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
        $manifest.version = $Version
        $manifest | ConvertTo-Json -Depth 6 | Set-Content $ManifestPath -Encoding UTF8
    }
    if (Test-Path $PackageContentsPath) {
        $xml = Get-Content $PackageContentsPath -Raw
        $xml = $xml -replace 'AppVersion="[^"]*"', "AppVersion=`"$Version`""
        Set-Content $PackageContentsPath $xml -Encoding UTF8
    }
}

Write-Host ""
Write-Host "Da build xong. Bundle: $BundleContents"

if ($SkipInstall) {
    Write-Host "Bo qua cai dat (-SkipInstall)."
    exit 0
}

& (Join-Path $Root "scripts\install-plugin-bundle.ps1")
