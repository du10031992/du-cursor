# Chi giu lenh MEPDB tren command line. Cac lenh MEP* khac chi goi tu panel.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$autoCadRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD"
if (-not (Test-Path $autoCadRoot)) {
    Write-Host "   (bo qua single-entry patch - khong tim thay src\MepPanel.AutoCAD)"
    exit 0
}

function Test-ExactMepDbCommand {
    param([string]$Line)
    return $Line -match 'CommandMethod\s*\(\s*"MEPDB"\s*[,)]'
}

function Test-MepSubCommand {
    param([string]$Line)
    if ($Line -notmatch 'CommandMethod\s*\(\s*"(MEP[^"]+)"') {
        return $false
    }
    $name = $Matches[1]
    return $name -ne "MEPDB"
}

function Patch-CsFile {
    param([string]$Path)

    $lines = Get-Content $Path -Encoding UTF8
    $changed = $false
    $activeSubFeature = $null
    $inEntryMethod = $false
    $result = New-Object System.Collections.Generic.List[string]

    foreach ($line in $lines) {
        $newLine = $line

        if (Test-ExactMepDbCommand $line) {
            $inEntryMethod = $true
            $activeSubFeature = $null
        }
        elseif (Test-MepSubCommand $line) {
            $activeSubFeature = $Matches[1]
            $inEntryMethod = $false
            if ($line -notmatch 'SINGLE_ENTRY_PATCH') {
                $indent = if ($line -match '^(\s*)') { $Matches[1] } else { "" }
                $newLine = "${indent}// SINGLE_ENTRY_PATCH: $($line.Trim())"
                $changed = $true
            }
        }
        elseif ($line -match '^\s*(public|private|protected|internal)\s+\w') {
            if ($line -notmatch 'CommandMethod') {
                $inEntryMethod = $false
                $activeSubFeature = $null
            }
        }

        if ($line -match 'LicenseGuard\.(EnsureAuthorized|EnsureFeature)\s*\(') {
            if ($inEntryMethod) {
                $replacement = 'LicenseGuard.EnsureEntry()'
                $updated = $line -replace 'LicenseGuard\.(EnsureAuthorized|EnsureFeature)\s*\([^)]*\)', $replacement
                if ($updated -ne $line) {
                    $newLine = $updated
                    $changed = $true
                }
            }
            elseif ($activeSubFeature) {
                $replacement = "LicenseGuard.EnsureSubFeature(`"$activeSubFeature`")"
                $updated = $line -replace 'LicenseGuard\.(EnsureAuthorized|EnsureFeature)\s*\([^)]*\)', $replacement
                if ($updated -ne $line) {
                    $newLine = $updated
                    $changed = $true
                }
            }
        }

        if ($line -match 'PluginFeatureGate\.Ensure\s*\(' -and $activeSubFeature) {
            $updated = $line -replace 'PluginFeatureGate\.Ensure\s*\([^)]*\)', "PluginFeatureGate.Ensure(`"$activeSubFeature`")"
            if ($updated -ne $line) {
                $newLine = $updated
                $changed = $true
            }
        }

        $result.Add($newLine)
    }

    if ($changed) {
        Set-Content -Path $Path -Value $result -Encoding UTF8
        Write-Host "   OK single-entry -> $Path"
    }
}

Write-Host "==> Apply single-entry patch (chi lenh MEPDB)"
Get-ChildItem -Path $autoCadRoot -Filter *.cs -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match 'CommandMethod\s*\(\s*"MEP') {
        Patch-CsFile -Path $_.FullName
    }
}
