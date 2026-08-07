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
$ConfirmPreference = "None"
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
    # Khong ghi de LicenseApiClient.cs / file .txt khoi phuc — repair script xu ly.
    Get-ChildItem (Join-Path $PatchLicensing "*") -File | ForEach-Object {
        if ($_.Name -eq 'LicenseApiClient.cs' -or $_.Extension -eq '.txt') {
            Write-Host "   (bo qua copy $($_.Name))"
            return
        }
        # LicenseDtos / DeviceIdentity: chi copy neu chua co type (tranh CS0101)
        if ($_.Name -eq 'LicenseDtos.cs' -or $_.Name -eq 'DeviceIdentity.cs') {
            return
        }
        Copy-Item $_.FullName (Join-Path $TargetLicensing $_.Name) -Force
    }
}

$TlsPatch = Join-Path $Root "scripts\apply-license-api-tls-patch.ps1"
if (Test-Path $TlsPatch) {
    & $TlsPatch -PluginSourceRoot $PluginSourceRoot
}

$PatchCoreFeatures = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Core\PluginFeatures.cs"
$TargetCoreFeatures = Join-Path $PluginSourceRoot "src\MepPanel.Core\PluginFeatures.cs"
if (Test-Path $PatchCoreFeatures) {
    Write-Host "==> Apply patch PluginFeatures (MEPDB entry + sub-features)"
    New-Item -ItemType Directory -Force -Path (Split-Path $TargetCoreFeatures) | Out-Null
    Copy-Item $PatchCoreFeatures $TargetCoreFeatures -Force
}

Write-Host "==> build-plugin-release.ps1 [no-standalone-Blocks] photoreal render"

$CabinetRenderPatch = Join-Path $Root "scripts\apply-cabinet-render-patch.ps1"
$RepairBlocks = Join-Path $Root "scripts\repair-blocks-cs0234.ps1"
if (Test-Path $RepairBlocks) {
    Write-Host "==> Repair Blocks.AutoCAD CS0234 (rename file loi)"
    & $RepairBlocks -PluginSourceRoot $PluginSourceRoot
}
if (Test-Path $CabinetRenderPatch) {
    Write-Host "==> Apply patch MepCabinetRenderService (Pillow photoreal)"
    & $CabinetRenderPatch -PluginSourceRoot $PluginSourceRoot
}

$WaterPcccPatch = Join-Path $Root "scripts\apply-water-pccc-patch.ps1"
if (Test-Path $WaterPcccPatch) {
    Write-Host "==> Apply patch He nuoc / PCCC (ve + phan tich + render PNG)"
    & $WaterPcccPatch -PluginSourceRoot $PluginSourceRoot
}

$BlocksProjectPatch = Join-Path $Root "scripts\apply-blocks-project-patch.ps1"
if (Test-Path $BlocksProjectPatch) {
    & $BlocksProjectPatch -PluginSourceRoot $PluginSourceRoot
}

$EnableWaterFireUi = Join-Path $Root "scripts\apply-enable-water-fire-ui.ps1"
$RepairXaml = Join-Path $Root "scripts\repair-electrical-tool-xaml.ps1"
if (Test-Path $EnableWaterFireUi) {
    Write-Host "==> Bat nut HE NUOC / BAO CHAY tren panel WPF"
    & $EnableWaterFireUi -PluginSourceRoot $PluginSourceRoot
}
elseif (Test-Path $RepairXaml) {
    Write-Host "   (chi co repair script - bo qua enable UI)"
}

$FeaturePatch = Join-Path $Root "scripts\apply-feature-guard-patch.ps1"
$SingleEntryPatch = Join-Path $Root "scripts\apply-single-entry-patch.ps1"
$RestoreSingleEntry = Join-Path $Root "scripts\restore-single-entry-patch.ps1"
$TypesPatch = Join-Path $Root "scripts\apply-licensing-types-patch.ps1"
if (Test-Path $TypesPatch) {
    & $TypesPatch -PluginSourceRoot $PluginSourceRoot
}

$RepairCorruptedCommands = Join-Path $Root "scripts\repair-corrupted-command-methods.ps1"
if (Test-Path $RepairCorruptedCommands) {
    & $RepairCorruptedCommands -PluginSourceRoot $PluginSourceRoot
}

# Dispatcher UI: luon ghi de de tranh CS0104 Application ambiguous tren source cu.
$UiDispatcherPatch = Join-Path $Root "scripts\apply-ui-dispatcher-patch.ps1"
if (Test-Path $UiDispatcherPatch) {
    & $UiDispatcherPatch -PluginSourceRoot $PluginSourceRoot
}

# Single-entry: CLI chi MEPDB; lenh phu -> MepInternalCommand (panel van goi duoc).
if (Test-Path $SingleEntryPatch) {
    Write-Host "==> Single-entry: chi lenh MEPDB tren command line"
    & $SingleEntryPatch -PluginSourceRoot $PluginSourceRoot
}

