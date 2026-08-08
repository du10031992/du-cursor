# Fix eLockViolation cho TAT CA he: Dien / HVAC / Nuoc / PCCC.
# - Copy MepDocumentContext.cs
# - Wrap void methods trong Cad\ va Commands\ co StartTransaction hoac ten ve CAD
# - Wrap *_Click trong UI\*.xaml.cs neu co StartTransaction / BuildSchematic / DrawingService
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$log = New-Object System.Collections.Generic.List[string]
function Log([string]$m) { $log.Add($m); Write-Host "   $m" }

Write-Host "==> Fix eLockViolation ALL systems (Dien/HVAC/Nuoc/PCCC)"

# --- helper ---
$uiDir = $null
foreach ($c in @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui")
)) { if (Test-Path $c) { $uiDir = $c; break } }
if (-not $uiDir) {
    $uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"
    New-Item -ItemType Directory -Force -Path $uiDir | Out-Null
}
Copy-Item (Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\UI\MepDocumentContext.cs") `
    (Join-Path $uiDir "MepDocumentContext.cs") -Force
Log "OK MepDocumentContext.cs"

# WaterFireCommands: luon copy ban da wrap MepDocumentContext
$wfSrc = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Commands\WaterFireCommands.cs"
$wfDstDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands"
if ((Test-Path $wfSrc) -and (Test-Path $wfDstDir)) {
    Copy-Item $wfSrc (Join-Path $wfDstDir "WaterFireCommands.cs") -Force
    Log "OK WaterFireCommands.cs (MepDocumentContext)"
}

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
        return @{ Text = $Text; Count = 0; Reason = "not found" }
    }

    $i = $m.Index + $m.Length
    $parenDepth = 1
    while ($i -lt $Text.Length -and $parenDepth -gt 0) {
        if ($Text[$i] -eq '(') { $parenDepth++ }
        elseif ($Text[$i] -eq ')') { $parenDepth-- }
        $i++
    }
    $braceOpen = $Text.IndexOf([char]123, $i)
    if ($braceOpen -lt 0 -or $braceOpen -gt ($i + 80)) {
        return @{ Text = $Text; Count = 0; Reason = "no brace" }
    }
    $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
    if ($braceClose -lt 0) {
        return @{ Text = $Text; Count = 0; Reason = "no close" }
    }

    $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
    if ($inner -match [regex]::Escape($Marker)) {
        return @{ Text = $Text; Count = 0; Reason = "already" }
    }
    if ($inner -match '^\s*(//[^\r\n]*\r?\n\s*)*MepDocumentContext\.Run\s*\(') {
        return @{ Text = $Text; Count = 0; Reason = "already Run" }
    }

    $wrapped =
        $Text.Substring(0, $braceOpen + 1) +
        "`r`n            // $Marker`r`n            MepDocumentContext.Run(() =>`r`n            " +
        [char]123 + $inner + "`r`n            " + [char]125 + ");`r`n            " +
        $Text.Substring($braceClose)

    return @{ Text = $wrapped; Count = 1; Reason = "patched" }
}

function Test-ShouldWrapMethod([string]$Name, [string]$Body) {
    if ($Body -match 'MepDocumentContext\.Run') { return $false }
    if ($Body -match 'StartTransaction') { return $true }
    if ($Name -match '^(Build|Draw|Create|Insert|Place|Render|Export|Update|Generate|Ve|Tao)') { return $true }
    if ($Body -match 'BuildSchematic|DrawingService|TransactionManager|AppendEntity|BlockReference') { return $true }
    if ($Name -match '_Click$' -and $Body -match 'StartTransaction|BuildSchematic|DrawingService|AppendEntity') { return $true }
    return $false
}

