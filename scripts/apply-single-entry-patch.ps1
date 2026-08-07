# Chi giu lenh MEPDB tren command line.
# Cac lenh MEP* khac -> [MepInternalCommand("...")] de panel van goi duoc qua AutoCadCommandDispatcher.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$script:TotalConverted = 0
$script:TotalFiles = 0
$script:ScannedFiles = 0

function Get-CommandNameFromAttribute {
    param([string]$AttributeText)

    if ($AttributeText -match 'CommandMethod\s*\(\s*"[^"]*"\s*,\s*"([^"]+)"') {
        return $Matches[1]
    }
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
    $localConverted = 0

    # Khoi phuc marker cu (comment) neu con sot
    $text = [regex]::Replace($text, '(?ms)/\*\s*SINGLE_ENTRY_PATCH:\s*(\[CommandMethod\b(?:[^[\]]|\[[^\]]*\])*\])\s*\*/', '$1')
    $text = [regex]::Replace($text, '(?m)^\s*//\s*SINGLE_ENTRY_PATCH:\s*(\[CommandMethod\b.*)$', '$1')

    $text = [regex]::Replace($text, '\[CommandMethod\s*\((?:[^[\]]|\[[^\]]*\])*\)\]', {
        param($m)

        if ($m.Value -match 'MepInternalCommand|SINGLE_ENTRY_PATCH') {
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
            $script:TotalConverted++
            $localConverted++
            return "[MepInternalCommand(`"$cmd`")]"
        }

        return $m.Value
    })

    # Neu da thanh MepInternalCommand tu lan truoc, giu nguyen.
    # Entry guard: MEPDB + EnsureAuthorized -> EnsureEntry
    $text = [regex]::Replace($text, 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', 'LicenseGuard.EnsureEntry()')
    $text = [regex]::Replace($text, 'LicenseGuard\.EnsureFeature\s*\(\s*"MEPDB"\s*\)', 'LicenseGuard.EnsureEntry()')
    $text = [regex]::Replace($text, 'LicenseGuard\.EnsureFeature\s*\(\s*PluginFeatures\.MepDb\s*\)', 'LicenseGuard.EnsureEntry()')

    if ($text -match 'MepInternalCommand' -and $text -notmatch 'using\s+MepPanelMvp\.UI') {
        if ($text -match '(?m)^namespace\s+') {
            $text = [regex]::Replace($text, '(?m)^(namespace\s+)', "using MepPanelMvp.UI;`r`n`r`n`$1", 1)
        }
    }

    if ($text -ne $original) {
        Set-Content -Path $Path -Value $text -Encoding UTF8 -NoNewline
        $script:TotalFiles++
        Write-Host "   OK single-entry -> $Path ($localConverted lenh phu -> MepInternalCommand)"
    }
}

function Copy-UiDispatcherPatch {
    $srcDir = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\UI"
    $dstCandidates = @(
        (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI"),
        (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui")
    )

    $dst = $null
    foreach ($c in $dstCandidates) {
        if (Test-Path $c) { $dst = $c; break }
    }
    if (-not $dst) {
        $dst = $dstCandidates[0]
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
    }

    foreach ($name in @("MepInternalCommandAttribute.cs", "AutoCadCommandDispatcher.cs")) {
        $src = Join-Path $srcDir $name
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $dst $name) -Force
            Write-Host "   OK copy $name -> $dst"
        }
    }
}

Write-Host "==> Apply single-entry patch (chi lenh MEPDB tren CLI)"
Copy-UiDispatcherPatch

Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') {
        return
    }
    # Khong patch file dispatcher/attribute vua copy
    if ($_.Name -eq 'AutoCadCommandDispatcher.cs' -or $_.Name -eq 'MepInternalCommandAttribute.cs') {
        return
    }
    Patch-CsFile -Path $_.FullName
}

Write-Host "   Da quet $script:ScannedFiles file co CommandMethod."
if ($script:TotalConverted -eq 0) {
    Write-Host "   CANH BAO: Khong tim thay lenh phu MEP* de chuyen noi bo."
    Write-Host "   Kiem tra PanelCommands.cs / HvacCommands.cs trong MepPanelMvp."
}
else {
    Write-Host "   Da chuyen $($script:TotalConverted) lenh phu -> MepInternalCommand trong $($script:TotalFiles) file."
}
Write-Host "   CLI chi con MEPDB (dang nhap + mo panel)."
