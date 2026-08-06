# Chen guard vao cua so HVAC khi khoi tao (chan du duong nao mo cua so).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$script:Injected = 0

$WindowFeatureMap = @{
    'HvacConfigurationWindow' = 'MEPHVAC'
    'HvacConfigWindow' = 'MEPHVAC'
    'SupplyAirWindow' = 'MEPHVAC'
    'WaterConfigurationWindow' = 'MEPDBWATER'
    'FireAlarmWindow' = 'MEPDBSMOKE'
    'SmokeConfigurationWindow' = 'MEPDBSMOKE'
}

function Get-FeatureForWindowFile {
    param([string]$Path)

    foreach ($key in $WindowFeatureMap.Keys) {
        if ($Path -match $key) {
            return $WindowFeatureMap[$key]
        }
    }

    return $null
}

function Patch-WindowFile {
    param([string]$Path, [string]$Feature)

    $lines = [System.Collections.Generic.List[string]](Get-Content $Path -Encoding UTF8)
    $changed = $false

    if (($lines -join "`n") -match 'SUBFEATURE_WINDOW_GUARD') {
        return
    }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch 'InitializeComponent\s*\(\s*\)\s*;') {
            continue
        }

        $indent = '            '
        if ($lines[$i] -match '^(\s*)') { $indent = $Matches[1] }

        $lines.Insert($i + 1, "${indent}if (!PluginFeatureGate.Ensure(`"$Feature`")) { Close(); return; } // SUBFEATURE_WINDOW_GUARD")
        $changed = $true
        $script:Injected++
        break
    }

    if ($changed) {
        $text = $lines -join "`r`n"
        if ($text -notmatch 'using MepPanel\.AutoCAD\.Licensing;' -and $text -match 'PluginFeatureGate') {
            $text = "using MepPanel.AutoCAD.Licensing;`r`n" + $text
        }
        Set-Content -Path $Path -Value $text -Encoding UTF8
        Write-Host "   OK window guard -> $Path ($Feature)"
    }
}

Write-Host "==> Apply HVAC/window entry guards"
Get-ChildItem -Path $PluginSourceRoot -Filter *.xaml.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }

    $feature = Get-FeatureForWindowFile -Path $_.FullName
    if (-not $feature) { return }

    Patch-WindowFile -Path $_.FullName -Feature $feature
}

Write-Host "   Da chen $script:Injected window guard."
