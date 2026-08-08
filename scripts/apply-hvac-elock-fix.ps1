# Fix eLockViolation HVAC - patch service + caller + copy helper.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$log = New-Object System.Collections.Generic.List[string]

function Log([string]$m) { $log.Add($m); Write-Host "   $m" }

Write-Host "==> Fix HVAC eLockViolation (service + caller)"

# --- copy helper ---
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
Copy-Item $srcHelper (Join-Path $uiDir "MepDocumentContext.cs") -Force
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

function Find-VoidMethodOpenBraceBefore([string]$Text, [int]$Index) {
    $search = $Text.Substring(0, $Index)
    $rx = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?void\s+(\w+)\s*\('
    $matches = $rx.Matches($search)
    if ($matches.Count -eq 0) { return -1 }
    $m = $matches[$matches.Count - 1]
    $sigEnd = $m.Index + $m.Length
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

function Wrap-VoidMethodsWithTransaction([string]$Text) {
    $result = New-Object System.Text.StringBuilder
    $pos = 0
    $wrapCount = 0
    $guard = 0
    while ($guard -lt 80) {
        $guard++
        $txIdx = $Text.IndexOf('StartTransaction', $pos, [StringComparison]::Ordinal)
        if ($txIdx -lt 0) { break }

        $braceOpen = Find-VoidMethodOpenBraceBefore -Text $Text -Index $txIdx
        if ($braceOpen -lt 0) { $pos = $txIdx + 16; continue }

        $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
        if ($braceClose -lt 0) { $pos = $txIdx + 16; continue }
        if ($txIdx -gt $braceClose) { $pos = $txIdx + 16; continue }

        $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
        if ($inner -match 'MEP_ELOCK_METHOD_WRAP') { $pos = $braceClose + 1; continue }
        if ($inner -notmatch 'StartTransaction') { $pos = $braceClose + 1; continue }

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
    if ($wrapCount -eq 0) { return @{ Text = $Text; Count = 0 } }
    [void]$result.Append($Text.Substring($pos))
    return @{ Text = $result.ToString(); Count = $wrapCount }
}

function Wrap-TryCatchAroundMessage([string]$Text) {
    # Find "so do HVAC" / LockViolation message then wrap preceding try
    $idx = -1
    foreach ($n in @('so do HVAC', 'so do HVAC', 'LockViolation', 'Tao so do', 'tao so do', 'HVAC:')) {
        $i = $Text.LastIndexOf($n, [StringComparison]::OrdinalIgnoreCase)
        if ($i -gt $idx) { $idx = $i }
    }
    # UTF-8 "Lỗi" prefix search via bytes decoded
    $loi = [System.Text.Encoding]::UTF8.GetString([byte[]](0x4C, 0xE1, 0xBB, 0x97, 0x69))
    $i2 = $Text.LastIndexOf($loi, [StringComparison]::Ordinal)
    if ($i2 -gt $idx) { $idx = $i2 }

    if ($idx -lt 0) { return @{ Text = $Text; Count = 0 } }
    if ($Text -match 'MEP_ELOCK_CALLER_WRAP') { return @{ Text = $Text; Count = 0 } }

    $tryIdx = $Text.LastIndexOf('try', $idx, [StringComparison]::Ordinal)
    if ($tryIdx -lt 0) { return @{ Text = $Text; Count = 0 } }

    $braceOpen = $Text.IndexOf([char]123, $tryIdx)
    if ($braceOpen -lt 0 -or $braceOpen -gt ($tryIdx + 40)) { return @{ Text = $Text; Count = 0 } }
    $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
    if ($braceClose -lt 0) { return @{ Text = $Text; Count = 0 } }

    $afterLen = [Math]::Min(100, $Text.Length - $braceClose - 1)
    $after = $Text.Substring($braceClose + 1, $afterLen)
    if ($after -notmatch '^\s*catch\b') { return @{ Text = $Text; Count = 0 } }

    $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
    if ($inner -match 'MepDocumentContext\.Run') { return @{ Text = $Text; Count = 0 } }

    $wrapped =
        $Text.Substring(0, $braceOpen + 1) +
        "`r`n            // MEP_ELOCK_CALLER_WRAP`r`n            MepDocumentContext.Run(() =>`r`n            " +
        [char]123 + $inner + "`r`n            " + [char]125 + ");`r`n            " +
        $Text.Substring($braceClose)

    return @{ Text = $wrapped; Count = 1 }
}

# --- patch service ---
$service = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Cad\HvacSupplyAirDrawingService.cs"
if (-not (Test-Path $service)) {
    throw "Khong thay: $service"
}
$raw = Read-Utf8 $service
Log ("SERVICE: " + $service)
Log ("  has MEP_ELOCK_METHOD_WRAP=" + ($raw -match 'MEP_ELOCK_METHOD_WRAP'))
Log ("  StartTransaction count=" + ([regex]::Matches($raw, 'StartTransaction')).Count)

# Force re-wrap: remove old wrap markers by not skipping - if already wrapped, leave it
if ($raw -match 'MEP_ELOCK_METHOD_WRAP') {
    Log "  service already wrapped"
}
else {
    $w = Wrap-VoidMethodsWithTransaction $raw
    if ($w.Count -eq 0) { throw "Khong wrap duoc void method trong HvacSupplyAirDrawingService.cs" }
    Write-Utf8NoBom $service (Ensure-UsingUi $w.Text)
    Log ("  PATCHED service methods=" + $w.Count)
}

# --- patch callers (error message files) under src only ---
$callerPatched = 0
Get-ChildItem (Join-Path $PluginSourceRoot "src") -Filter *.cs -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|package_staging)\\' } |
    ForEach-Object {
        $t = Read-Utf8 $_.FullName
        $loi = [System.Text.Encoding]::UTF8.GetString([byte[]](0x4C, 0xE1, 0xBB, 0x97, 0x69))
        $hit = ($t.Contains($loi) -and $t -match 'HVAC') -or ($t -match 'Loi tao so do HVAC') -or ($t -match 'tao so do HVAC')
        if (-not $hit) { return }

        Log ("CALLER: " + $_.FullName)
        $w = Wrap-TryCatchAroundMessage $t
        if ($w.Count -gt 0) {
            Write-Utf8NoBom $_.FullName (Ensure-UsingUi $w.Text)
            Log ("  PATCHED caller")
            $script:callerPatched++
        }
        else {
            Log "  caller wrap skipped (structure)"
        }
    }

# --- dump method names from service for debug ---
$svcNow = Read-Utf8 $service
$rxMethods = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?void\s+(\w+)\s*\('
foreach ($m in $rxMethods.Matches($svcNow)) {
    Log ("  void method: " + $m.Groups[3].Value)
}

$logPath = Join-Path $Root "hvac-elock-fix.log"
$log.Add(("callerPatched=" + $callerPatched))
$log | Set-Content $logPath -Encoding UTF8
Write-Host ("   Log: " + $logPath)

# --- disable old bundle if present ---
$old = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanelMvp.bundle"
if ((Test-Path $old) -and -not ($old.EndsWith('.OFF'))) {
    try {
        Rename-Item $old "MepPanelMvp.bundle.OFF" -Force -ErrorAction Stop
        Log "Disabled MepPanelMvp.bundle -> .OFF"
    } catch {
        Log "Could not rename MepPanelMvp.bundle (maybe already OFF or locked)"
    }
}

Write-Host "   Done."
