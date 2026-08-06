# Sua PanelCommands.cs / HvacCommands.cs bi vo (CS1519).
# Quet lui tu moi method command: xoa dong rac, chen [CommandMethod].
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot,
    [switch]$TryGitRestoreFirst
)

$ErrorActionPreference = "Stop"

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

function Test-IsSkippableBeforeMethod {
    param([string]$Line)

    if ([string]::IsNullOrWhiteSpace($Line)) { return $true }
    if ($Line -match '^\s*//') { return $true }
    if ($Line -match '^\s*"[^"]+"\s*,?\s*$') { return $true }
    if ($Line -match '^\s*\[CommandMethod\b') { return $true }
    if ($Line -match '^\s*CommandFlags\.') { return $true }
    if ($Line -match '^\s*Modal\b') { return $true }
    if ($Line -match '\)\]\s*$') { return $true }
    if ($Line -match '^\s*\]\s*$') { return $true }
    if ($Line -match '^\s*\#region') { return $true }
    if ($Line -match '^\s*\#endregion') { return $true }

    return $false
}

function Test-HasValidCommandMethod {
    param(
        [string]$Line,
        [string]$CommandName
    )
    return $Line -match "\[CommandMethod\s*\(\s*`"$([regex]::Escape($CommandName))`"[^\]]*\)\]\s*$"
}

function Test-IsMethodDeclaration {
    param(
        [string]$Line,
        [string]$MethodName
    )
    return $Line -match "^\s*(public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?[\w<>\[\]?,\s]+\s+$([regex]::Escape($MethodName))\s*\("
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

function Test-IsAnyMethodDeclaration {
    param([string]$Line)
    return $Line -match '^\s*(public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?[\w<>\[\]?,\s]+\s+(?<name>\w+)\s*\('
}

function Repair-CommandFile {
    param(
        [string]$Path,
        [hashtable]$MethodMap
    )

    $lines = [System.Collections.Generic.List[string]](Get-Content $Path -Encoding UTF8)
    $removed = 0
    $inserted = 0
    $changed = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if (-not (Test-IsAnyMethodDeclaration -Line $lines[$i])) { continue }
        if ($lines[$i] -notmatch '^\s*(public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?[\w<>\[\]?,\s]+\s+(?<name>\w+)\s*\(') {
            continue
        }

        $methodName = $Matches['name']
        $commandName = $MethodMap[$methodName]
        $indent = Get-LeadingIndent $lines[$i]
        $j = $i - 1
        $deleteIndexes = New-Object System.Collections.Generic.List[int]
        $needsInsert = [bool]$commandName

        while ($j -ge 0) {
            if ($commandName -and (Test-HasValidCommandMethod -Line $lines[$j] -CommandName $commandName)) {
                $needsInsert = $false
                break
            }

            if (Test-IsSkippableBeforeMethod -Line $lines[$j]) {
                if (-not [string]::IsNullOrWhiteSpace($lines[$j])) {
                    $deleteIndexes.Add($j)
                }
                $j--
                continue
            }
            break
        }

        if ($deleteIndexes.Count -gt 0) {
            $changed = $true
            $removed += $deleteIndexes.Count
            foreach ($idx in ($deleteIndexes | Sort-Object -Descending)) {
                $lines.RemoveAt($idx)
            }
            $i -= $deleteIndexes.Count
        }

        if ($needsInsert -and $commandName) {
            $lines.Insert($i, "${indent}[CommandMethod(`"$commandName`", CommandFlags.Modal)]")
            $inserted++
            $changed = $true
            $i++
        }
    }

    if ($changed) {
        Set-Content -Path $Path -Value ($lines -join "`r`n") -Encoding UTF8
    }

    return @{ Removed = $removed; Inserted = $inserted }
}

Write-Host "==> Repair: xoa dong rac + gan lai [CommandMethod] (PanelCommands/HvacCommands)"

$commandDir = Join-Path $PluginSourceRoot 'src\MepPanel.AutoCAD\Commands'
$any = $false

foreach ($entry in $CommandMaps.GetEnumerator()) {
    $fileName = $entry.Key
    $map = $entry.Value
    $path = Join-Path $commandDir $fileName
    if (-not (Test-Path $path)) {
        Write-Host "   (bo qua - khong tim thay $fileName)"
        continue
    }

    if ($TryGitRestoreFirst) {
        if (Try-GitRestoreCommandFile -Path $path) {
            Write-Host "   git restore -> $fileName"
        }
    }

    $result = Repair-CommandFile -Path $path -MethodMap $map
    Write-Host "   $fileName : xoa $($result.Removed) dong rac, chen $($result.Inserted) [CommandMethod]"
    if ($result.Removed -gt 0 -or $result.Inserted -gt 0) { $any = $true }
}

if (-not $any) {
    Write-Host "   Da quet xong. Neu van loi CS1519, gui 10 dong quanh line 286 trong PanelCommands.cs"
}
