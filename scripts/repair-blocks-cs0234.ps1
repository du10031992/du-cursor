# Sua file legacy trong MepPanel.Blocks.AutoCAD gay CS0234/CS0246.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
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
    Write-Host "   Khong tim thay file legacy CS0234 (da xu ly hoac khong co)."
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

# AutoCadAssemblyInfo.cs cu tham chieu PowerBlockLibraryCommands / MepPanelMvp.Blocks.
$assemblyInfo = Join-Path $blocksRoot "AutoCadAssemblyInfo.cs"
$patchAssemblyInfo = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Blocks.AutoCAD\AutoCadAssemblyInfo.cs"
$needsReplace = $false

if (Test-Path $assemblyInfo) {
    $text = Get-Content $assemblyInfo -Raw -Encoding UTF8
    if ($text -match 'PowerBlockLibraryCommands|MepPanelMvp\.Blocks|CommandClass') {
        $needsReplace = $true
        Write-Host "   Phat hien AutoCadAssemblyInfo.cs legacy (CommandClass cu)"
    }
}
else {
    $needsReplace = $true
}

if ($needsReplace -and (Test-Path $patchAssemblyInfo)) {
    Copy-Item $patchAssemblyInfo $assemblyInfo -Force
    Write-Host "   OK thay AutoCadAssemblyInfo.cs (khong con CommandClass loi)"
}

# Quet them file .cs tham chieu namespace cu.
Get-ChildItem -Path $blocksRoot -Recurse -Filter "*.cs" -File -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Name -eq 'AutoCadAssemblyInfo.cs') { return }
    $body = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($body -match 'MepPanelMvp\.Blocks\.|PowerBlockLibraryCommands|ReferenceDrawingInspector') {
        $excludeName = $_.Name + ".exclude"
        $excludePath = Join-Path $_.DirectoryName $excludeName
        if (-not (Test-Path $excludePath)) {
            Rename-Item -Path $_.FullName -NewName $excludeName -Force
            Write-Host "   Rename legacy -> $excludeName"
        }
        else {
            Remove-Item $_.FullName -Force
            Write-Host "   Xoa trung legacy: $($_.Name)"
        }
    }
}

# Exclude trong csproj (phong SDK-style van compile file .exclude).
$csproj = Join-Path $blocksRoot "MepPanel.Blocks.AutoCAD.csproj"
if (Test-Path $csproj) {
    $content = Get-Content $csproj -Raw -Encoding UTF8
    $changed = $false
    $excludeNames = $brokenNames + @("AutoCadAssemblyInfo.cs.exclude")
    foreach ($name in $excludeNames) {
        if ($content -match [regex]::Escape("Compile Remove=`"$name`"")) { continue }
        if ($name -eq "AutoCadAssemblyInfo.cs.exclude") { continue }
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
        Write-Host "   OK cap nhat csproj exclude legacy"
    }
}

Write-Host "   Xong repair Blocks legacy."
