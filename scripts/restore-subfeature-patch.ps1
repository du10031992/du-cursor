# Go bo dong SUBFEATURE_GUARD bi chen sai (chay truoc build neu loi compile).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$removed = 0

Write-Host "==> Restore: xoa SUBFEATURE_GUARD bi chen sai"
Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }
    $lines = Get-Content $_.FullName -Encoding UTF8
    $newLines = $lines | Where-Object { $_ -notmatch 'SUBFEATURE_GUARD' }
    if ($newLines.Count -lt $lines.Count) {
        $count = $lines.Count - $newLines.Count
        $removed += $count
        Set-Content -Path $_.FullName -Value ($newLines -join "`r`n") -Encoding UTF8
        Write-Host "   xoa $count dong -> $($_.FullName)"
    }
}

Write-Host "   Da xoa $removed dong guard."
