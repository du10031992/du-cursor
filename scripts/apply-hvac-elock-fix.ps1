# Fix eLockViolation khi tao so do HVAC tu cua so WPF modeless.
# Copy MepDocumentContext.cs + boc try { ve CAD } bang MepDocumentContext.Run.
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
        if ($ch -eq '{') { $depth++ }
        elseif ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) { return $i }
        }
    }
    return -1
}

function Wrap-HvacTryBlocks([string]$Text) {
    if ($Text -match 'MepDocumentContext\.Run\s*\(') {
        return $Text
    }

    # Tim chuoi loi HVAC (unicode hoac ascii)
    $rxMsg = [regex]'L[oô]i\s+t[aạ]o\s+s[oơ]\s+[dđ][oồ]\s+HVAC|Loi tao so do HVAC|tạo sơ đồ HVAC|tao so do HVAC'
    $m = $rxMsg.Match($Text)
    if (-not $m.Success) {
        return $Text
    }

    # Tim try { gan nhat truoc chuoi loi (thuong la try cua catch hien message)
    $searchFrom = $m.Index
    $tryIdx = $Text.LastIndexOf('try', $searchFrom, [StringComparison]::Ordinal)
    if ($tryIdx -lt 0) {
        return $Text
    }

    # Bo qua "try" nam trong chuoi/comment thô - can { ngay sau
    $braceOpen = $Text.IndexOf('{', $tryIdx)
    if ($braceOpen -lt 0 -or $braceOpen -gt ($tryIdx + 40)) {
        return $Text
    }

    $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
    if ($braceClose -lt 0) {
        return $Text
    }

    # Chi wrap neu catch nam ngay sau block nay
    $after = $Text.Substring($braceClose + 1, [Math]::Min(80, $Text.Length - $braceClose - 1))
    if ($after -notmatch '^\s*catch\b') {
        return $Text
    }

    $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)
    if ($inner -match 'MepDocumentContext\.Run') {
        return $Text
    }

    $wrapped =
        $Text.Substring(0, $braceOpen + 1) +
        "`r`n            MepDocumentContext.Run(() =>`r`n            {" +
        $inner +
        "`r`n            });`r`n            " +
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
    if ($raw -notmatch 'HVAC' -and $raw -notmatch 'Hvac' -and $raw -notmatch 'DieuHoa' -and $raw -notmatch 'dieu hoa') {
        continue
    }
    if ($raw -notmatch 'L[oô]i\s+t[aạ]o\s+s[oơ]|Loi tao so do|sơ đồ HVAC|so do HVAC|StartTransaction') {
        continue
    }

    # Chi xu ly file co message loi HVAC hoac ten file lien quan
    $isTarget = ($raw -match 'L[oô]i\s+t[aạ]o\s+s[oơ]\s+[dđ][oồ]\s+HVAC|Loi tao so do HVAC|tạo sơ đồ HVAC') -or
                ($f.Name -match 'Hvac|HVAC|DieuHoa|SupplyAir|AirCondition')
    if (-not $isTarget) { continue }

    $newText = Wrap-HvacTryBlocks $raw
    if ($newText -ne $raw) {
        Write-Utf8NoBom $f.FullName $newText
        Write-Host "   OK wrap MepDocumentContext.Run -> $($f.FullName)"
        $patched++
        continue
    }

    # Fallback: file HVAC co StartTransaction nhung chua LockDocument / MepDocumentContext
    if ($raw -match 'StartTransaction' -and $raw -notmatch 'MepDocumentContext\.Run' -and $raw -notmatch 'LockDocument\s*\(') {
        Write-Host "   CANH BAO: $($f.Name) co StartTransaction nhung chua wrap duoc tu dong."
        Write-Host "             Them: MepDocumentContext.Run(() => { ... ve CAD ... });"
    }
}

if ($patched -eq 0) {
    Write-Host "   (Khong tu wrap duoc file message loi - helper da copy.)"
    Write-Host "   Neu van eLockViolation: mo file chua chuoi 'tao so do HVAC',"
    Write-Host "   boc doan ve CAD bang MepDocumentContext.Run(() => { ... });"
}
else {
    Write-Host "   Da patch $patched file."
}

Write-Host "   Xong HVAC eLock fix."
