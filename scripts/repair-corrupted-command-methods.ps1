# Sua PanelCommands.cs / HvacCommands.cs bi vo do single-entry patch + restore cu.
# Truong hop: mat [CommandMethod(, con lai "MEP_PANEL", ... CommandFlags.Modal)]
# -> CS1519 Invalid token ']'. Khong can marker SINGLE_ENTRY_PATCH.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$fixedFiles = 0
$fixedBlocks = 0

function Get-LeadingIndent {
    param([string]$Line)
    if ($Line -match '^(\s*)') { return $Matches[1] }
    return '        '
}

function Test-IsOrphanAttributeStart {
    param([string]$Line)
    return $Line -match '^\s*"[^"]+"\s*,?\s*$'
}

function Test-IsOrphanAttributeContinuation {
    param([string]$Line)
    return $Line -match '^\s*"[^"]+"\s*,?\s*$' -or
           $Line -match '^\s*CommandFlags\.' -or
           ($Line -match '\)\]\s*$' -and $Line -notmatch '\[CommandMethod')
}

function Test-IsMethodOrAttributeLine {
    param([string]$Line)
    return $Line -match '\[CommandMethod' -or
           $Line -match '^\s*(public|private|protected|internal)\s+' -or
           $Line -match '^\s*\[(?!CommandMethod)' -or
           $Line -match '^\s*#region' -or
           $Line -match '^\s*#endregion' -or
           $Line -match '^\s*(\}|{)\s*$' -or
           $Line -match '^\s*namespace\s+' -or
           $Line -match '^\s*public\s+class\s+' -or
           $Line -match '^\s*using\s+'
}

function Build-CommandMethodFromOrphans {
    param([string[]]$Buffer, [string]$Indent)

    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($raw in $Buffer) {
        $t = $raw.Trim()
        if ($t -match '^"([^"]+)"\s*,?\s*$') {
            $parts.Add('"' + $Matches[1] + '"')
            continue
        }
        if ($t -match '^(CommandFlags\.[^)]*(?:\|\s*CommandFlags\.[^)]*)*)\)\]\s*$') {
            $parts.Add($Matches[1])
            continue
        }
        if ($t -match '^(CommandFlags\.[^)]*(?:\|\s*CommandFlags\.[^)]*)*)\)\]\s*,?\s*$') {
            $parts.Add($Matches[1])
            continue
        }
        if ($t -match '\)\]\s*$') {
            $t = $t -replace '\)\]\s*$', ''
            $t = $t.Trim().TrimEnd(',')
            if ($t) { $parts.Add($t) }
        }
    }

    if ($parts.Count -eq 0) { return $null }
    return "$Indent[CommandMethod($($parts -join ', '))]"
}

function Test-FileNeedsOrphanRepair {
    param([string]$Text)
    if ($Text -notmatch 'CommandMethod|CommandFlags\.') { return $false }
    # Dong string literal doc lap truoc method (dau hieu file bi vo)
    return $Text -match '(?m)^\s*"[^"]+"\s*,\s*\r?\n\s*(?:"[^"]+"\s*,\s*\r?\n\s*)*CommandFlags\.[^\r\n]*\)\]\s*\r?\n\s*(?:public|private|protected|internal)\s+'
}

function Try-GitRestoreCommandFile {
    param([string]$Path)

    $gitRoot = $PluginSourceRoot
    for ($n = 0; $n -lt 5; $n++) {
        if (Test-Path (Join-Path $gitRoot '.git')) { break }
        $parent = Split-Path $gitRoot -Parent
        if ($parent -eq $gitRoot) { return $false }
        $gitRoot = $parent
    }
    if (-not (Test-Path (Join-Path $gitRoot '.git'))) { return $false }

    $rel = $Path.Substring($gitRoot.Length).TrimStart('\', '/')
    Push-Location $gitRoot
    try {
        git rev-parse --is-inside-work-tree 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return $false }

        git cat-file -e "HEAD:$($rel -replace '\\','/')" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return $false }

        git checkout HEAD -- $rel 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   git restore -> $rel"
            return $true
        }
    }
    finally {
        Pop-Location
    }
    return $false
}

