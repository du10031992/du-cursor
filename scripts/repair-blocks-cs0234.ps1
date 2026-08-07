# Sua loi CS0234: doi ten file gay loi de SDK-style khong compile.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$blocksRoot = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD"
if (-not (Test-Path $blocksRoot)) {
    Write-Host "   (bo qua - khong co MepPanel.Blocks.AutoCAD)"
    exit 0
}

$brokenNames = @(
    "PowerBlockLibraryCommands.cs",
    "ReferenceDrawingInspector.cs"
)

$found = Get-ChildItem -Path $blocksRoot -Recurse -Filter "*.cs" -File -ErrorAction SilentlyContinue |
    Where-Object { $brokenNames -contains $_.Name }

if (-not $found) {
    Write-Host "   Khong tim thay file CS0234 (da xu ly hoac khong co)."
}
else {
    foreach ($f in $found) {
        $dest = $f.FullName + ".exclude"
        if (Test-Path $dest) {
            Remove-Item $f.FullName -Force
            Write-Host "   Xoa trung: $($f.Name)"
        }
        else {
            Rename-Item -Path $f.FullName -NewName ($f.Name + ".exclude") -Force
            Write-Host "   Rename -> $($f.Name).exclude"
        }
    }
}

# Dong thoi exclude trong csproj (phong huu)
$csproj = Join-Path $blocksRoot "MepPanel.Blocks.AutoCAD.csproj"
if (Test-Path $csproj) {
    $content = Get-Content $csproj -Raw
    $changed = $false
    foreach ($name in $brokenNames) {
        if ($content -match [regex]::Escape("Compile Remove=`"$name`"")) { continue }
        $item = @"

  <ItemGroup>
    <Compile Remove="$name" />
    <Compile Remove="**\$name" />
    <None Include="$name.exclude" Condition="Exists('$name.exclude')" />
  </ItemGroup>
"@
        if ($content -match '</Project>') {
            $content = $content -replace '</Project>', ($item + "`r`n</Project>")
            $changed = $true
        }
    }
    if ($changed) {
        Set-Content -Path $csproj -Value $content -Encoding UTF8
        Write-Host "   OK cap nhat csproj exclude"
    }
}

Write-Host "   Xong repair CS0234."
