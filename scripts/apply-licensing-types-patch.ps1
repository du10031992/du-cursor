# Dam bao CheckLicenseResponse co the mo rong bang partial class (Features).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$licensingRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Licensing"
if (-not (Test-Path $licensingRoot)) {
    Write-Host "   (bo qua licensing types patch - khong tim thay Licensing)"
    exit 0
}

Write-Host "==> Apply licensing types patch (CheckLicenseResponse.Features)"
$changed = $false

Get-ChildItem -Path $licensingRoot -Filter *.cs -Recurse | ForEach-Object {
    $path = $_.FullName
    $content = Get-Content $path -Raw -Encoding UTF8
    if ($content -notmatch 'class\s+CheckLicenseResponse') {
        return
    }

    if ($content -match 'public\s+partial\s+class\s+CheckLicenseResponse') {
        return
    }

    if ($content -match 'public\s+class\s+CheckLicenseResponse') {
        $newContent = $content -replace 'public\s+class\s+CheckLicenseResponse', 'public partial class CheckLicenseResponse'
        if ($newContent -ne $content) {
            Set-Content -Path $path -Value $newContent -Encoding UTF8
            Write-Host "   OK partial CheckLicenseResponse -> $path"
            $changed = $true
        }
    }
}

if (-not $changed) {
    Write-Host "   (CheckLicenseResponse da partial hoac khong tim thay)"
}
