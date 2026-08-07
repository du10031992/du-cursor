# Dong bo TOAN BO MepPanel.Blocks.AutoCAD tu repo -> MepPanelMvp (moi lan build).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$srcRoot = Join-Path $Root "src\MepPanel.Blocks.AutoCAD"
$patchRoot = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Blocks.AutoCAD"
$dstRoot = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD"

if (-not (Test-Path $srcRoot) -and -not (Test-Path $patchRoot)) {
    Write-Host "   (bo qua - khong co Blocks.AutoCAD trong repo)"
    exit 0
}

function Sync-Tree {
    param(
        [string]$Source,
        [string]$Dest
    )

    New-Item -ItemType Directory -Force -Path $Dest | Out-Null

    Get-ChildItem -Path $Source -File -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $Dest $_.Name) -Force
    }

    Get-ChildItem -Path $Source -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notin @('bin', 'obj', '.vs') } |
        ForEach-Object {
            Sync-Tree -Source $_.FullName -Dest (Join-Path $Dest $_.Name)
        }
}

Write-Host "==> Dong bo MepPanel.Blocks.AutoCAD (full project tu repo)"
if (Test-Path $srcRoot) {
    Sync-Tree -Source $srcRoot -Dest $dstRoot
}

if (Test-Path $patchRoot) {
    Sync-Tree -Source $patchRoot -Dest $dstRoot
    Write-Host "   OK overlay patches\MepPanel.Blocks.AutoCAD"
}

$requiredFiles = @(
    "MepPanel.Blocks.AutoCAD.csproj",
    "MepDbToolPanel.cs",
    "Drawing\MepPipeLibraryService.cs",
    "Drawing\MepWaterDrawingService.cs",
    "Drawing\MepDrawingHelper.cs"
)

$missing = @()
foreach ($rel in $requiredFiles) {
    if (-not (Test-Path (Join-Path $dstRoot $rel))) {
        $missing += $rel
    }
}

if ($missing.Count -gt 0) {
    throw @"
Thieu file Blocks.AutoCAD sau khi dong bo:
  $($missing -join "`n  ")

Chay: git pull origin cursor/water-pccc-visual-cc24
     .\scripts\apply-blocks-project-patch.ps1 -PluginSourceRoot C:\MepPanel\MepPanelMvp
"@
}

Write-Host "   OK $($requiredFiles.Count) file bat buoc"

# AutoCAD.csproj tham chieu Blocks (neu chua co).
$acadProj = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\MepPanel.AutoCAD.csproj"
$blocksProj = Join-Path $dstRoot "MepPanel.Blocks.AutoCAD.csproj"
if ((Test-Path $acadProj) -and (Test-Path $blocksProj)) {
    $projText = Get-Content $acadProj -Raw -Encoding UTF8
    if ($projText -notmatch 'MepPanel\.Blocks\.AutoCAD\.csproj') {
        $ref = @"

  <ItemGroup>
    <ProjectReference Include="..\MepPanel.Blocks.AutoCAD\MepPanel.Blocks.AutoCAD.csproj" />
  </ItemGroup>
"@
        $projText = $projText -replace '</Project>', ($ref + "`r`n</Project>")
        Set-Content -Path $acadProj -Value $projText -Encoding UTF8 -NoNewline
        Write-Host "   OK them ProjectReference Blocks.AutoCAD"
    }
}

Write-Host "   Blocks project san sang build."
