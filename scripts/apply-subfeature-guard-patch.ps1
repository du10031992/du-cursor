# Chen PluginFeatureGate.Ensure vao handler panel + method sau SINGLE_ENTRY_PATCH.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$script:Injected = 0
$script:Files = 0

function Resolve-FeatureFromMethodName {
    param([string]$Name)

    if ($Name -match 'Login|Otp|Theme|Close|Refresh|Save|MoveUp|MoveDown|Add_|Remove|Calculate|Recalculate|Renumber|QuickLoad|Balance|IncreaseCable|BranchPanel|MainPanel|EditPanel|NewPanel|OpenPanel|Select[^L]') {
        return $null
    }

    $rules = @(
        @{ Pattern = 'Cabinet2d|DrawCabinet2d'; Feature = 'MEPDBCABINET2D' },
        @{ Pattern = 'Cabinet3d'; Feature = 'MEPDBCABINET3D' },
        @{ Pattern = 'CabinetViews'; Feature = 'MEPDBCABINETVIEWS' },
        @{ Pattern = 'HvacDraw'; Feature = 'MEPHVACDRAW' },
        @{ Pattern = 'HvacSmoke'; Feature = 'MEPHVACSMOKE' },
        @{ Pattern = 'HvacConfig'; Feature = 'MEPHVACCONFIG' },
        @{ Pattern = 'Hvac'; Feature = 'MEPHVAC' },
        @{ Pattern = 'Unfold'; Feature = 'MEPDBUNFOLD' },
        @{ Pattern = 'RealisticWiringRender|RealWiringRender'; Feature = 'MEPDBREALRENDER' },
        @{ Pattern = 'RealisticWiring|RealWiring'; Feature = 'MEPDBREALWIRING' },
        @{ Pattern = 'CabinetRender'; Feature = 'MEPDBRENDER' },
        @{ Pattern = 'DeviceBlocks'; Feature = 'MEPDEVICEBLOCKS' },
        @{ Pattern = 'SelectLayer'; Feature = 'MEPSELAYER' },
        @{ Pattern = 'Duplicate'; Feature = 'MEPDBDUPLICATE' },
        @{ Pattern = 'Update'; Feature = 'MEPDBUPDATE' },
        @{ Pattern = 'OpenConfiguration|Configuration'; Feature = 'MEPDBCONFIG' },
        @{ Pattern = 'ElectricalKnowledge|Knowledge'; Feature = 'MEPDBKNOWLEDGE' },
        @{ Pattern = 'ThreePhaseFourWire|3P4W'; Feature = 'MEPDB3P4W' },
        @{ Pattern = 'Help'; Feature = 'MEPDBHELP' },
        @{ Pattern = 'Export'; Feature = 'MEPDBEXPORT' },
        @{ Pattern = 'Smoke'; Feature = 'MEPDBSMOKE' },
        @{ Pattern = 'Power'; Feature = 'MEPDBPOWER' },
        @{ Pattern = 'Draw'; Feature = 'MEPDBDRAW' }
    )

    foreach ($rule in $rules) {
        if ($Name -match $rule.Pattern) {
            return $rule.Feature
        }
    }

    return $null
}

function Get-Indent {
    param([string]$Line)
    if ($Line -match '^(\s*)') { return $Matches[1] }
    return '            '
}

function Should-SkipInject {
    param([string[]]$Lines, [int]$BraceIndex)

    $end = [Math]::Min($BraceIndex + 4, $Lines.Count - 1)
    for ($i = $BraceIndex + 1; $i -le $end; $i++) {
        if ($Lines[$i] -match 'PluginFeatureGate\.Ensure|LicenseGuard\.EnsureSubFeature|SUBFEATURE_GUARD') {
            return $true
        }
    }
    return $false
}

function New-GuardLine {
    param([string]$Indent, [string]$Feature)
    return "${Indent}if (!PluginFeatureGate.Ensure(`"$Feature`")) return; // SUBFEATURE_GUARD"
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

        if ($line -match '^\s*(public|private|protected|internal)\s+(?:async\s+)?(?:void|[\w<>\[\]]+)\s+(?<name>\w+)\s*\(') {
            $methodName = $Matches['name']
            $feature = $pendingFeature
            if (-not $feature) {
                $feature = Resolve-FeatureFromMethodName -Name $methodName
            }

            if ($feature) {
                $braceIndex = $i
                while ($braceIndex -lt $lines.Count - 1 -and $lines[$braceIndex] -notmatch '\{') {
                    $braceIndex++
                }

                if ($braceIndex -lt $lines.Count -and $lines[$braceIndex] -match '\{' -and -not (Should-SkipInject $lines.ToArray() $braceIndex)) {
                    $indent = Get-Indent $lines[$braceIndex]
                    $indent = "$indent    "
                    $lines.Insert($braceIndex + 1, (New-GuardLine $indent $feature))
                    $localInjected++
                    $changed = $true
                    $i++
                }
            }

            $pendingFeature = $null
        }
    }

    if ($changed) {
        $text = $lines -join "`r`n"
        if ($text -notmatch 'using MepPanel\.AutoCAD\.Licensing' -and $text -match 'PluginFeatureGate') {
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
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match '_Click|SINGLE_ENTRY_PATCH|DrawCabinet|Hvac_|Cabinet') {
        Patch-CsFile -Path $_.FullName
    }
}

Write-Host "   Da chen $script:Injected guard trong $script:Files file."
if ($script:Injected -eq 0) {
    Write-Host "   CANH BAO: Khong chen duoc guard. Kiem tra ten handler trong MepPanelMvp."
}
