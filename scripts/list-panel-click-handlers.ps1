# Liet ke tat ca void *_Click trong panel UI de bo sung guard map.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$uiRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD"
Write-Host "==> Panel *_Click handlers trong $uiRoot"
Write-Host ""

Get-ChildItem -Path $uiRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj|Licensing)\\') { return }

    $lines = Get-Content $_.FullName -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -notmatch '^\s*(?:public|private|protected|internal)\s+(?:async\s+)?void\s+(?<name>\w+)\s*\(') {
            continue
        }
        $name = $Matches['name']
        if ($name -notmatch '_Click$') { continue }

        $rel = $_.FullName.Substring($PluginSourceRoot.Length).TrimStart('\')
        $hasGuard = $false
        for ($j = $i + 1; $j -le [Math]::Min($i + 8, $lines.Count - 1); $j++) {
            if ($lines[$j] -match 'SUBFEATURE_GUARD|PluginFeatureGate\.Ensure') { $hasGuard = $true; break }
        }

        $flag = if ($hasGuard) { "[OK]" } else { "[!!]" }
        Write-Host "$flag $rel :: $name"
    }
}
