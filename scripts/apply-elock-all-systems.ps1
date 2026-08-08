# 1) Go BO wrap MEP_ELOCK_* thua (phuc hoi method goc)
# 2) Chi wrap ENTRY POINT (Commands + UI Click) — khong wrap helper Cad
# 3) Copy MepDocumentContext smart (sync khi da trong command context)
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$log = New-Object System.Collections.Generic.List[string]
function Log([string]$m) { $log.Add($m); Write-Host "   $m" }

Write-Host "==> eLock AUDIT FIX (revert overwrap + entry-only)"

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

# Go: // MEP_ELOCK_XXX \n MepDocumentContext.Run(() => \n { INNER });
function Remove-ElockWraps([string]$Text) {
    $removed = 0
    $guard = 0
    while ($guard -lt 100 -and $Text -match 'MEP_ELOCK_') {
        $guard++
        $markerIdx = $Text.IndexOf('MEP_ELOCK_', [StringComparison]::Ordinal)
        if ($markerIdx -lt 0) { break }

        # tim MepDocumentContext.Run sau marker
        $runIdx = $Text.IndexOf('MepDocumentContext.Run', $markerIdx, [StringComparison]::Ordinal)
        if ($runIdx -lt 0 -or $runIdx -gt ($markerIdx + 120)) {
            # marker rac - xoa dong comment
            $lineStart = $Text.LastIndexOf("`n", $markerIdx)
            if ($lineStart -lt 0) { $lineStart = 0 } else { $lineStart++ }
            $lineEnd = $Text.IndexOf("`n", $markerIdx)
            if ($lineEnd -lt 0) { break }
            $Text = $Text.Remove($lineStart, $lineEnd - $lineStart + 1)
            $removed++
            continue
        }

        $arrowIdx = $Text.IndexOf('=>', $runIdx, [StringComparison]::Ordinal)
        if ($arrowIdx -lt 0) { break }
        $braceOpen = $Text.IndexOf([char]123, $arrowIdx)
        if ($braceOpen -lt 0) { break }
        $braceClose = Find-MatchingBrace -Text $Text -OpenIndex $braceOpen
        if ($braceClose -lt 0) { break }

        $inner = $Text.Substring($braceOpen + 1, $braceClose - $braceOpen - 1)

        # bo ");" sau closing brace cua lambda
        $after = $braceClose + 1
        while ($after -lt $Text.Length -and [char]::IsWhiteSpace($Text[$after])) { $after++ }
        if ($after -lt $Text.Length -and $Text[$after] -eq ')') { $after++ }
        if ($after -lt $Text.Length -and $Text[$after] -eq ';') { $after++ }

        # xoa tu dau dong comment marker
        $lineStart = $Text.LastIndexOf("`n", $markerIdx)
        if ($lineStart -lt 0) { $lineStart = 0 } else { $lineStart++ }

        $Text = $Text.Substring(0, $lineStart) + $inner + $Text.Substring($after)
        $removed++
    }
    return @{ Text = $Text; Count = $removed }
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
    if ($inner -match 'MepDocumentContext\.Run\s*\(') {
        return @{ Text = $Text; Count = 0; Reason = "already" }
    }

    $wrapped =
        $Text.Substring(0, $braceOpen + 1) +
        "`r`n            // $Marker`r`n            MepDocumentContext.Run(() =>`r`n            " +
        [char]123 + $inner + "`r`n            " + [char]125 + ");`r`n            " +
        $Text.Substring($braceClose)
    return @{ Text = $wrapped; Count = 1; Reason = "patched" }
}

