# Sua loi CS0234 tren MepPanel.Blocks.AutoCAD (file thua / thieu reference).
# Neu PowerBlockLibraryCommands.cs / ReferenceDrawingInspector.cs gay loi
# "MepPanel.AutoCAD does not exist" -> loai khoi compile tam thoi.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$csproj = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\MepPanel.Blocks.AutoCAD.csproj"
if (-not (Test-Path $csproj)) {
    Write-Host "   (bo qua - khong co Blocks.AutoCAD.csproj)"
    exit 0
}

$broken = @(
    "PowerBlockLibraryCommands.cs",
    "ReferenceDrawingInspector.cs"
)

$xml = [xml](Get-Content $csproj -Raw)
$ns = $xml.Project.GetAttribute("xmlns")
$changed = $false

# SDK-style: them ItemGroup Compile Remove
$content = Get-Content $csproj -Raw
foreach ($file in $broken) {
    $path = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\$file"
    $pathAlt = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\Drawing\$file"
    $exists = (Test-Path $path) -or (Test-Path $pathAlt)
    if (-not $exists) { continue }

    if ($content -match [regex]::Escape("Compile Remove=`"$file`"")) {
        Write-Host "   Da exclude: $file"
        continue
    }

    # Them vao cuoi Project
    $item = @"

  <ItemGroup>
    <!-- CS0234: thieu reference MepPanel.AutoCAD - exclude tam -->
    <Compile Remove="$file" />
    <Compile Remove="**\$file" />
  </ItemGroup>
"@
    if ($content -match '</Project>') {
        $content = $content -replace '</Project>', ($item + "`r`n</Project>")
        $changed = $true
        Write-Host "   Exclude khoi build: $file"
    }
}

if ($changed) {
    Set-Content -Path $csproj -Value $content -Encoding UTF8
    Write-Host "   OK cap nhat $csproj"
}
else {
    Write-Host "   Khong can sua csproj (hoac da exclude)."
}
