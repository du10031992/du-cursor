# Patch MepPanelMvp command files: EnsureAuthorized() -> EnsureFeature(...) theo nhom lenh.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$autoCadRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD"
if (-not (Test-Path $autoCadRoot)) {
    Write-Host "   (bo qua feature guard patch - khong tim thay src\MepPanel.AutoCAD)"
    exit 0
}

function Get-FeatureForCommandMethodLine {
    param([string]$Line)

    if ($Line -match 'CommandMethod\s*\(\s*"MEPDB[^"]*"') { return "MEPDB" }
    if ($Line -match 'CommandMethod\s*\(\s*"MEPHVAC[^"]*"') { return "MEPHVAC" }
    return $null
}

function Patch-CsFile {
    param([string]$Path)

    $content = Get-Content $Path -Raw -Encoding UTF8
    $lines = Get-Content $Path -Encoding UTF8
    $currentFeature = $null
    $changed = $false
    $result = New-Object System.Collections.Generic.List[string]

    foreach ($line in $lines) {
        $feature = Get-FeatureForCommandMethodLine -Line $line
        if ($feature) {
            $currentFeature = $feature
        }
        elseif ($line -match 'CommandMethod\s*\(' -and $line -notmatch 'MEPDB|MEPHVAC') {
            $currentFeature = $null
        }

        $newLine = $line
        if ($currentFeature -and $line -match 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)') {
            $newLine = $line -replace 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', "LicenseGuard.EnsureFeature(`"$currentFeature`")"
            if ($newLine -ne $line) {
                $changed = $true
            }
        }

        $isDispatcher = $Path -match 'Dispatcher'
        if ($isDispatcher -and $line -match 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)') {
            foreach ($var in @('featureCode', 'requiredFeature', 'feature', 'pluginFeature')) {
                if ($content -match $var) {
                    $newLine = $line -replace 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', "LicenseGuard.EnsureFeature($var)"
                    if ($newLine -ne $line) {
                        $changed = $true
                        break
                    }
                }
            }
        }

        # Dispatcher/helper thuong co tham so featureCode
        if ($line -match 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)' -and
            $line -match 'featureCode|requiredFeature|feature') {
            $newLine = $line -replace 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', 'LicenseGuard.EnsureFeature(featureCode)'
            if ($newLine -ne $line) {
                $changed = $true
            }
        }

        if ($line -match 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)' -and
            $line -match 'PluginFeatures\.MepDb') {
            $newLine = $line -replace 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', 'LicenseGuard.EnsureFeature(PluginFeatures.MepDb)'
            $changed = $true
        }

        if ($line -match 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)' -and
            $line -match 'PluginFeatures\.MepHvac') {
            $newLine = $line -replace 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', 'LicenseGuard.EnsureFeature(PluginFeatures.MepHvac)'
            $changed = $true
        }

        $result.Add($newLine)
    }

    if ($changed) {
        Set-Content -Path $Path -Value $result -Encoding UTF8
        Write-Host "   OK feature guard -> $Path"
    }
}

Write-Host "==> Apply feature guard patch (MEPDB/MEPHVAC)"
Get-ChildItem -Path $autoCadRoot -Filter *.cs -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match 'LicenseGuard\.EnsureAuthorized') {
        Patch-CsFile -Path $_.FullName
    }
}
