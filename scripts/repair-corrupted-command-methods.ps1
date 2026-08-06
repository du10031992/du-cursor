# Sua PanelCommands.cs / HvacCommands.cs bi vo (CS1519) bang cach gan lai [CommandMethod].
# Map method -> lenh lay tu MepPanel.AutoCAD.dll trong bundle (nguon goc plugin).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot,
    [string]$BundleDll
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $BundleDll) {
    $BundleDll = Join-Path $Root "bundle\MepPanel.Plugin.bundle\Contents\MepPanel.AutoCAD.dll"
}

$CommandMaps = @{
    'PanelCommands.cs' = [ordered]@{
        'ShowPanelPalette' = 'MEPDB'
        'ShowPanelConfiguration' = 'MEPDBCONFIG'
        'DrawPanel' = 'MEPDBDRAW'
        'DrawThreePhaseFourWireSystem' = 'MEPDB3P4W'
        'DrawRealisticWiringDiagram' = 'MEPDBREALWIRING'
        'RenderRealisticWiringDiagram' = 'MEPDBREALRENDER'
        'EditPanel' = 'MEPDBEDIT'
        'UpdatePanel' = 'MEPDBUPDATE'
        'ExportPanel' = 'MEPDBEXPORT'
        'DrawCabinetViews' = 'MEPDBCABINETVIEWS'
        'DrawCabinetSheetMetalUnfold' = 'MEPDBUNFOLD'
        'ShowElectricalKnowledge' = 'MEPDBKNOWLEDGE'
        'DrawCabinet2d' = 'MEPDBCABINET2D'
        'RedirectLegacyCabinet3d' = 'MEPDBCABINET3D'
        'DrawCabinetPowerLayout' = 'MEPDBPOWER'
        'RenderCabinetColor' = 'MEPDBRENDER'
        'CreatePowerDeviceBlocks' = 'MEPDEVICEBLOCKS'
        'DuplicatePanel' = 'MEPDBDUPLICATE'
        'SelectSameLayer' = 'MEPSELAYER'
        'SmokeTest' = 'MEPDBSMOKE'
        'ShowHelp' = 'MEPDBHELP'
    }
    'HvacCommands.cs' = [ordered]@{
        'ShowConfiguration' = 'MEPHVAC'
        'DrawSupplyAirSchematic' = 'MEPHVACDRAW'
        'SmokeTest' = 'MEPHVACSMOKE'
    }
}

function Get-LeadingIndent {
    param([string]$Line)
    if ($Line -match '^(\s*)') { return $Matches[1] }
    return '        '
}

function Test-IsOrphanAttributeLine {
    param([string]$Line)
    if ([string]::IsNullOrWhiteSpace($Line)) { return $false }
    return $Line -match '^\s*//\s*SINGLE_ENTRY_PATCH' -or
           $Line -match '^\s*//\s*\[CommandMethod' -or
           $Line -match '^\s*"[^"]+"\s*,?\s*$' -or
           $Line -match '^\s*[\w\.]*CommandFlags\.' -or
           ($Line -match '\)\]\s*$' -and $Line -notmatch '\[CommandMethod') -or
           ($Line -match '^\s*\]\s*$')
}

