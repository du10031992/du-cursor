# Fix eLockViolation HVAC - ban manh (ASCII-only).
# 1) Copy MepDocumentContext.cs
# 2) Tim file .cs co StartTransaction + HVAC/AHU/Hvac/...
# 3) Boc moi try{...}catch chua StartTransaction bang MepDocumentContext.Run
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Fix HVAC eLockViolation (strong)"

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
Write-Host "   OK MepDocumentContext.cs -> $uiDir"

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

function Test-HvacRelated([string]$Name, [string]$Text) {
    if ($Name -match '(?i)Hvac|HVAC|DieuHoa|SupplyAir|AirCondition|OngGio|Configuration') { return $true }
    if ($Text -match '(?i)HVAC|Hvac|AHU|MEPHVAC|SupplyAir|DieuHoa') { return $true }
    if ($Text -match 'SA-\d{2}') { return $true }
    if ($Text -match '(?i)LockViolation') { return $true }
    if ($Text -match '(?i)tao so do') { return $true }
    return $false
}

function Ensure-UsingUi([string]$Text) {
    if ($Text -match 'using\s+MepPanelMvp\.UI\s*;') { return $Text }
    if ($Text -match '(?m)^namespace\s+') {
        return [regex]::Replace($Text, '(?m)^(namespace\s+)', "using MepPanelMvp.UI;`r`n`r`n`$1", 1)
    }
    return "using MepPanelMvp.UI;`r`n" + $Text
}

function Wrap-AllTryWithTransaction([string]$Text) {
    $result = New-Object System.Text.StringBuilder
    $pos = 0
    $wrapCount = 0

    while ($true) {
        $tryIdx = $Text.IndexOf('try', $pos, [StringComparison]::Ordinal)
        if ($tryIdx -lt 0) { break }

        # word boundary: char before try
        if ($tryIdx -gt 0) {
            $before = $Text[$tryIdx - 1]
            if ([char]::IsLetterOrDigit($before) -or $before -eq '_') {
                $pos = $tryIdx + 3
                continue
            }
        }
        $afterTry = $tryIdx + 3
        if ($afterTry -lt $Text.Length) {
            $afterCh = $Text[$afterTry]
            if ([char]::IsLetterOrDigit($afterCh) -or $afterCh -eq '_') {
                $pos = $tryIdx + 3
                continue
            }
        }

        $braceOpen = $Text.IndexOf([char]123, $tryIdx)
        if ($braceOpen -lt 0 -or $braceOpen -gt ($tryIdx + 40)) {
            $pos = $tryIdx + 3
            continue
        }

        $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
        if ($braceClose -lt 0) {
            $pos = $tryIdx + 3
            continue
        }

        $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
        if ($inner -notmatch 'StartTransaction') {
            $pos = $braceClose + 1
            continue
        }
        if ($inner -match 'MepDocumentContext\.Run') {
            $pos = $braceClose + 1
            continue
        }

        $afterLen = [Math]::Min(120, $Text.Length - $braceClose - 1)
        $after = ""
        if ($afterLen -gt 0) {
            $after = $Text.Substring($braceClose + 1, $afterLen)
        }
        if ($after -notmatch '^\s*catch\b') {
            $pos = $braceClose + 1
            continue
        }

        [void]$result.Append($Text.Substring($pos, $braceOpen + 1 - $pos))
        [void]$result.Append("`r`n            MepDocumentContext.Run(() =>`r`n            ")
        [void]$result.Append([char]123)
        [void]$result.Append($inner)
        [void]$result.Append("`r`n            ")
        [void]$result.Append([char]125)
        [void]$result.Append(");`r`n            ")
        # keep the original closing brace of try
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
$patchedFiles = 0
$totalWraps = 0

$candidates = Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '\\(bin|obj)\\' -and
        $_.Name -ne 'MepDocumentContext.cs'
    }

foreach ($f in $candidates) {
    $raw = Read-Utf8 $f.FullName
    if ($raw -notmatch 'StartTransaction') { continue }
    if (-not (Test-HvacRelated -Name $f.Name -Text $raw)) { continue }

    $log.Add(("CANDIDATE: " + $f.FullName))
    $wrap = Wrap-AllTryWithTransaction $raw
    if ($wrap.Count -gt 0) {
        $out = Ensure-UsingUi $wrap.Text
        Write-Utf8NoBom $f.FullName $out
        Write-Host ("   OK " + $f.Name + " wraps=" + $wrap.Count)
        $log.Add(("PATCHED wraps=" + $wrap.Count + " " + $f.FullName))
        $patchedFiles++
        $totalWraps += $wrap.Count
    }
    else {
        Write-Host ("   SKIP (no wrap) " + $f.Name)
        $log.Add(("SKIP: " + $f.FullName))
    }
}

$logPath = Join-Path $Root "hvac-elock-fix.log"
$log | Set-Content -Path $logPath -Encoding UTF8
Write-Host ("   Log: " + $logPath)
Write-Host ("   PatchedFiles=" + $patchedFiles + " TotalWraps=" + $totalWraps)

if ($patchedFiles -eq 0) {
    Write-Host "   WARNING: Khong wrap duoc file nao."
    Write-Host "   Gui noi dung file hvac-elock-fix.log hoac ten file chua chuoi loi HVAC."
}

Write-Host "   Done HVAC eLock strong fix."
