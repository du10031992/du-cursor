# Chen guard vao handler panel: chi void *_Click (an toan) + lenh sau SINGLE_ENTRY_PATCH.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$script:Injected = 0
$script:Files = 0
$script:SkippedAlready = 0

$ClickFeatureMap = @{
    'Cabinet2d_Click' = 'MEPDBCABINET2D'
    'DrawCabinet2d' = 'MEPDBCABINET2D'
    'DrawCabinet2D_Click' = 'MEPDBCABINET2D'
    'Cabinet3d_Click' = 'MEPDBCABINET2D'
    'CabinetViews_Click' = 'MEPDBCABINETVIEWS'
    'CabinetUnfold_Click' = 'MEPDBCABINET2D'
    'RealisticWiringRender_Click' = 'MEPDBCABINET2D'
    'RealisticWiring_Click' = 'MEPDBCABINET2D'
    'CabinetRender_Click' = 'MEPDBCABINET2D'
    'Duplicate_Click' = 'MEPDBCABINET2D'
    'DuplicatePanel_Click' = 'MEPDBCABINET2D'
    'Update_Click' = 'MEPDBUPDATE'
    'OpenConfiguration_Click' = 'MEPDBCONFIG'
    'Config_Click' = 'MEPDBCONFIG'
    'CauHinhTu_Click' = 'MEPDBCONFIG'
    'Export_Click' = 'MEPDBEXPORT'
    'ExportCsv_Click' = 'MEPDBEXPORT'
    'Excel_Click' = 'MEPDBEXCEL'
    'ExportExcel_Click' = 'MEPDBEXCEL'
    'Power_Click' = 'MEPDBPOWER'
    'PowerLayout_Click' = 'MEPDBPOWER'
    'ThreePhaseFourWire_Click' = 'MEPDB3P4W'
    'Diagram3P4W_Click' = 'MEPDB3P4W'
    'SoDo3P4W_Click' = 'MEPDB3P4W'
    'SelectLayer_Click' = 'MEPSELAYER'
    'ChonLayer_Click' = 'MEPSELAYER'
    'SameLayer_Click' = 'MEPSELAYER'
    'DeviceBlocks_Click' = 'MEPDBDRAW'
    'ElectricalKnowledge_Click' = 'MEPDBDRAW'
    'ElectricalSystem_Click' = 'MEPDBDRAW'
    'HeDien_Click' = 'MEPDBDRAW'
    'Electric_Click' = 'MEPDBDRAW'
    'PowerSystem_Click' = 'MEPDBDRAW'
    'WaterSystem_Click' = 'MEPDBWATER'
    'HeNuoc_Click' = 'MEPDBWATER'
    'Plumbing_Click' = 'MEPDBWATER'
    'Water_Click' = 'MEPDBWATER'
    'FireAlarm_Click' = 'MEPDBSMOKE'
    'BaoChay_Click' = 'MEPDBSMOKE'
    'SmokeSystem_Click' = 'MEPDBSMOKE'
    'FireSystem_Click' = 'MEPDBSMOKE'
    'Hvac_Click' = 'MEPHVAC'
    'HvacSystem_Click' = 'MEPHVAC'
    'HvacPanel_Click' = 'MEPHVAC'
    'OpenHvac_Click' = 'MEPHVAC'
    'ShowHvac_Click' = 'MEPHVAC'
    'DieuHoa_Click' = 'MEPHVAC'
    'DieuHoaSystem_Click' = 'MEPHVAC'
    'AirCondition_Click' = 'MEPHVAC'
    'AirConditioning_Click' = 'MEPHVAC'
    'BtnHvac_Click' = 'MEPHVAC'
    'HvacButton_Click' = 'MEPHVAC'
    'HvacTab_Click' = 'MEPHVAC'
    'SelectHvac_Click' = 'MEPHVAC'
    'SystemHvac_Click' = 'MEPHVAC'
    'MepHvac_Click' = 'MEPHVAC'
    'HvacDraw_Click' = 'MEPHVAC'
    'HvacConfig_Click' = 'MEPHVAC'
    'HvacSmoke_Click' = 'MEPHVAC'
    'HvacDuct_Click' = 'MEPHVAC'
    'HvacLayer_Click' = 'MEPHVAC'
    'HvacElbow_Click' = 'MEPHVAC'
    'HvacPipe_Click' = 'MEPHVAC'
    'HvacSetup_Click' = 'MEPHVAC'
    'CapGio_Click' = 'MEPHVAC'
    'HeThongCapGio_Click' = 'MEPHVAC'
    'SupplyAir_Click' = 'MEPHVAC'
    'ThongSoCapGio_Click' = 'MEPHVAC'
    'BtnDieuHoa_Click' = 'MEPHVAC'
    'HeDieuHoa_Click' = 'MEPHVAC'
    'DieuHoaBtn_Click' = 'MEPHVAC'
}

function Resolve-FeatureForClickHandler {
    param([string]$MethodName)

    if ($ClickFeatureMap.ContainsKey($MethodName)) {
        return $ClickFeatureMap[$MethodName]
    }

    return $null
}

