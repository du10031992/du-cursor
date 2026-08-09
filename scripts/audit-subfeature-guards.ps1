# Bao cao handler panel da / chua co SUBFEATURE_GUARD sau build patch.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$autoCadRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD"
if (-not (Test-Path $autoCadRoot)) {
    Write-Host "Khong tim thay $autoCadRoot"
    exit 1
}

Write-Host "==> Audit subfeature guards trong MepPanel.AutoCAD"
$guarded = @()
$unguarded = @()

Get-ChildItem -Path $autoCadRoot -Filter *.cs -Recurse | ForEach-Object {
    if ($_.FullName -match '\\(bin|obj)\\') { return }

    $lines = Get-Content $_.FullName -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -notmatch '^\s*(?:public|private|protected|internal)\s+(?:async\s+)?(?:void|bool|Task)\s+(?<name>\w+)\s*\(') {
            continue
        }

        $name = $Matches['name']
        if ($name -notmatch '(?i)Click|Hvac|Water|Fire|Cabinet|Export|Power|Layer|Electrical|DieuHoa|Config|Excel|Smoke|Plumbing|AirCondition') {
            continue
        }

        $hasGuard = $false
        for ($j = $i + 1; $j -le [Math]::Min($i + 8, $lines.Count - 1); $j++) {
            if ($lines[$j] -match 'SUBFEATURE_GUARD') { $hasGuard = $true; break }
            if ($lines[$j] -match '^\s*\}') { break }
        }

        $rel = $_.FullName.Substring($PluginSourceRoot.Length).TrimStart('\')
        $entry = "$rel :: $name"
        if ($hasGuard) { $guarded += $entry } else { $unguarded += $entry }
    }
}

Write-Host ""
Write-Host "CO GUARD ($($guarded.Count)):"
$guarded | Sort-Object | ForEach-Object { Write-Host "  [OK] $_" }

Write-Host ""
Write-Host "CHUA CO GUARD ($($unguarded.Count)) - can bo sung vao apply-subfeature-guard-patch.ps1:"
$unguarded | Sort-Object | ForEach-Object { Write-Host "  [!!] $_" }

if ($unguarded.Count -gt 0) {
    exit 2
}