function Repair-OrphanCommandMethods {
    param([string]$Path)

    $text = Get-Content $Path -Raw -Encoding UTF8
    $isCommandFile = $Path -match 'PanelCommands\.cs$|HvacCommands\.cs$|\\Commands\\'

    if (-not $isCommandFile -and -not (Test-FileNeedsOrphanRepair -Text $text)) { return 0 }

    if (Test-FileNeedsOrphanRepair -Text $text) {
        if (Try-GitRestoreCommandFile -Path $Path) {
            return 1
        }
    }

    $lines = [System.Collections.Generic.List[string]](Get-Content $Path -Encoding UTF8)
    $out = New-Object System.Collections.Generic.List[string]
    $localFixed = 0
    $i = 0

    while ($i -lt $lines.Count) {
        $line = $lines[$i]

        if (Test-IsOrphanAttributeStart -Line $line) {
            $peek = $i + 1
            $looksOrphan = $false
            while ($peek -lt $lines.Count -and $peek -le $i + 8) {
                if ($lines[$peek] -match '\)\]\s*$') { $looksOrphan = $true; break }
                if (Test-IsMethodOrAttributeLine -Line $lines[$peek]) { break }
                $peek++
            }

            if ($looksOrphan) {
                $indent = Get-LeadingIndent $line
                $buffer = New-Object System.Collections.Generic.List[string]
                $buffer.Add($line)
                $i++

                while ($i -lt $lines.Count) {
                    $cur = $lines[$i]
                    if ($cur -match '\)\]\s*$' -and $cur -notmatch '\[CommandMethod') {
                        $buffer.Add($cur)
                        $i++
                        break
                    }
                    if (Test-IsOrphanAttributeContinuation -Line $cur) {
                        $buffer.Add($cur)
                        $i++
                        continue
                    }
                    break
                }

                $rebuilt = Build-CommandMethodFromOrphans -Buffer $buffer.ToArray() -Indent $indent
                if ($rebuilt) {
                    $out.Add($rebuilt)
                    $localFixed++
                    continue
                }

                foreach ($b in $buffer) { $out.Add($b) }
                continue
            }
        }

        # Dong )] doc lap (mat ca phan string phia tren)
        if ($line -match '^\s*CommandFlags\.[^\r\n]*\)\]\s*$' -and $line -notmatch '\[CommandMethod') {
            $indent = Get-LeadingIndent $line
            $rebuilt = Build-CommandMethodFromOrphans -Buffer @($line) -Indent $indent
            if ($rebuilt) {
                $out.Add($rebuilt)
                $localFixed++
                $i++
                continue
            }
        }

        $out.Add($line)
        $i++
    }

    if ($localFixed -gt 0) {
        Set-Content -Path $Path -Value ($out -join "`r`n") -Encoding UTF8
    }

    return $localFixed
}

Write-Host "==> Repair: sua CommandMethod bi vo (CS1519 orphan )])"

$targets = @(
    Join-Path $PluginSourceRoot 'src\MepPanel.AutoCAD\Commands\PanelCommands.cs'
    Join-Path $PluginSourceRoot 'src\MepPanel.AutoCAD\Commands\HvacCommands.cs'
)

Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }
    if ($_.FullName -notmatch 'Commands\\|PanelCommands|HvacCommands|LoaderCommands') { return }
    if ($targets -notcontains $_.FullName) {
        $targets += $_.FullName
    }
}

foreach ($path in $targets) {
    if (-not (Test-Path $path)) { continue }
    $count = Repair-OrphanCommandMethods -Path $path
    if ($count -gt 0) {
        $fixedFiles++
        $fixedBlocks += $count
        Write-Host "   sua $count khoi -> $(Split-Path $path -Leaf)"
    }
}

if ($fixedFiles -eq 0) {
    Write-Host "   (Khong tim thay orphan CommandMethod - file co the da sach hoac can git restore thu cong.)"
}
else {
    Write-Host "   Da sua $fixedBlocks khoi trong $fixedFiles file."
}
