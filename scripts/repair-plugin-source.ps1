# Sua source MepPanelMvp bi loi do guard inject sai (110 errors compile).
# Chay TRUOC build neu gap loi PanelConfigurationWindow.xaml.cs
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$removed = 0
$files = 0

Write-Host "==> Repair: xoa moi dong guard inject loi trong source plugin"
Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }

    $lines = Get-Content $_.FullName -Encoding UTF8
    $newLines = New-Object System.Collections.Generic.List[string]

    foreach ($line in $lines) {
        if ($line -match 'SUBFEATURE_GUARD') { continue }
        if ($line -match 'PluginFeatureGate\.Ensure\s*\(') { continue }
        if ($line -match 'PanelSystemGuard\.Ensure') { continue }
        $newLines.Add($line)
    }

    if ($newLines.Count -lt $lines.Count) {
        $count = $lines.Count - $newLines.Count
        $removed += $count
        $files++
        Set-Content -Path $_.FullName -Value ($newLines -join "`r`n") -Encoding UTF8
        Write-Host "   xoa $count dong -> $($_.Name)"
    }
}

Write-Host "   Da sua $files file, xoa tong $removed dong guard loi."
if ($removed -eq 0) {
    Write-Host "   (Khong tim thay dong guard — source co the da sach.)"
}