function Resolve-FeatureForUiClick {
    param([string]$MethodName)

    $mapped = Resolve-FeatureForClickHandler -MethodName $MethodName
    if ($mapped) { return $mapped }

    if ($MethodName -match '(?i)(Hvac|DieuHoa|CapGio|SupplyAir|AirCond|HeThongGio|ThongSoHe|HeGio)') { return 'MEPHVAC' }
    if ($MethodName -match '(?i)(Water|HeNuoc|Plumb|Nuoc(?!.*Hvac))') { return 'MEPDBWATER' }
    if ($MethodName -match '(?i)(FireAlarm|BaoChay|SmokeSystem|FireSystem|HeChay|BaoChay)') { return 'MEPDBSMOKE' }
    if ($MethodName -match '(?i)(Electrical|HeDien|ElectricSystem|DeviceBlock|HeDien)') { return 'MEPDBDRAW' }
    if ($MethodName -match '(?i)(SelectLayer|SameLayer|ChonLayer)') { return 'MEPSELAYER' }
    if ($MethodName -match '(?i)(OpenConfiguration|CauHinhTu|DbConfig)') { return 'MEPDBCONFIG' }
    if ($MethodName -match '(?i)(ExportCsv|XuatCsv)') { return 'MEPDBEXPORT' }
    if ($MethodName -match '(?i)(ExportExcel|XuatExcel|ExcelExport)') { return 'MEPDBEXCEL' }
    if ($MethodName -match '(?i)(CabinetView|Elevation|MaietChieu)') { return 'MEPDBCABINETVIEWS' }
    if ($MethodName -match '(?i)(ThreePhase|3P4W|SoDo3P)') { return 'MEPDB3P4W' }
    if ($MethodName -match '(?i)(PowerLayout|BoTriDongLuc|DongLuc)') { return 'MEPDBPOWER' }
    if ($MethodName -match '(?i)(Cabinet2|Cabinet3|DrawCabinet|Unfold|Render|Duplicate|RealisticWiring|VeTu|CapNhatTu|UpdateCabinet)') { return 'MEPDBCABINET2D' }

    return $null
}

function Test-IsUiPanelFile {
    param([string]$Path)
    return $Path -match '\\UI\\|ElectricalToolControl|DrawingTool|MepDrawing|MainPanel|ToolControl|ToolPalette'
}

function Test-ShouldScanFile {
    param([string]$FullName)

    if ($FullName -match '\\(bin|obj)\\') { return $false }
    if ($FullName -match '\\Licensing\\') { return $false }
    if ($FullName -notmatch '\\src\\MepPanel\.AutoCAD\\') { return $false }
    if ($FullName -match 'LoginWindow|LoginPalette') { return $false }
    if ($FullName -match 'PanelConfigurationWindow|ConfigurationWindow') { return $false }
    return $true
}

function Test-IsVoidMethodSignature {
    param([string]$Line)

    return $Line -match '^\s*(?:public|private|protected|internal)\s+(?:async\s+)?void\s+\w+\s*\('
}

function Patch-CsFile {
    param([string]$Path)

    $isUiPanel = Test-IsUiPanelFile -Path $Path
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

        if (-not (Test-IsVoidMethodSignature -Line $line)) {
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
        elseif ($methodName -match '_Click$' -or $ClickFeatureMap.ContainsKey($methodName)) {
            if ($isUiPanel) {
                $feature = Resolve-FeatureForUiClick -MethodName $methodName
            }
            else {
                $feature = Resolve-FeatureForClickHandler -MethodName $methodName
            }
        }
        elseif ($isUiPanel -and $methodName -match '(?i)^(Open|Show|Launch|Select|Switch|On).*(Hvac|CapGio|Supply|DieuHoa|Water|Fire|Electric|Smoke|Layer|Cabinet|Excel|Export|Power|Config)') {
            $feature = Resolve-FeatureForUiClick -MethodName $methodName
        }

        if (-not $feature) { continue }

        $braceIndex = $i
        while ($braceIndex -lt $lines.Count - 1 -and $lines[$braceIndex] -notmatch '\{') {
            $braceIndex++
        }

        if ($braceIndex -ge $lines.Count) { continue }

        # Bo qua expression-bodied / delegate - khong co body block
        if ($lines[$braceIndex] -match '=>\s*\S') { continue }

        $already = $false
        for ($j = $braceIndex + 1; $j -le [Math]::Min($braceIndex + 6, $lines.Count - 1); $j++) {
            if ($lines[$j] -match 'SUBFEATURE_GUARD') { $already = $true; break }
        }
        if ($already) {
            $script:SkippedAlready++
            continue
        }

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

Write-Host "==> Apply subfeature guard patch (void *_Click handlers only)"
Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if (-not (Test-ShouldScanFile -FullName $_.FullName)) { return }

    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match '_Click|SINGLE_ENTRY_PATCH') {
        Patch-CsFile -Path $_.FullName
    }
}

Write-Host "   Da chen $script:Injected guard trong $script:Files file (bo qua $script:SkippedAlready da co guard)."
