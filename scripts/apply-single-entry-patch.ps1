# Chi giu lenh MEPDB tren command line. Comment moi [CommandMethod] khac MEPDB.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$script:TotalCommented = 0
$script:TotalFiles = 0
$script:ScannedFiles = 0

function Get-CommandNameFromAttribute {
    param([string]$AttributeText)

    # [CommandMethod("Group", "MEPDBCABINET2D", ...)]
    if ($AttributeText -match 'CommandMethod\s*\(\s*"[^"]*"\s*,\s*"([^"]+)"') {
        return $Matches[1]
    }

    # [CommandMethod("MEPDB", ...)]
    if ($AttributeText -match 'CommandMethod\s*\(\s*"([^"]+)"') {
        return $Matches[1]
    }

    return $null
}

function Patch-CsFile {
    param([string]$Path)

    $text = Get-Content $Path -Raw -Encoding UTF8
    if ($text -notmatch 'CommandMethod') {
        return
    }

    $script:ScannedFiles++
    $original = $text
    $localCommented = 0

    # Match [CommandMethod(...)] ke ca nhieu dong
    $text = [regex]::Replace($text, '\[CommandMethod\s*\((?:[^[\]]|\[[^\]]*\])*\)\]', {
        param($m)

        if ($m.Value -match 'SINGLE_ENTRY_PATCH') {
            return $m.Value
        }

        $cmd = Get-CommandNameFromAttribute $m.Value
        if ($null -eq $cmd) {
            return $m.Value
        }

        if ($cmd -eq 'MEPDB') {
            return $m.Value
        }

        if ($cmd -like 'MEP*') {
            $script:TotalCommented++
            $localCommented++
            # Block comment khi attribute nhieu dong — tranh // chi an dong dau (loi CS1519).
            if ($m.Value -match '[\r\n]') {
                return "/* SINGLE_ENTRY_PATCH: $($m.Value) */"
            }
            return "// SINGLE_ENTRY_PATCH: $($m.Value)"
        }

        return $m.Value
    })

    $text = [regex]::Replace($text, 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', 'LicenseGuard.EnsureEntry()')
    $text = [regex]::Replace($text, 'LicenseGuard\.EnsureFeature\s*\(\s*"MEPDB"\s*\)', 'LicenseGuard.EnsureEntry()')
    $text = [regex]::Replace($text, 'LicenseGuard\.EnsureFeature\s*\(\s*PluginFeatures\.MepDb\s*\)', 'LicenseGuard.EnsureEntry()')

    if ($text -ne $original) {
        Set-Content -Path $Path -Value $text -Encoding UTF8 -NoNewline
        $script:TotalFiles++
        Write-Host "   OK single-entry -> $Path ($localCommented lenh phu da an)"
    }
}

Write-Host "==> Apply single-entry patch (chi lenh MEPDB)"
Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') {
        return
    }
    Patch-CsFile -Path $_.FullName
}

Write-Host "   Da quet $script:ScannedFiles file co CommandMethod."
if ($script:TotalCommented -eq 0) {
    Write-Host "   CANH BAO: Khong tim thay lenh phu MEP* de an."
    Write-Host "   Kiem tra PanelCommands.cs / HvacCommands.cs trong MepPanelMvp."
}
else {
    Write-Host "   Da an $($script:TotalCommented) [CommandMethod] phu trong $($script:TotalFiles) file."
}
