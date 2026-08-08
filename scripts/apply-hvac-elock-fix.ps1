# Fix eLockViolation khi tao so do HVAC tu cua so WPF modeless.
# ASCII-only script (tranh loi parse PowerShell Windows).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Fix HVAC eLockViolation (MepDocumentContext)"

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
if (-not (Test-Path $srcHelper)) {
    throw "Thieu patch: $srcHelper"
}
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
        $ch = $Text[$i]
        if ($ch -eq [char]123) { $depth++ }
        elseif ($ch -eq [char]125) {
            $depth--
            if ($depth -eq 0) { return $i }
        }
    }
    return -1
}

function Test-HvacErrorMessage([string]$Text) {
    # ASCII + UTF8 patterns without breaking PS parser
    if ($Text -match 'Loi tao so do HVAC') { return $true }
    if ($Text -match 'tao so do HVAC') { return $true }
    if ($Text -match 'so do HVAC') { return $true }
    # Vietnamese UTF-8 bytes decoded: "Lỗi tạo sơ đồ HVAC"
    if ($Text.Contains([string]([char]0x004C) + [char]0x1ED7 + 'i tao')) { return $true }
    if ($Text.IndexOf('HVAC', [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $Text.IndexOf([char]0x1ED3) -ge 0) { return $true } # o with horn+dot below often in "do"
    return $false
}

function Wrap-HvacTryBlocks([string]$Text) {
    if ($Text -match 'MepDocumentContext\.Run\s*\(') {
        return $Text
    }

    if (-not (Test-HvacErrorMessage $Text)) {
        return $Text
    }

    # Find last "try" before an HVAC-related catch message
    $needle = 'HVAC'
    $msgIdx = $Text.LastIndexOf($needle, [StringComparison]::OrdinalIgnoreCase)
    if ($msgIdx -lt 0) { return $Text }

    $tryIdx = $Text.LastIndexOf('try', $msgIdx, [StringComparison]::Ordinal)
    if ($tryIdx -lt 0) { return $Text }

    $braceOpen = $Text.IndexOf([char]123, $tryIdx)
    if ($braceOpen -lt 0 -or $braceOpen -gt ($tryIdx + 40)) {
        return $Text
    }

    $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
    if ($braceClose -lt 0) {
        return $Text
    }

    $afterLen = [Math]::Min(80, $Text.Length - $braceClose - 1)
    $after = $Text.Substring($braceClose + 1, $afterLen)
    if ($after -notmatch '^\s*catch\b') {
        return $Text
    }

    $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
    if ($inner -match 'MepDocumentContext\.Run') {
        return $Text
    }

    $openWrap = "`r`n            MepDocumentContext.Run(() =>`r`n            " + [char]123
    $closeWrap = "`r`n            " + [char]125 + ");`r`n            "

    $wrapped =
        $Text.Substring(0, $braceOpen + 1) +
        $openWrap +
        $inner +
        $closeWrap +
        $Text.Substring($braceClose)

    if ($wrapped -notmatch 'using\s+MepPanelMvp\.UI') {
        if ($wrapped -match '(?m)^namespace\s+') {
            $wrapped = [regex]::Replace($wrapped, '(?m)^(namespace\s+)', "using MepPanelMvp.UI;`r`n`r`n`$1", 1)
        }
        else {
            $wrapped = "using MepPanelMvp.UI;`r`n" + $wrapped
        }
    }

    return $wrapped
}

$patched = 0
$candidates = Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '\\(bin|obj)\\' -and
        $_.Name -ne 'MepDocumentContext.cs'
    }

foreach ($f in $candidates) {
    $raw = Read-Utf8 $f.FullName
    $nameHit = ($f.Name -match 'Hvac|HVAC|DieuHoa|SupplyAir|AirCondition')
    $textHit = (Test-HvacErrorMessage $raw)
    if (-not $nameHit -and -not $textHit) { continue }
    if ($raw -notmatch 'StartTransaction' -and -not $textHit) { continue }

    $newText = Wrap-HvacTryBlocks $raw
    if ($newText -ne $raw) {
        Write-Utf8NoBom $f.FullName $newText
        Write-Host ("   OK wrap MepDocumentContext.Run -> " + $f.FullName)
        $patched++
        continue
    }

    if ($raw -match 'StartTransaction' -and $raw -notmatch 'MepDocumentContext\.Run' -and $raw -notmatch 'LockDocument\s*\(') {
        Write-Host ("   WARN: " + $f.Name + " has StartTransaction but auto-wrap skipped.")
    }
}

if ($patched -eq 0) {
    Write-Host "   Helper copied. Auto-wrap found 0 files."
    Write-Host "   If still eLockViolation, wrap CAD draw code with MepDocumentContext.Run."
}
else {
    Write-Host ("   Patched " + $patched + " file(s).")
}

Write-Host "   Done HVAC eLock fix."