function Wrap-FileMethods([string]$Path) {
    if ($Path -match 'MepDocumentContext\.cs|WaterFireCommands\.cs') {
        return 0
    }

    $text = Read-Utf8 $Path
    $rx = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?void\s+(\w+)\s*\('
    $names = @()
    foreach ($m in $rx.Matches($text)) {
        $name = $m.Groups[3].Value
        $open = $text.IndexOf([char]123, $m.Index)
        if ($open -lt 0) { continue }
        $close = Find-MatchingBrace -Text $text -OpenIndex $open
        if ($close -lt 0) { continue }
        $body = $text.Substring($open, $close - $open + 1)
        if (Test-ShouldWrapMethod -Name $name -Body $body) {
            $names += $name
        }
    }
    $names = $names | Select-Object -Unique
    $count = 0
    foreach ($name in $names) {
        $text = Read-Utf8 $Path
        $marker = "MEP_ELOCK_" + $name
        $r = Wrap-NamedVoidMethod -Text $text -MethodName $name -Marker $marker
        if ($r.Count -gt 0) {
            Write-Utf8NoBom $Path (Ensure-UsingUi $r.Text)
            Log ("OK " + [IO.Path]::GetFileName($Path) + "::" + $name)
            $count++
        }
    }
    return $count
}

$autoCadRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD"
if (-not (Test-Path $autoCadRoot)) {
    throw "Khong thay $autoCadRoot"
}

$total = 0
$dirs = @(
    (Join-Path $autoCadRoot "Cad"),
    (Join-Path $autoCadRoot "Commands"),
    (Join-Path $autoCadRoot "UI"),
    (Join-Path $autoCadRoot "ui")
)

foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem $dir -Filter *.cs -File -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.FullName -match 'package_staging') { return }
        $n = Wrap-FileMethods $_.FullName
        $total += $n
    }
    # xaml.cs
    Get-ChildItem $dir -Filter *.xaml.cs -File -ErrorAction SilentlyContinue | ForEach-Object {
        $n = Wrap-FileMethods $_.FullName
        $total += $n
    }
}

# Force known entry points neu con sot
$force = @(
    @{ File = "Cad\HvacSupplyAirDrawingService.cs"; Methods = @("BuildSchematic") },
    @{ File = "Commands\HvacCommands.cs"; Methods = @("DrawSupplyAirSchematic") },
    @{ File = "Commands\PanelCommands.cs"; Methods = @() }
)

foreach ($f in $force) {
    $path = Join-Path $autoCadRoot $f.File
    if (-not (Test-Path $path)) { continue }
    $text = Read-Utf8 $path
    # Neu PanelCommands: wrap moi void co StartTransaction / Build / Draw
    if ($f.File -match 'PanelCommands') {
        $total += (Wrap-FileMethods $path)
        continue
    }
    foreach ($method in $f.Methods) {
        $text = Read-Utf8 $path
        $r = Wrap-NamedVoidMethod -Text $text -MethodName $method -Marker ("MEP_ELOCK_" + $method)
        if ($r.Count -gt 0) {
            Write-Utf8NoBom $path (Ensure-UsingUi $r.Text)
            Log ("FORCE " + $f.File + "::" + $method)
            $total++
        }
        else {
            Log ("SKIP " + $f.File + "::" + $method + " (" + $r.Reason + ")")
        }
    }
}

# Disable old bundle
$old = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanelMvp.bundle"
if (Test-Path $old) {
    try {
        Rename-Item $old "MepPanelMvp.bundle.OFF" -Force -ErrorAction Stop
        Log "Disabled MepPanelMvp.bundle"
    } catch {
        Log "MepPanelMvp.bundle rename skipped"
    }
}

$logPath = Join-Path $Root "elock-all-systems.log"
$log.Add(("TOTAL_WRAPS=" + $total))
$log | Set-Content $logPath -Encoding UTF8
Write-Host ("   TOTAL_WRAPS=" + $total)
Write-Host ("   Log: " + $logPath)

if ($total -eq 0) {
    Write-Host "   WARNING: 0 wraps (co the da wrap het)."
}

Write-Host "   Done ALL systems eLock fix."
