# Sua file legacy trong MepPanel.Blocks.AutoCAD gay CS0234/CS0246/CS0579.
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

function Exclude-LegacyFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return
    }

    $dir = Split-Path $Path -Parent
    $name = Split-Path $Path -Leaf
    $excludeName = $name + ".exclude"
    $excludePath = Join-Path $dir $excludeName

    if (Test-Path $excludePath) {
        Remove-Item $Path -Force
        Write-Host "   Xoa trung: $name"
    }
    else {
        Rename-Item -Path $Path -NewName $excludeName -Force
        Write-Host "   Exclude legacy -> $excludeName"
    }
}

$brokenNames = @(
    "PowerBlockLibraryCommands.cs",
    "ReferenceDrawingInspector.cs",
    "AutoCadAssemblyInfo.cs",
    "MepDrawingToolPanel.cs"
)

foreach ($name in $brokenNames) {
    Get-ChildItem -Path $blocksRoot -Recurse -Filter $name -File -ErrorAction SilentlyContinue |
        ForEach-Object { Exclude-LegacyFile -Path $_.FullName }
}

# Properties\AssemblyInfo.cs (net48 cu) tranh CS0579 trung SDK auto-generate.
$propsAssemblyInfo = Join-Path $blocksRoot "Properties\AssemblyInfo.cs"
Exclude-LegacyFile -Path $propsAssemblyInfo

# Quet them file .cs tham chieu namespace cu.
Get-ChildItem -Path $blocksRoot -Recurse -Filter "*.cs" -File -ErrorAction SilentlyContinue | ForEach-Object {
    if ($brokenNames -contains $_.Name) { return }
    if ($_.Name -eq 'AssemblyInfo.cs') { return }
    $body = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($body -match 'MepPanelMvp\.Blocks\.|PowerBlockLibraryCommands|ReferenceDrawingInspector|\[assembly:\s*CommandClass') {
        Exclude-LegacyFile -Path $_.FullName
    }
}

Write-Host "   Xong repair Blocks legacy."
