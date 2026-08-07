# Dam bao MepPanelMvp co project Blocks.AutoCAD (ve ong + thu vien AMC).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$dstRoot = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD"

if (-not (Test-Path $dstRoot)) {
    $srcRoot = Join-Path $Root "src\MepPanel.Blocks.AutoCAD"
    if (-not (Test-Path $srcRoot)) {
        Write-Host "   (bo qua - khong co src\MepPanel.Blocks.AutoCAD trong repo)"
        exit 0
    }

    Write-Host "==> Tao MepPanel.Blocks.AutoCAD trong MepPanelMvp"
    New-Item -ItemType Directory -Force -Path (Split-Path $dstRoot) | Out-Null
    Copy-Item $srcRoot $dstRoot -Recurse -Force
    Write-Host "   OK copy project Blocks.AutoCAD"
}

$patchDraw = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Blocks.AutoCAD\Drawing"
$dstDraw = Join-Path $dstRoot "Drawing"
if (Test-Path $patchDraw) {
    New-Item -ItemType Directory -Force -Path $dstDraw | Out-Null
    Copy-Item (Join-Path $patchDraw "*.cs") $dstDraw -Force
    Write-Host "   OK cap nhat Drawing (water/PCCC/AMC)"
}

$patchPanel = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Blocks.AutoCAD\MepDbToolPanel.cs"
if ((Test-Path $patchPanel) -and (Test-Path $dstRoot)) {
    Copy-Item $patchPanel (Join-Path $dstRoot "MepDbToolPanel.cs") -Force
}

# AutoCAD.csproj can tham chieu Blocks (neu chua co).
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
        Write-Host "   OK them ProjectReference Blocks.AutoCAD vao MepPanel.AutoCAD.csproj"
    }
}

Write-Host "   Blocks project san sang build."