if (Test-Path $FeaturePatch) {
    Write-Host "==> MEPDB entry -> EnsureEntry() (dang nhap truoc khi mo panel)"
    & $FeaturePatch -PluginSourceRoot $PluginSourceRoot
}

$SubFeaturePatch = Join-Path $Root "scripts\apply-subfeature-guard-patch.ps1"
$RestoreSubFeature = Join-Path $Root "scripts\restore-subfeature-patch.ps1"
$RepairSource = Join-Path $Root "scripts\repair-plugin-source.ps1"

# Dam bao source khong bi hong; subfeature guard (khoa nut Admin) bat khi can.
if (Test-Path $RepairSource) {
    & $RepairSource -PluginSourceRoot $PluginSourceRoot
}

# SUBFEATURE GUARD: bat neu muon Admin khoa tung nut trong panel.
# if (Test-Path $SubFeaturePatch) { & $SubFeaturePatch -PluginSourceRoot $PluginSourceRoot }
# $WindowGuard = Join-Path $Root "scripts\apply-window-entry-guard.ps1"
# if (Test-Path $WindowGuard) { & $WindowGuard -PluginSourceRoot $PluginSourceRoot }
# $ElectricalGuards = Join-Path $Root "scripts\apply-electrical-tool-guards.ps1"
# if (Test-Path $ElectricalGuards) { & $ElectricalGuards -PluginSourceRoot $PluginSourceRoot }

foreach ($proj in @($AutoCadProj, $CoreProj)) {
    if (-not (Test-Path $proj)) {
        throw "Khong tim thay project: $proj`nKiem tra pluginSourceRoot trong plugin.local.json"
    }
}

# KHONG build Blocks.AutoCAD rieng - project nay thuong thieu reference (CS0234).
# AutoCAD.csproj se build Blocks nhu dependency neu can.
# Patch MepCabinetRenderService da apply o tren.

$BlocksProj = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\MepPanel.Blocks.AutoCAD.csproj"

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

$Preflight = Join-Path $Root "scripts\preflight-plugin-build.ps1"
if (Test-Path $Preflight) {
    & $Preflight -PluginSourceRoot $PluginSourceRoot
}

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

if (Test-Path $BlocksProj) {
    Write-Host "==> Build MepPanel.Blocks.AutoCAD (ve ong + thu vien AMC)"
    $blocksBuildArgs = @(
        "build",
        $BlocksProj,
        "-c", $Configuration,
        "-p:Platform=x64"
    )
    if ($AutoCadDir) {
        $blocksBuildArgs += "-p:AutoCadDir=$AutoCadDir"
    }
    dotnet @blocksBuildArgs
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        throw "Build MepPanel.Blocks.AutoCAD that bai."
    }
}
else {
    Write-Host "   (bo qua Blocks.AutoCAD - khong co project)"
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
$blocksOut = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\bin\x64\$Configuration"
if (-not (Test-Path (Join-Path $blocksOut "MepPanel.Blocks.AutoCAD.dll"))) {
    $blocksOut = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\bin\$Configuration"
}
$blocksDirs = @($blocksOut, $AutoCadOut, $AutoCadAlt)

$outputs = @(
    @{ Name = "MepPanel.AutoCAD.dll"; Dirs = $acadDirs },
    @{ Name = "MepPanel.AutoCAD.pdb"; Dirs = $acadDirs },
    @{ Name = "MepPanel.Core.dll";   Dirs = $coreDirs  },
    @{ Name = "MepPanel.Core.pdb";   Dirs = $coreDirs  },
    @{ Name = "MepPanel.Blocks.AutoCAD.dll"; Dirs = $blocksDirs },
    @{ Name = "MepPanel.Blocks.AutoCAD.pdb"; Dirs = $blocksDirs }
)

Write-Host "==> Copy DLL vao bundle"
New-Item -ItemType Directory -Force -Path $BundleContents | Out-Null

$TemplateSrc = Join-Path $Root "assets\templates\AMC_TEMPLATE_RV29.dwg"
$TemplateDstDir = Join-Path $BundleContents "samples\templates"
if (Test-Path $TemplateSrc) {
    New-Item -ItemType Directory -Force -Path $TemplateDstDir | Out-Null
    Copy-Item $TemplateSrc (Join-Path $TemplateDstDir "AMC_TEMPLATE_RV29.dwg") -Force
    Write-Host "   OK AMC_TEMPLATE_RV29.dwg -> samples/templates"
}

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
    Write-Host ("   OK " + $item.Name + " from " + $src)
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
        $replacement = 'AppVersion="' + $Version + '"'
        $xml = $xml -replace 'AppVersion="[^"]*"', $replacement
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

$InstallRenderer = Join-Path $Root "scripts\install-renderer-devices.ps1"
if (Test-Path $InstallRenderer) {
    & $InstallRenderer
}