function Test-IsValidCommandMethodFor {
    param([string]$Line, [string]$CommandName)
    return $Line -match "\[CommandMethod\s*\(\s*`"$([regex]::Escape($CommandName))`""
}

function Test-IsMethodDeclaration {
    param([string]$Line)
    return $Line -match '^\s*(public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:void|[\w<>\[\]?,\s]+)\s+(?<name>\w+)\s*\('
}

function Remove-TrailingOrphanLines {
    param($OutList)

    while ($OutList.Count -gt 0) {
        $last = $OutList[$OutList.Count - 1]
        if ([string]::IsNullOrWhiteSpace($last)) {
            $OutList.RemoveAt($OutList.Count - 1)
            continue
        }
        if (Test-IsOrphanAttributeLine -Line $last) {
            $OutList.RemoveAt($OutList.Count - 1)
            continue
        }
        if ($last -match '^\s*\[CommandMethod' -and $last -notmatch '\)\]\s*$') {
            $OutList.RemoveAt($OutList.Count - 1)
            continue
        }
        break
    }
}

function Test-FileLooksCorrupted {
    param([string]$Text)
    return $Text -match '(?m)^\s*"[^"]+"\s*,\s*$' -or
           $Text -match '(?m)^\s*[\w\.]*CommandFlags\.[^\r\n]*\)\]\s*$' -or
           $Text -match 'SINGLE_ENTRY_PATCH' -or
           ($Text -match '(?m)\)\]\s*$' -and $Text -match '(?m)^\s*\]\s*$')
}

function Try-GitRestoreCommandFile {
    param([string]$Path)

    $gitRoot = $PluginSourceRoot
    for ($n = 0; $n -lt 6; $n++) {
        if (Test-Path (Join-Path $gitRoot '.git')) { break }
        $parent = Split-Path $gitRoot -Parent
        if ($parent -eq $gitRoot) { return $false }
        $gitRoot = $parent
    }
    if (-not (Test-Path (Join-Path $gitRoot '.git'))) { return $false }

    $rel = $Path.Substring($gitRoot.Length).TrimStart('\', '/')
    Push-Location $gitRoot
    try {
        git cat-file -e "HEAD:$($rel -replace '\\','/')" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return $false }
        git checkout HEAD -- $rel 2>$null | Out-Null
        return ($LASTEXITCODE -eq 0)
    }
    finally {
        Pop-Location
    }
}

function Repair-CommandFileByMethodMap {
    param(
        [string]$Path,
        [hashtable]$MethodMap
    )

    $text = Get-Content $Path -Raw -Encoding UTF8
    if (-not (Test-FileLooksCorrupted -Text $text)) { return 0 }

    $lines = Get-Content $Path -Encoding UTF8
    $out = New-Object System.Collections.Generic.List[string]
    $fixed = 0
    $changed = $false

    foreach ($line in $lines) {
        if (-not (Test-IsMethodDeclaration -Line $line)) {
            $out.Add($line)
            continue
        }

        if ($line -notmatch '^\s*(public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:void|[\w<>\[\]?,\s]+)\s+(?<name>\w+)\s*\(') {
            $out.Add($line)
            continue
        }

        $methodName = $Matches['name']
        $commandName = $MethodMap[$methodName]
        $indent = Get-LeadingIndent $line

        Remove-TrailingOrphanLines -OutList $out

        $needsAttribute = $false
        if ($commandName) {
            $hasValid = $false
            if ($out.Count -gt 0) {
                $prev = $out[$out.Count - 1]
                if (Test-IsValidCommandMethodFor -Line $prev -CommandName $commandName) {
                    $hasValid = $true
                }
                elseif ($prev -match '^\s*\[CommandMethod' -and -not (Test-IsValidCommandMethodFor -Line $prev -CommandName $commandName)) {
                    $out.RemoveAt($out.Count - 1)
                    $changed = $true
                    $needsAttribute = $true
                }
            }
            else {
                $needsAttribute = $true
            }

            if (-not $hasValid -and -not $needsAttribute) {
                if ($out.Count -eq 0 -or $out[$out.Count - 1] -notmatch '\[CommandMethod') {
                    $needsAttribute = $true
                }
            }

            if ($needsAttribute) {
                $out.Add("${indent}[CommandMethod(`"$commandName`", CommandFlags.Modal)]")
                $fixed++
                $changed = $true
            }
        }

        $out.Add($line)
    }

    if ($changed) {
        Set-Content -Path $Path -Value ($out -join "`r`n") -Encoding UTF8
    }

    return $fixed
}

Write-Host "==> Repair: gan lai [CommandMethod] theo ten method (sua CS1519)"
if (-not (Test-Path $BundleDll)) {
    Write-Host "   CANH BAO: Khong tim thay $BundleDll"
}

$commandDir = Join-Path $PluginSourceRoot 'src\MepPanel.AutoCAD\Commands'
$totalFixed = 0
$totalFiles = 0

foreach ($entry in $CommandMaps.GetEnumerator()) {
    $fileName = $entry.Key
    $map = $entry.Value
    $path = Join-Path $commandDir $fileName
    if (-not (Test-Path $path)) { continue }

    $raw = Get-Content $path -Raw -Encoding UTF8
    if (-not (Test-FileLooksCorrupted -Text $raw)) { continue }

    if (Try-GitRestoreCommandFile -Path $path) {
        Write-Host "   git restore -> $fileName"
        $totalFiles++
        continue
    }

    $count = Repair-CommandFileByMethodMap -Path $path -MethodMap $map
    if ($count -gt 0) {
        Write-Host "   gan lai $count [CommandMethod] -> $fileName"
        $totalFixed += $count
        $totalFiles++
    }
    elseif (Test-FileLooksCorrupted -Text (Get-Content $path -Raw -Encoding UTF8)) {
        Write-Host "   CANH BAO: $fileName van co dau hieu hong - can xem tay"
    }
}

if ($totalFiles -eq 0) {
    Write-Host "   (Khong phat hien file Commands bi hong.)"
}
else {
    Write-Host "   Da sua $totalFixed attribute trong $totalFiles file."
}
