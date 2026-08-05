# Chi chen guard vao *_Click handler ro rang + method sau SINGLE_ENTRY_PATCH.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$script:Injected = 0
$script:Files = 0

$ClickFeatureMap = @{
    'Cabinet2d_Click' = 'MEPDBCABINET2D'
    'DrawCabinet2d' = 'MEPDBCABINET2D'
    'Cabinet3d_Click' = 'MEPDBCABINET2D'
    'CabinetViews_Click' = 'MEPDBCABINETVIEWS'
    'HvacDraw_Click' = 'MEPHVAC'
    'HvacSmoke_Click' = 'MEPHVAC'
    'HvacConfig_Click' = 'MEPHVAC'
    'Hvac_Click' = 'MEPHVAC'
    'CabinetUnfold_Click' = 'MEPDBCABINET2D'
    'RealisticWiringRender_Click' = 'MEPDBCABINET2D'
    'RealisticWiring_Click' = 'MEPDBCABINET2D'
    'CabinetRender_Click' = 'MEPDBCABINET2D'
    'DeviceBlocks_Click' = 'MEPDBDRAW'
    'SelectLayer_Click' = 'MEPSELAYER'
    'Duplicate_Click' = 'MEPDBCABINET2D'
    'DuplicatePanel_Click' = 'MEPDBCABINET2D'
    'Update_Click' = 'MEPDBUPDATE'
    'OpenConfiguration_Click' = 'MEPDBCONFIG'
    'ElectricalKnowledge_Click' = 'MEPDBDRAW'
    'ThreePhaseFourWire_Click' = 'MEPDB3P4W'
    'Export_Click' = 'MEPDBEXPORT'
    'Excel_Click' = 'MEPDBEXCEL'
    'Power_Click' = 'MEPDBPOWER'
    'ElectricalSystem_Click' = 'MEPDBDRAW'
    'WaterSystem_Click' = 'MEPDBWATER'
    'FireAlarm_Click' = 'MEPDBSMOKE'
}

function Patch-CsFile {
    param([string]$Path)

    $lines = [System.Collections.Generic.List[string]](Get-Content $Path -Encoding UTF8)
    $changed = $false
    $localInjected = 0
    $pendingFeature = $null

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if ($line -match 'SINGLE_ENTRY_PATCH:.*CommandMethod.*"(MEP[^"]+)"') {
            $pendingFeature = $Matches[1]
            continue
        }

        if ($line -notmatch '^\s*(?:public|private|protected|internal)\s+(?:async\s+)?void\s+(?<name>\w+)\s*\(') {
            continue
        }

        $methodName = $Matches['name']
        $feature = $null

        if ($pendingFeature) {
            $feature = $pendingFeature
            $pendingFeature = $null
        }
        elseif ($ClickFeatureMap.ContainsKey($methodName)) {
            $feature = $ClickFeatureMap[$methodName]
        }

        if (-not $feature) { continue }

        $braceIndex = $i
        while ($braceIndex -lt $lines.Count - 1 -and $lines[$braceIndex] -notmatch '\{') {
            $braceIndex++
        }

        if ($braceIndex -ge $lines.Count) { continue }

        $already = $false
        for ($j = $braceIndex + 1; $j -le [Math]::Min($braceIndex + 4, $lines.Count - 1); $j++) {
            if ($lines[$j] -match 'SUBFEATURE_GUARD') { $already = $true; break }
        }
        if ($already) { continue }

        $indent = '            '
        if ($lines[$braceIndex] -match '^(\s*)') { $indent = $Matches[1] + '    ' }

        $lines.Insert($braceIndex + 1, "${indent}if (!PluginFeatureGate.Ensure(`"$feature`")) return; // SUBFEATURE_GUARD")
        $localInjected++
        $changed = $true
        $i++
    }

    if ($changed) {
        $text = $lines -join "`r`n"
        if ($text -notmatch 'using MepPanel\.AutoCAD\.Licensing;' -and $text -match 'PluginFeatureGate') {
            $text = "using MepPanel.AutoCAD.Licensing;`r`n" + $text
        }
        Set-Content -Path $Path -Value $text -Encoding UTF8
        $script:Files++
        $script:Injected += $localInjected
        Write-Host "   OK subfeature guard -> $Path ($localInjected cho)"
    }
}

Write-Host "==> Apply subfeature guard patch (panel handlers)"
Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }
    if ($_.FullName -match 'Configuration|LoginWindow|LoginPalette') { return }
    if ($_.FullName -notmatch '\\Commands\\|PanelCommands|HvacCommands|\.xaml\.cs$') { return }

    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match '_Click|SINGLE_ENTRY_PATCH') {
        Patch-CsFile -Path $_.FullName
    }
}

Write-Host "   Da chen $script:Injected guard trong $script:Files file."