# --- copy smart helpers ---
$uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"
if (-not (Test-Path $uiDir)) { $uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui" }
if (-not (Test-Path $uiDir)) {
    New-Item -ItemType Directory -Force -Path (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI") | Out-Null
    $uiDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"
}
Copy-Item (Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\UI\MepDocumentContext.cs") (Join-Path $uiDir "MepDocumentContext.cs") -Force
Log "OK MepDocumentContext.cs (smart context)"

$wfSrc = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Commands\WaterFireCommands.cs"
$wfDst = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands\WaterFireCommands.cs"
if (Test-Path $wfSrc) {
    Copy-Item $wfSrc $wfDst -Force
    Log "OK WaterFireCommands.cs"
}

# --- REVERT all overwraps under src\MepPanel.AutoCAD ---
$autoRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD"
$revertedFiles = 0
$revertedWraps = 0
Get-ChildItem $autoRoot -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|package_staging)\\' -and $_.Name -ne 'MepDocumentContext.cs' -and $_.Name -ne 'WaterFireCommands.cs' } |
    ForEach-Object {
        $raw = Read-Utf8 $_.FullName
        if ($raw -notmatch 'MEP_ELOCK_') { return }
        $r = Remove-ElockWraps $raw
        if ($r.Count -gt 0) {
            Write-Utf8NoBom $_.FullName $r.Text
            Log ("REVERT " + $_.Name + " wraps=" + $r.Count)
            $script:revertedFiles++
            $script:revertedWraps += $r.Count
        }
    }
Log ("REVERT total files=" + $revertedFiles + " wraps=" + $revertedWraps)

# --- ENTRY ONLY wraps ---
function Wrap-EntryMethodsInFile([string]$Path, [string]$Label) {
    if (-not (Test-Path $Path)) {
        Log ("MISSING " + $Label + ": " + $Path)
        return
    }
    $t = Read-Utf8 $Path
    $rx = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?(async\s+)?void\s+(\w+)\s*\('
    $names = @()
    foreach ($m in $rx.Matches($t)) {
        $name = $m.Groups[4].Value
        if ($name -match '^(HeNuoc_Click|BaoChay_Click)$') { continue }
        $open = $t.IndexOf([char]123, $m.Index)
        if ($open -lt 0) { continue }
        $close = Find-MatchingBrace $t $open
        if ($close -lt 0) { continue }
        $body = $t.Substring($open, $close - $open)
        $hit = $false
        if ($body -match 'StartTransaction') { $hit = $true }
        if ($body -match 'BuildSchematic|DrawingService|TransactionManager|AppendEntity') { $hit = $true }
        if ($body -match 'HvacSupplyAir|PanelDrawing|CabinetLayout|RealisticWiring|ThreePhase|PowerDevice|CustomDevice') { $hit = $true }
        if ($body -match 'so do HVAC|tao so do|LockViolation') { $hit = $true }
        if ($name -match '(_Click|Draw|Build|Create|Insert|Place|Export|Render|Duplicate|EditPanel|SelectSame|Schematic|SoDo)') {
            if ($body -match 'Cad\.|DrawingService|BuildSchematic|StartTransaction|Database|HvacSupply') { $hit = $true }
        }
        if ($hit) { $names += $name }
    }
    if ($names.Count -eq 0 -and $t -match 'BuildSchematic|StartTransaction') {
        Log ("WARN " + $Label + ": has CAD calls but no void entry matched")
    }
    foreach ($name in ($names | Select-Object -Unique)) {
        $t = Read-Utf8 $Path
        $r = Wrap-NamedVoidMethod $t $name ("MEP_ELOCK_ENTRY_" + $name)
        Log ("ENTRY " + $Label + "." + $name + ": " + $r.Reason)
        if ($r.Count -gt 0) { Write-Utf8NoBom $Path (Ensure-UsingUi $r.Text) }
    }
}

Wrap-EntryMethodsInFile (Join-Path $autoRoot "Commands\HvacCommands.cs") "HvacCommands"
Wrap-EntryMethodsInFile (Join-Path $autoRoot "Commands\PanelCommands.cs") "PanelCommands"

# Cua so WPF + ElectricalToolControl: quet de quy toan bo AutoCAD
$xamlFiles = Get-ChildItem $autoRoot -Filter *.xaml.cs -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|package_staging)\\' }

Log ("XAML.CS found=" + @($xamlFiles).Count)
foreach ($f in $xamlFiles) {
    $rel = $f.FullName.Substring($autoRoot.Length).TrimStart('\', '/')
    Log ("SCAN " + $rel)
    # Dump method signatures for key windows (evidence, khong doan)
    if ($f.Name -match 'HvacConfiguration|PanelConfiguration|ElectricalToolControl') {
        $raw = Read-Utf8 $f.FullName
        Log ("  size=" + $raw.Length)
        $rxAll = [regex]'(?m)^\s*((?:public|private|internal|protected)\s+(?:static\s+)?(?:async\s+)?[\w\.<>,\[\]\?]+\s+(\w+)\s*\([^;]*\))\s*\{?'
        $count = 0
        foreach ($m in $rxAll.Matches($raw)) {
            $sig = ($m.Groups[1].Value -replace '\s+', ' ').Trim()
            $name = $m.Groups[2].Value
            if ($name -match '^(get_|set_|add_|remove_)') { continue }
            $open = $raw.IndexOf([char]123, $m.Index)
            $flags = @()
            if ($open -gt 0) {
                $close = Find-MatchingBrace $raw $open
                if ($close -gt 0) {
                    $body = $raw.Substring($open, [Math]::Min(5000, $close - $open))
                    if ($body -match 'BuildSchematic') { $flags += 'BuildSchematic' }
                    if ($body -match 'StartTransaction') { $flags += 'StartTransaction' }
                    if ($body -match 'DrawingService|HvacSupply|PanelDrawing|Cad\.') { $flags += 'CadCall' }
                    if ($body -match 'LockDocument') { $flags += 'LockDocument' }
                    if ($body -match 'MepDocumentContext') { $flags += 'MepDocumentContext' }
                }
            }
            if ($flags.Count -gt 0 -or $name -match 'Click|Draw|Build|Create|Schematic|SoDo|Export|Render') {
                Log ("  METHOD " + $sig + " [" + ($flags -join ',') + "]")
                $count++
            }
        }
        Log ("  dumpedMethods=" + $count)
    }
    Wrap-EntryMethodsInFile $f.FullName $f.Name
}

# Force: moi file goi BuildSchematic (thuong la HvacConfigurationWindow / ViewModel)
Get-ChildItem $autoRoot -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|package_staging|Cad)\\' } |
    ForEach-Object {
        $raw = Read-Utf8 $_.FullName
        if ($raw -notmatch 'BuildSchematic|StartTransaction') { return }
        Log ("HAS CAD WRITE: " + $_.Name)
        Wrap-EntryMethodsInFile $_.FullName $_.Name
    }

# Draw_Click chi sua WPF model + Queue command. KHONG wrap UI:
# Queue la cua vao command context duy nhat; wrap o day tao nested context/lock.
$auditWindows = @(
    'HvacConfigurationWindow.xaml.cs',
    'PanelConfigurationWindow.xaml.cs'
)
Log "UI CLICK: khong wrap; Queue la command-context entry duy nhat"

# ViewModel / Host: wrap void methods co CAD write (entry thuc su sau CommitEdits)
Get-ChildItem $autoRoot -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '\\(bin|obj|package_staging|Cad)\\' -and
        ($_.Name -match 'ViewModel|ConfigurationHost|HvacConfigurationHost|PaletteHost')
    } |
    ForEach-Object {
        $raw = Read-Utf8 $_.FullName
        if ($raw -notmatch 'BuildSchematic|StartTransaction|DrawingService|AppendEntity|TransactionManager') {
            Log ("VM SCAN " + $_.Name + ": no CAD write keywords")
            return
        }
        Log ("VM SCAN " + $_.Name + ": has CAD write -> wrap entries")
        # Dump method names for evidence
        $rx = [regex]'(?m)^\s*(public|private|internal|protected)\s+(static\s+)?(async\s+)?void\s+(\w+)\s*\('
        foreach ($m in $rx.Matches($raw)) {
            $name = $m.Groups[4].Value
            $open = $raw.IndexOf([char]123, $m.Index)
            if ($open -lt 0) { continue }
            $close = Find-MatchingBrace $raw $open
            if ($close -lt 0) { continue }
            $body = $raw.Substring($open, [Math]::Min(4000, $close - $open))
            $flags = @()
            if ($body -match 'BuildSchematic') { $flags += 'BuildSchematic' }
            if ($body -match 'StartTransaction') { $flags += 'StartTransaction' }
            if ($body -match 'DrawingService|HvacSupply|PanelDrawing|Cad\.') { $flags += 'CadCall' }
            if ($flags.Count -gt 0) {
                Log ("  VM METHOD " + $name + " [" + ($flags -join ',') + "]")
            }
        }
        Wrap-EntryMethodsInFile $_.FullName $_.Name
    }

# Dump Draw_Click bodies (evidence sau patch)
foreach ($win in $auditWindows) {
    $hits = Get-ChildItem $autoRoot -Filter $win -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|package_staging)\\' }
    foreach ($f in $hits) {
        $raw = Read-Utf8 $f.FullName
        $rx = [regex]'(?m)^\s*(?:public|private|internal|protected)\s+(?:static\s+)?void\s+Draw_Click\s*\('
        $m = $rx.Match($raw)
        if (-not $m.Success) { continue }
        $open = $raw.IndexOf([char]123, $m.Index)
        $close = Find-MatchingBrace $raw $open
        if ($open -lt 0 -or $close -lt 0) { continue }
        $body = $raw.Substring($open, [Math]::Min(800, $close - $open + 1)) -replace '\s+', ' '
        Log ("DRAW_BODY " + $f.Name + ": " + $body)
    }
}

# Khong wrap Cad\* helpers.

$logPath = Join-Path $Root "elock-audit-fix.log"
$log | Set-Content $logPath -Encoding UTF8
Write-Host ("   Log: " + $logPath)
Write-Host "   Done audit fix."
