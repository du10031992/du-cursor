# Khoi phuc tat ca [CommandMethod] MEP* da bi an boi single-entry patch.
# Sua ca truong hop attribute nhieu dong (// chi comment dong dau -> CS1519 ']').
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$restored = 0
$files = 0

function Get-LeadingIndent {
    param([string]$Line)
    if ($Line -match '^(\s*)') { return $Matches[1] }
    return '        '
}

function Repair-CommandMethodFile {
    param([string]$Path)

    $lines = [System.Collections.Generic.List[string]](Get-Content $Path -Encoding UTF8)
    $changed = $false
    $localRestored = 0
    $out = New-Object System.Collections.Generic.List[string]
    $i = 0

    while ($i -lt $lines.Count) {
        $line = $lines[$i]

        # Block comment form: /* SINGLE_ENTRY_PATCH: [CommandMethod(...)] */
        if ($line -match '^\s*/\*\s*SINGLE_ENTRY_PATCH:\s*(\[CommandMethod[\s\S]*?\])\s*\*/\s*$') {
            $indent = Get-LeadingIndent $line
            $out.Add("$indent$($Matches[1])")
            $localRestored++
            $changed = $true
            $i++
            continue
        }

        # Line comment: // SINGLE_ENTRY_PATCH: [CommandMethod(...)]  (1 dong hoac mo dau nhieu dong)
        if ($line -match '^\s*//\s*SINGLE_ENTRY_PATCH:\s*(.*)$') {
            $indent = Get-LeadingIndent $line
            $attrLines = New-Object System.Collections.Generic.List[string]
            $firstPart = $Matches[1].TrimEnd()
            $attrLines.Add($firstPart)

            if ($firstPart -notmatch '\)\]\s*$') {
                $i++
                while ($i -lt $lines.Count) {
                    $next = $lines[$i]
                    if ($next -match '^\s*//\s*SINGLE_ENTRY_PATCH:\s*(.*)$') {
                        $attrLines.Add($Matches[1].TrimEnd())
                    }
                    else {
                        $attrLines.Add($next.TrimEnd())
                    }

                    if ($attrLines[$attrLines.Count - 1] -match '\)\]\s*$') {
                        $i++
                        break
                    }
                    $i++
                }
            }
            else {
                $i++
            }

            $merged = ($attrLines -join ' ').Trim()
            $merged = [regex]::Replace($merged, '\s+', ' ')
            $out.Add("$indent$merged")
            $localRestored++
            $changed = $true
            continue
        }

        # Legacy / manual: // [CommandMethod( ... nhieu dong ... )]
        if ($line -match '^\s*//\s*(\[CommandMethod\b.*)$') {
            $indent = Get-LeadingIndent $line
            $attrLines = New-Object System.Collections.Generic.List[string]
            $attrLines.Add($Matches[1].TrimEnd())

            if ($Matches[1] -notmatch '\)\]\s*$') {
                $i++
                while ($i -lt $lines.Count) {
                    $next = $lines[$i].TrimEnd()
                    if ($next -match '^\s*//\s*(.+)$') { $next = $Matches[1] }
                    $attrLines.Add($next)
                    if ($next -match '\)\]\s*$') {
                        $i++
                        break
                    }
                    $i++
                }
            }
            else {
                $i++
            }

            $merged = ($attrLines -join ' ').Trim()
            $merged = [regex]::Replace($merged, '\s+', ' ')
            $out.Add("$indent$merged")
            $localRestored++
            $changed = $true
            continue
        }

        # Don rac: dong chi co )] hoac ] sau khi attribute bi vo
        if ($line -match '^\s*\)\]\s*$' -or $line -match '^\s*\]\s*$') {
            $prev = if ($out.Count -gt 0) { $out[$out.Count - 1] } else { '' }
            if ($prev -notmatch '\[CommandMethod' -and $prev -notmatch '\)\]\s*$') {
                $changed = $true
                $i++
                continue
            }
        }

        # Don rac: dong param attribute bi roi ( "MEP_PANEL", ... ) khi dong truoc khong phai [CommandMethod
        if ($line -match '^\s*"[^"]*"\s*,?\s*$' -or $line -match '^\s*CommandFlags\.') {
            $prev = if ($out.Count -gt 0) { $out[$out.Count - 1] } else { '' }
            if ($prev -match '^\s*//\s*SINGLE_ENTRY_PATCH:' -or ($prev -notmatch '\[CommandMethod' -and $prev -notmatch '\(\s*$')) {
                $changed = $true
                if ($line -match '\)\]\s*$') { $i++; continue }
                $i++
                continue
            }
        }

        # Sua ]]] thua tren cung dong
        if ($line -match '\[CommandMethod' -and $line -match '\]\]') {
            $fixed = [regex]::Replace($line, '\]\s*\]', ']')
            if ($fixed -ne $line) {
                $line = $fixed
                $changed = $true
            }
        }

        $out.Add($line)
        $i++
    }

    if ($changed) {
        Set-Content -Path $Path -Value ($out -join "`r`n") -Encoding UTF8
    }

    return $localRestored
}

Write-Host "==> Restore: mo lai cac lenh MEP* (MEPDBDRAW, MEPDBCABINET2D, ...)"
Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }

    $text = Get-Content $_.FullName -Raw -Encoding UTF8
    $needsRepair = $text -match 'SINGLE_ENTRY_PATCH|//\s*\[CommandMethod|/\*\s*SINGLE_ENTRY_PATCH'
    if (-not $needsRepair) { return }

    $count = Repair-CommandMethodFile -Path $_.FullName
    if ($count -gt 0) {
        $restored += $count
        $files++
        Write-Host "   mo lai $count lenh -> $($_.Name)"
    }
}

Write-Host "   Da mo lai $restored lenh trong $files file."
