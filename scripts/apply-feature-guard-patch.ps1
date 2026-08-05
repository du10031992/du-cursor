# Patch license guard: MEPDB entry -> EnsureEntry(), giu tuong thich EnsureAuthorized.
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

function Test-ExactMepDbCommand {
    param([string]$Line)
    return $Line -match 'CommandMethod\s*\(\s*"MEPDB"\s*[,)]'
}

function Patch-CsFile {
    param([string]$Path)

    $lines = Get-Content $Path -Encoding UTF8
    $inEntryMethod = $false
    $changed = $false
    $result = New-Object System.Collections.Generic.List[string]

    foreach ($line in $lines) {
        if (Test-ExactMepDbCommand $line) {
            $inEntryMethod = $true
        }
        elseif ($line -match 'CommandMethod\s*\(' -and $line -notmatch 'CommandMethod\s*\(\s*"MEPDB"\s*[,)]') {
            $inEntryMethod = $false
        }
        elseif ($line -match '^\s*(public|private|protected|internal)\s+\w' -and $line -notmatch 'CommandMethod') {
            $inEntryMethod = $false
        }

        $newLine = $line
        if ($inEntryMethod -and $line -match 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)') {
            $newLine = $line -replace 'LicenseGuard\.EnsureAuthorized\s*\(\s*\)', 'LicenseGuard.EnsureEntry()'
            if ($newLine -ne $line) { $changed = $true }
        }

        $result.Add($newLine)
    }

    if ($changed) {
        Set-Content -Path $Path -Value $result -Encoding UTF8
        Write-Host "   OK feature guard entry -> $Path"
    }
}

Write-Host "==> Apply feature guard patch (MEPDB entry)"
Get-ChildItem -Path $autoCadRoot -Filter *.cs -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match 'LicenseGuard\.EnsureAuthorized') {
        Patch-CsFile -Path $_.FullName
    }
}
