# Chi giu lenh MEPDB tren command line. Comment moi [CommandMethod] khac MEPDB.
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

$script:TotalCommented = 0
$script:TotalFiles = 0

function Get-CommandNameFromAttribute {
    param([string]$AttributeText)

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

    $original = $text
    $localCommented = 0

    # Comment tung dong [CommandMethod(...)] neu khong phai MEPDB chinh xac
    $text = [regex]::Replace($text, '(?m)^(?<indent>\s*)\[(?<attr>CommandMethod[^\]]*\])', {
        param($m)
        $cmd = Get-CommandNameFromAttribute $m.Groups['attr'].Value
        if ($null -eq $cmd) {
            return $m.Value
        }
        if ($cmd -eq 'MEPDB') {
            return $m.Value
        }
        if ($cmd -like 'MEP*') {
            $script:TotalCommented++
            $localCommented++
            return "$($m.Groups['indent'].Value)// SINGLE_ENTRY_PATCH: [$($m.Groups['attr'].Value)]"
        }
        return $m.Value
    })

    # Doi license guard trong file
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
Get-ChildItem -Path $autoCadRoot -Filter *.cs -Recurse | ForEach-Object {
    Patch-CsFile -Path $_.FullName
}

if ($script:TotalCommented -eq 0) {
    Write-Host "   CANH BAO: Khong tim thay lenh phu MEP* de an. Kiem tra source MepPanelMvp."
}
else {
    Write-Host "   Da an $($script:TotalCommented) [CommandMethod] phu trong $($script:TotalFiles) file."
}
