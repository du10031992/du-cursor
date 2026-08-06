# Khoi phuc tat ca [CommandMethod] MEP* da bi an boi single-entry patch.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$restored = 0
$files = 0

Write-Host "==> Restore: mo lai cac lenh MEP* (MEPDBDRAW, MEPDBCABINET2D, ...)"
Get-ChildItem -Path $PluginSourceRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }

    $text = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($text -notmatch 'SINGLE_ENTRY_PATCH') { return }

    $count = ([regex]::Matches($text, '// SINGLE_ENTRY_PATCH:')).Count
    $newText = [regex]::Replace($text, '// SINGLE_ENTRY_PATCH:\s*', '')

    if ($newText -ne $text) {
        Set-Content -Path $_.FullName -Value $newText -Encoding UTF8 -NoNewline
        $restored += $count
        $files++
        Write-Host "   mo lai $count lenh -> $($_.Name)"
    }
}

Write-Host "   Da mo lai $restored lenh trong $files file."
