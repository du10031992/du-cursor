# Chen guard cho MOI void *_Click trong ElectricalToolControl.xaml.cs
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$target = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml.cs"
if (-not (Test-Path $target)) {
    Write-Host "   (bo qua - khong tim thay ElectricalToolControl.xaml.cs)"
    exit 0
}

$ClickFeatureMap = @{
    'ElectricalSystem_Click' = 'MEPDBDRAW'
    'HeDien_Click' = 'MEPDBDRAW'
    'WaterSystem_Click' = 'MEPDBWATER'
    'HeNuoc_Click' = 'MEPDBWATER'
    'FireAlarm_Click' = 'MEPDBSMOKE'
    'BaoChay_Click' = 'MEPDBSMOKE'
    'Hvac_Click' = 'MEPHVAC'
    'DieuHoa_Click' = 'MEPHVAC'
    'CapGio_Click' = 'MEPHVAC'
    'SelectLayer_Click' = 'MEPSELAYER'
    'OpenConfiguration_Click' = 'MEPDBCONFIG'
    'Export_Click' = 'MEPDBEXPORT'
    'Excel_Click' = 'MEPDBEXCEL'
    'Cabinet2d_Click' = 'MEPDBCABINET2D'
    'CabinetViews_Click' = 'MEPDBCABINETVIEWS'
    'Update_Click' = 'MEPDBUPDATE'
    'Power_Click' = 'MEPDBPOWER'
    'ThreePhaseFourWire_Click' = 'MEPDB3P4W'
    'DeviceBlocks_Click' = 'MEPDBDRAW'
    'ElectricalKnowledge_Click' = 'MEPDBDRAW'
    'CabinetRender_Click' = 'MEPDBCABINET2D'
    'CabinetUnfold_Click' = 'MEPDBCABINET2D'
    'RealisticWiringRender_Click' = 'MEPDBCABINET2D'
    'RealisticWiring_Click' = 'MEPDBCABINET2D'
    'Duplicate_Click' = 'MEPDBCABINET2D'
    'DuplicatePanel_Click' = 'MEPDBCABINET2D'
}

function Resolve-Feature($name) {
    if ($ClickFeatureMap.ContainsKey($name)) { return $ClickFeatureMap[$name] }
    if ($name -match '(?i)(Hvac|DieuHoa|CapGio|SupplyAir|HeThongGio)') { return 'MEPHVAC' }
    if ($name -match '(?i)(Water|HeNuoc|Nuoc)') { return 'MEPDBWATER' }
    if ($name -match '(?i)(Fire|Smoke|BaoChay|Chay)') { return 'MEPDBSMOKE' }
    if ($name -match '(?i)(Electric|HeDien|DeviceBlock|Knowledge)') { return 'MEPDBDRAW' }
    if ($name -match '(?i)(Layer)') { return 'MEPSELAYER' }
    if ($name -match '(?i)(Config|Configuration)') { return 'MEPDBCONFIG' }
    if ($name -match '(?i)(Excel)') { return 'MEPDBEXCEL' }
    if ($name -match '(?i)(Export)') { return 'MEPDBEXPORT' }
    if ($name -match '(?i)(Cabinet|Render|Unfold|Wiring|Duplicate|Panel2d|2d)') { return 'MEPDBCABINET2D' }
    if ($name -match '(?i)(Power|DongLuc)') { return 'MEPDBPOWER' }
    if ($name -match '(?i)(ThreePhase|3P4W|SoDo)') { return 'MEPDB3P4W' }
    if ($name -match '(?i)(CabinetView|View|Elevation|MaietChieu)') { return 'MEPDBCABINETVIEWS' }
    return $null
}

Write-Host "==> Force guards on ElectricalToolControl.xaml.cs"
$lines = [System.Collections.Generic.List[string]](Get-Content $target -Encoding UTF8)
$injected = 0

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -notmatch '^\s*(?:public|private|protected|internal)\s+(?:async\s+)?void\s+(?<name>\w+)\s*\(') {
        continue
    }
    if ($Matches['name'] -notmatch '_Click$') { continue }

    $feature = Resolve-Feature $Matches['name']
    if (-not $feature) { continue }

    $braceIndex = $i
    while ($braceIndex -lt $lines.Count - 1 -and $lines[$braceIndex] -notmatch '\{') { $braceIndex++ }
    if ($braceIndex -ge $lines.Count) { continue }

    $already = $false
    for ($j = $braceIndex + 1; $j -le [Math]::Min($braceIndex + 6, $lines.Count - 1); $j++) {
        if ($lines[$j] -match 'SUBFEATURE_GUARD|SUBFEATURE_WINDOW_GUARD|PluginFeatureGate\.Ensure') { $already = $true; break }
    }
    if ($already) { continue }

    $indent = '            '
    if ($lines[$braceIndex] -match '^(\s*)') { $indent = $Matches[1] + '    ' }

    $lines.Insert($braceIndex + 1, "${indent}if (!PluginFeatureGate.Ensure(`"$feature`")) return; // SUBFEATURE_GUARD")
    $injected++
    $i++
}

$text = $lines -join "`r`n"
if ($text -notmatch 'using MepPanel\.AutoCAD\.Licensing;' -and $text -match 'PluginFeatureGate') {
    $text = "using MepPanel.AutoCAD.Licensing;`r`n" + $text
}
Set-Content -Path $target -Value $text -Encoding UTF8
Write-Host "   Da chen $injected guard trong ElectricalToolControl.xaml.cs"
