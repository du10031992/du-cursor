# Fix eLockViolation: force-wrap BuildSchematic + HvacCommands callers.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$log = New-Object System.Collections.Generic.List[string]
function Log([string]$m) { $log.Add($m); Write-Host "   $m" }

Write-Host "==> Fix HVAC eLockViolation (BuildSchematic + HvacCommands)"

$uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"
if (-not (Test-Path $uiDir)) {
    $uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui"
}
if (-not (Test-Path $uiDir)) {
    New-Item -ItemType Directory -Force -Path (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI") | Out-Null
    $uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"
}
Copy-Item (Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\UI\MepDocumentContext.cs") `
    (Join-Path $uiDir "MepDocumentContext.cs") -Force
Log "OK MepDocumentContext.cs"

function Read-Utf8([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

function Find-MatchingBrace([string]$Text, [int]$OpenIndex) {
    $depth = 0
    for ($i = $OpenIndex; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq [char]123) { $depth++ }
        elseif ($Text[$i] -eq [char]125) {
            $depth--
            if ($depth -eq 0) { return $i }
        }
    }
    return -1
}

function Ensure-UsingUi([string]$Text) {
    if ($Text -match 'using\s+MepPanelMvp\.UI\s*;') { return $Text }
    if ($Text -match '(?m)^namespace\s+') {
        return [regex]::Replace($Text, '(?m)^(namespace\s+)', "using MepPanelMvp.UI;`r`n`r`n`$1", 1)
    }
    return "using MepPanelMvp.UI;`r`n" + $Text
}

function Wrap-NamedVoidMethod([string]$Text, [string]$MethodName, [string]$Marker) {
    $rx = [regex]("(?m)^(\s*)(public|private|internal|protected)\s+(static\s+)?void\s+" + [regex]::Escape($MethodName) + "\s*\(")
    $m = $rx.Match($Text)
    if (-not $m.Success) {
        return @{ Text = $Text; Count = 0; Reason = "method not found: $MethodName" }
    }

    $sigStart = $m.Index
    $i = $m.Index + $m.Length
    $parenDepth = 1
    while ($i -lt $Text.Length -and $parenDepth -gt 0) {
        if ($Text[$i] -eq '(') { $parenDepth++ }
        elseif ($Text[$i] -eq ')') { $parenDepth-- }
        $i++
    }
    $braceOpen = $Text.IndexOf([char]123, $i)
    if ($braceOpen -lt 0 -or $braceOpen -gt ($i + 80)) {
        return @{ Text = $Text; Count = 0; Reason = "no body brace: $MethodName" }
    }
    $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
    if ($braceClose -lt 0) {
        return @{ Text = $Text; Count = 0; Reason = "no body close: $MethodName" }
    }

    $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
    if ($inner -match [regex]::Escape($Marker)) {
        return @{ Text = $Text; Count = 0; Reason = "already marked: $MethodName" }
    }

    # Neu body da co MepDocumentContext.Run o top-level thi bo qua
    if ($inner -match '^\s*MepDocumentContext\.Run\s*\(') {
        return @{ Text = $Text; Count = 0; Reason = "already Run: $MethodName" }
    }

    $wrapped =
        $Text.Substring(0, $braceOpen + 1) +
        "`r`n            // $Marker`r`n            MepDocumentContext.Run(() =>`r`n            " +
        [char]123 + $inner + "`r`n            " + [char]125 + ");`r`n            " +
        $Text.Substring($braceClose)

    return @{ Text = $wrapped; Count = 1; Reason = "patched $MethodName" }
}

# 1) Force wrap BuildSchematic in service
$service = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Cad\HvacSupplyAirDrawingService.cs"
if (-not (Test-Path $service)) { throw "Missing $service" }
$svc = Read-Utf8 $service
$r1 = Wrap-NamedVoidMethod -Text $svc -MethodName "BuildSchematic" -Marker "MEP_ELOCK_BUILDSCHEMATIC"
Log ("SERVICE BuildSchematic: " + $r1.Reason)
if ($r1.Count -gt 0) {
    Write-Utf8NoBom $service (Ensure-UsingUi $r1.Text)
    $svc = Read-Utf8 $service
}

# 2) Patch HvacCommands.cs - wrap methods that call BuildSchematic or show HVAC error
$hvacCmd = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands\HvacCommands.cs"
if (Test-Path $hvacCmd) {
    $cmd = Read-Utf8 $hvacCmd
    Log ("HvacCommands.cs length=" + $cmd.Length)

    # Tim ten method void co BuildSchematic hoac chuoi loi
    $rx = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?void\s+(\w+)\s*\('
    $patchedCmd = 0
    # Process from end to keep indices stable when wrapping one method at a time - wrap by re-reading
    $methodNames = @()
    foreach ($m in $rx.Matches($cmd)) {
        $name = $m.Groups[3].Value
        $open = $cmd.IndexOf([char]123, $m.Index)
        if ($open -lt 0) { continue }
        $close = Find-MatchingBrace -Text $cmd -OpenIndex $open
        if ($close -lt 0) { continue }
        $body = $cmd.Substring($open, $close - $open)
        $loi = [System.Text.Encoding]::UTF8.GetString([byte[]](0x4C, 0xE1, 0xBB, 0x97, 0x69))
        if ($body -match 'BuildSchematic' -or $body.Contains($loi) -or $body -match 'so do HVAC' -or $body -match 'tao so do') {
            $methodNames += $name
        }
    }
    $methodNames = $methodNames | Select-Object -Unique
    Log ("HvacCommands candidate methods: " + ($methodNames -join ', '))

    foreach ($name in $methodNames) {
        $cmd = Read-Utf8 $hvacCmd
        $r = Wrap-NamedVoidMethod -Text $cmd -MethodName $name -Marker ("MEP_ELOCK_HVACCMD_" + $name)
        Log ("  " + $name + ": " + $r.Reason)
        if ($r.Count -gt 0) {
            Write-Utf8NoBom $hvacCmd (Ensure-UsingUi $r.Text)
            $patchedCmd++
        }
    }
    Log ("HvacCommands patched=" + $patchedCmd)
}
else {
    Log "HvacCommands.cs not found"
}

# 3) Same for PanelCommands if it calls BuildSchematic
$panelCmd = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands\PanelCommands.cs"
if (Test-Path $panelCmd) {
    $cmd = Read-Utf8 $panelCmd
    $rx = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?void\s+(\w+)\s*\('
    $names = @()
    foreach ($m in $rx.Matches($cmd)) {
        $name = $m.Groups[3].Value
        $open = $cmd.IndexOf([char]123, $m.Index)
        if ($open -lt 0) { continue }
        $close = Find-MatchingBrace -Text $cmd -OpenIndex $open
        if ($close -lt 0) { continue }
        $body = $cmd.Substring($open, $close - $open)
        if ($body -match 'BuildSchematic') { $names += $name }
    }
    foreach ($name in ($names | Select-Object -Unique)) {
        $cmd = Read-Utf8 $panelCmd
        $r = Wrap-NamedVoidMethod -Text $cmd -MethodName $name -Marker ("MEP_ELOCK_PANEL_" + $name)
        Log ("PanelCommands " + $name + ": " + $r.Reason)
        if ($r.Count -gt 0) {
            Write-Utf8NoBom $panelCmd (Ensure-UsingUi $r.Text)
        }
    }
}

# 4) Snapshot: show whether BuildSchematic body starts with Run
$svc2 = Read-Utf8 $service
if ($svc2 -match 'void\s+BuildSchematic') {
    $m = [regex]::Match($svc2, '(?s)void\s+BuildSchematic\s*\([^)]*\)\s*\{(.{0,200})')
    if ($m.Success) {
        $snippet = ($m.Groups[1].Value -replace '\s+', ' ').Trim()
        if ($snippet.Length -gt 160) { $snippet = $snippet.Substring(0, 160) }
        Log ("BuildSchematic start: " + $snippet)
    }
}

$logPath = Join-Path $Root "hvac-elock-fix.log"
$log | Set-Content $logPath -Encoding UTF8
Write-Host ("   Log: " + $logPath)
Write-Host "   Done."
