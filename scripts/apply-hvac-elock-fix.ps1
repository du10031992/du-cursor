# Fix eLockViolation: patch HvacSupplyAirDrawingService.cs (va file Cad HVAC).
# Boc TOAN BO than method chua StartTransaction bang MepDocumentContext.Run.
# Chi patch src\ (bo qua package_staging_*).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Fix HVAC eLockViolation (method wrap)"

$uiDir = $null
foreach ($c in @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui")
)) {
    if (Test-Path $c) { $uiDir = $c; break }
}
if (-not $uiDir) {
    $uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"
    New-Item -ItemType Directory -Force -Path $uiDir | Out-Null
}

$srcHelper = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\UI\MepDocumentContext.cs"
if (-not (Test-Path $srcHelper)) { throw "Thieu patch: $srcHelper" }
Copy-Item $srcHelper (Join-Path $uiDir "MepDocumentContext.cs") -Force
Write-Host "   OK MepDocumentContext.cs"

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

function Find-MethodOpenBraceBefore([string]$Text, [int]$Index) {
    # Walk back to a method-like signature, then find its {
    $search = $Text.Substring(0, $Index)
    $rx = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?(void|[\w\.]+)\s+(\w+)\s*\('
    $matches = $rx.Matches($search)
    if ($matches.Count -eq 0) { return -1 }

    $m = $matches[$matches.Count - 1]
    $sigEnd = $m.Index + $m.Length
    # find closing paren of signature then {
    $parenDepth = 1
    $i = $sigEnd
    while ($i -lt $Text.Length -and $parenDepth -gt 0) {
        if ($Text[$i] -eq '(') { $parenDepth++ }
        elseif ($Text[$i] -eq ')') { $parenDepth-- }
        $i++
    }
    if ($parenDepth -ne 0) { return -1 }

    $braceOpen = $Text.IndexOf([char]123, $i)
    if ($braceOpen -lt 0 -or $braceOpen -gt ($i + 80)) { return -1 }
    return $braceOpen
}

function Wrap-MethodsWithTransaction([string]$Text) {
    if ($Text -match 'MEP_ELOCK_METHOD_WRAP') {
        return @{ Text = $Text; Count = 0 }
    }

    $result = New-Object System.Text.StringBuilder
    $pos = 0
    $wrapCount = 0
    $guard = 0

    while ($guard -lt 50) {
        $guard++
        $txIdx = $Text.IndexOf('StartTransaction', $pos, [StringComparison]::Ordinal)
        if ($txIdx -lt 0) { break }

        $braceOpen = Find-MethodOpenBraceBefore -Text $Text -Index $txIdx
        if ($braceOpen -lt 0) {
            $pos = $txIdx + 16
            continue
        }

        $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
        if ($braceClose -lt 0) {
            $pos = $txIdx + 16
            continue
        }

        # Skip if this StartTransaction is outside the method we found
        if ($txIdx -gt $braceClose) {
            $pos = $txIdx + 16
            continue
        }

        $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
        if ($inner -match 'MepDocumentContext\.Run' -or $inner -match 'MEP_ELOCK_METHOD_WRAP') {
            $pos = $braceClose + 1
            continue
        }
        if ($inner -notmatch 'StartTransaction') {
            $pos = $braceClose + 1
            continue
        }

        [void]$result.Append($Text.Substring($pos, $braceOpen + 1 - $pos))
        [void]$result.Append("`r`n            // MEP_ELOCK_METHOD_WRAP`r`n            MepDocumentContext.Run(() =>`r`n            ")
        [void]$result.Append([char]123)
        [void]$result.Append($inner)
        [void]$result.Append("`r`n            ")
        [void]$result.Append([char]125)
        [void]$result.Append(");`r`n            ")
        [void]$result.Append([char]125)

        $wrapCount++
        $pos = $braceClose + 1
    }

    if ($wrapCount -eq 0) {
        return @{ Text = $Text; Count = 0 }
    }

    [void]$result.Append($Text.Substring($pos))
    return @{ Text = $result.ToString(); Count = $wrapCount }
}

$log = New-Object System.Collections.Generic.List[string]
$targets = @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Cad\HvacSupplyAirDrawingService.cs"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\cad\HvacSupplyAirDrawingService.cs")
)

# Them cac file Cad\*Hvac*.cs trong src (khong staging)
$cadDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Cad"
if (Test-Path $cadDir) {
    Get-ChildItem $cadDir -Filter "*Hvac*.cs" -File -ErrorAction SilentlyContinue | ForEach-Object {
        $targets += $_.FullName
    }
    Get-ChildItem $cadDir -Filter "*HVAC*.cs" -File -ErrorAction SilentlyContinue | ForEach-Object {
        $targets += $_.FullName
    }
}

$targets = $targets | Select-Object -Unique
$patchedFiles = 0
$totalWraps = 0

foreach ($path in $targets) {
    if (-not (Test-Path $path)) { continue }
    if ($path -match 'package_staging') { continue }

    $raw = Read-Utf8 $path
    $log.Add(("TARGET: " + $path))
    if ($raw -notmatch 'StartTransaction') {
        $log.Add("  no StartTransaction")
        continue
    }

    $wrap = Wrap-MethodsWithTransaction $raw
    if ($wrap.Count -gt 0) {
        $out = Ensure-UsingUi $wrap.Text
        Write-Utf8NoBom $path $out
        Write-Host ("   OK " + [IO.Path]::GetFileName($path) + " methods=" + $wrap.Count)
        $log.Add(("  PATCHED methods=" + $wrap.Count))
        $patchedFiles++
        $totalWraps += $wrap.Count
    }
    else {
        Write-Host ("   SKIP " + [IO.Path]::GetFileName($path))
        $log.Add("  SKIP wrap=0")
        # Dump small hint: does it already use LockDocument?
        if ($raw -match 'LockDocument') { $log.Add("  has LockDocument") }
        if ($raw -match 'MepDocumentContext') { $log.Add("  has MepDocumentContext") }
    }
}

$logPath = Join-Path $Root "hvac-elock-fix.log"
$log.Add(("PatchedFiles=" + $patchedFiles + " TotalWraps=" + $totalWraps))
$log | Set-Content -Path $logPath -Encoding UTF8
Write-Host ("   Log: " + $logPath)
Write-Host ("   PatchedFiles=" + $patchedFiles + " TotalWraps=" + $totalWraps)

if ($patchedFiles -eq 0) {
    throw "Khong wrap duoc HvacSupplyAirDrawingService.cs. Mo file do gui 30 dong quanh StartTransaction."
}

Write-Host "   Done."
