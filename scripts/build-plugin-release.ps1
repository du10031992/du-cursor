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

$PatchLicensing = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Licensing"
$TargetLicensing = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Licensing"
if (Test-Path $PatchLicensing) {
    Write-Host "==> Apply patch Licensing (LicenseGuard, PluginFeatureGate, ...)"
    New-Item -ItemType Directory -Force -Path $TargetLicensing | Out-Null
    Copy-Item (Join-Path $PatchLicensing "*") $TargetLicensing -Force
}

$PatchCoreFeatures = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Core\PluginFeatures.cs"
$TargetCoreFeatures = Join-Path $PluginSourceRoot "src\MepPanel.Core\PluginFeatures.cs"
if (Test-Path $PatchCoreFeatures) {
    Write-Host "==> Apply patch PluginFeatures (MEPDB entry + sub-features)"
    New-Item -ItemType Directory -Force -Path (Split-Path $TargetCoreFeatures) | Out-Null
    Copy-Item $PatchCoreFeatures $TargetCoreFeatures -Force
}

$FeaturePatch = Join-Path $Root "scripts\apply-feature-guard-patch.ps1"
$SingleEntryPatch = Join-Path $Root "scripts\apply-single-entry-patch.ps1"
$TypesPatch = Join-Path $Root "scripts\apply-licensing-types-patch.ps1"
if (Test-Path $TypesPatch) {
    & $TypesPatch -PluginSourceRoot $PluginSourceRoot
}
if (Test-Path $SingleEntryPatch) {
    & $SingleEntryPatch -PluginSourceRoot $PluginSourceRoot
}
$SubFeaturePatch = Join-Path $Root "scripts\apply-subfeature-guard-patch.ps1"
if (Test-Path $SubFeaturePatch) {
    & $SubFeaturePatch -PluginSourceRoot $PluginSourceRoot
}
if (Test-Path $FeaturePatch) {
    & $FeaturePatch -PluginSourceRoot $PluginSourceRoot
}

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

$AutoCadOut = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\bin\x64\$Configuration"
if (-not (Test-Path (Join-Path $AutoCadOut "MepPanel.AutoCAD.dll"))) {
    $AutoCadOut = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\bin\$Configuration"
}

# MepPanelMvp co the output thang vao ApplicationPlugins (output path custom)
# Tim them o day neu bin\ khong co
$AutoCadAlt = "$env:ProgramData\Autodesk\ApplicationPlugins\MepPanelMvp.bundle\Contents\Windows"

$CoreOut = Join-Path $PluginSourceRoot "src\MepPanel.Core\bin\x64\$Configuration\net48"
if (-not (Test-Path (Join-Path $CoreOut "MepPanel.Core.dll"))) {
    $CoreOut = Join-Path $PluginSourceRoot "src\MepPanel.Core\bin\$Configuration\net48"
}
if (-not (Test-Path (Join-Path $CoreOut "MepPanel.Core.dll"))) {
    $CoreOut = Join-Path $PluginSourceRoot "src\MepPanel.Core\bin\x64\$Configuration"
}
if (-not (Test-Path (Join-Path $CoreOut "MepPanel.Core.dll"))) {
    $CoreOut = Join-Path $PluginSourceRoot "src\MepPanel.Core\bin\$Configuration"
}

function Find-Dll {
    param([string]$Name, [string[]]$Dirs)
    foreach ($dir in $Dirs) {
        $p = Join-Path $dir $Name
        if (Test-Path $p) { return $p }
    }
    return $null
}

$acadDirs = @($AutoCadOut, $AutoCadAlt)
$coreDirs  = @($CoreOut)

$outputs = @(
    @{ Name = "MepPanel.AutoCAD.dll"; Dirs = $acadDirs },
    @{ Name = "MepPanel.AutoCAD.pdb"; Dirs = $acadDirs },
    @{ Name = "MepPanel.Core.dll";   Dirs = $coreDirs  },
    @{ Name = "MepPanel.Core.pdb";   Dirs = $coreDirs  }
)

Write-Host "==> Copy DLL vao bundle"
New-Item -ItemType Directory -Force -Path $BundleContents | Out-Null
$anyOk = $false
foreach ($item in $outputs) {
    $src = Find-Dll -Name $item.Name -Dirs $item.Dirs
    if (-not $src) {
        Write-Host "   (bo qua - khong tim thay) $($item.Name)"
        Write-Host "   Da tim trong: $($item.Dirs -join ', ')"
        continue
    }
    $dest = Join-Path $BundleContents $item.Name
    Copy-Item $src $dest -Force
    Unblock-File $dest -ErrorAction SilentlyContinue
    Write-Host "   OK $($item.Name) <- $src"
    if ($item.Name -eq "MepPanel.AutoCAD.dll") { $anyOk = $true }
}

if (-not $anyOk) {
    throw @"
Khong tim thay MepPanel.AutoCAD.dll sau khi build.

Tim trong:
  $($acadDirs -join "`n  ")

Kiem tra OutputPath trong MepPanel.AutoCAD.csproj cua MepPanelMvp.
"@
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
