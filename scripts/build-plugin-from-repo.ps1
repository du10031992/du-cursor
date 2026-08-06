# Build plugin tu source trong repo (MepPanel.Plugin + Blocks + Licensing)
# Chay tren Windows PowerShell co AutoCAD 2021 + .NET SDK.
#
# Build + cap nhat bundle + cai AutoCAD:
#   .\scripts\build-plugin-from-repo.ps1
#
# Chi build, khong cai:
#   .\scripts\build-plugin-from-repo.ps1 -SkipInstall

param(
    [string]$AutoCadDir,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "0.14.0",
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $Root "plugin.local.json"
$BundleContents = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents"
$BundleRoot = Join-Path $Root "bundle\MepPanel.Plugin.bundle"
$PackageContentsPath = Join-Path $BundleRoot "PackageContents.xml"
$ManifestPath = Join-Path $BundleContents "plugin.manifest.json"
$OutDir = Join-Path $Root "src\MepPanel.AutoCAD\bin\$Configuration"

function Read-LocalConfig {
    if (-not (Test-Path $LocalConfig)) { return $null }
    return Get-Content $LocalConfig -Raw | ConvertFrom-Json
}

$config = Read-LocalConfig
if (-not $AutoCadDir -and $config -and $config.autoCadDir) {
    $AutoCadDir = $config.autoCadDir
}
if ($config -and $config.configuration -and -not $PSBoundParameters.ContainsKey("Configuration")) {
    $Configuration = $config.configuration
}

$Projects = @(
    "src\MepPanel.Core\MepPanel.Core.csproj",
    "src\MepPanel.AutoCAD.Licensing\MepPanel.AutoCAD.Licensing.csproj",
    "src\MepPanel.Blocks.AutoCAD\MepPanel.Blocks.AutoCAD.csproj",
    "src\MepPanel.AutoCAD\MepPanel.AutoCAD.csproj"
)

Write-Host "==> Build plugin tu repo: $Root"
Write-Host "==> Configuration: $Configuration x64"
if ($AutoCadDir) { Write-Host "==> AutoCadDir: $AutoCadDir" }

Push-Location $Root
foreach ($rel in $Projects) {
    $proj = Join-Path $Root $rel
    if (-not (Test-Path $proj)) {
        Pop-Location
        throw "Khong tim thay project: $proj"
    }

    $args = @("build", $proj, "-c", $Configuration, "-p:Platform=x64")
    if ($AutoCadDir) { $args += "-p:AutoCadDir=$AutoCadDir" }

    Write-Host "==> dotnet build $rel"
    dotnet @args
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        throw "Build that bai: $rel"
    }
}
Pop-Location

$RequiredOutputs = @(
    "MepPanel.Plugin.dll",
    "MepPanel.AutoCAD.Licensing.dll",
    "MepPanel.Blocks.AutoCAD.dll",
    "MepPanel.Core.dll"
)

foreach ($file in $RequiredOutputs) {
    $path = Join-Path $OutDir $file
    if (-not (Test-Path $path)) {
        throw "Thieu file build: $path`nKiem tra AutoCadDir va .NET Framework 4.8 SDK."
    }
}

Write-Host "==> Copy DLL vao bundle"
New-Item -ItemType Directory -Force -Path $BundleContents | Out-Null
foreach ($file in $RequiredOutputs) {
    $src = Join-Path $OutDir $file
    $dest = Join-Path $BundleContents $file
    Copy-Item $src $dest -Force
    Unblock-File $dest -ErrorAction SilentlyContinue
    Write-Host "   OK $file"
}

# PDB (optional)
foreach ($file in @("MepPanel.Plugin.pdb", "MepPanel.Blocks.AutoCAD.pdb")) {
    $src = Join-Path $OutDir $file
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $BundleContents $file) -Force
    }
}

$configExample = Join-Path $BundleContents "MepPanel.config.json.example"
$configPath = Join-Path $BundleContents "MepPanel.config.json"
if (-not (Test-Path $configPath) -and (Test-Path $configExample)) {
    Copy-Item $configExample $configPath
}

Write-Host "==> Cap nhat PackageContents.xml (entry: MepPanel.Plugin.dll)"
if (Test-Path $PackageContentsPath) {
    [xml]$xml = Get-Content $PackageContentsPath
    $entry = $xml.ApplicationPackage.Components.ComponentEntry
    $entry.ModuleName = "./Contents/MepPanel.Plugin.dll"
    $entry.AppDescription = "MepPanel MEP Plugin v$Version (repo build)"
    $xml.ApplicationPackage.AppVersion = $Version
    $xml.Save($PackageContentsPath)
}

if (Test-Path $ManifestPath) {
    $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $manifest.version = $Version
    $manifest.entryDll = "MepPanel.Plugin.dll"
    $manifest.requiredDlls = @(
        @{ file = "MepPanel.Plugin.dll"; role = "main"; description = "Plugin loader: MEPDB, MEP DRAWING TOOL" },
        @{ file = "MepPanel.AutoCAD.Licensing.dll"; role = "licensing"; description = "License + login" },
        @{ file = "MepPanel.Blocks.AutoCAD.dll"; role = "tools"; description = "Panel va drawing services" },
        @{ file = "MepPanel.Core.dll"; role = "shared"; description = "Core features" }
    )
    $manifest | ConvertTo-Json -Depth 6 | Set-Content $ManifestPath -Encoding UTF8
}

Write-Host ""
Write-Host "Da build xong. Bundle: $BundleContents"
Write-Host "Lenh AutoCAD: MEPDB, MEPHVAC, MEPLOGIN, MEPSTATUS, MEPLOGOUT"
Write-Host "Dev mode: dat devMode=true trong MepPanel.config.json de bo qua license server"

if ($SkipInstall) {
    Write-Host "Bo qua cai dat (-SkipInstall)."
    exit 0
}

& (Join-Path $Root "scripts\install-plugin-bundle.ps1") -Configuration $Configuration
