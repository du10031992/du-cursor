# Kiem tra / huong dan cai Blender cho render tu dien 3D (Windows)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Kiem tra Blender cho render 3D"
Write-Host ""

$blender = $null
if (Get-Command blender -ErrorAction SilentlyContinue) {
    $blender = (Get-Command blender).Source
}
else {
    $bases = @(
        ${env:ProgramFiles},
        ${env:ProgramFiles(x86)}
    ) | Where-Object { $_ }
    foreach ($base in $bases) {
        $bf = Join-Path $base "Blender Foundation"
        if (Test-Path $bf) {
            $found = Get-ChildItem $bf -Directory | Sort-Object Name -Descending | ForEach-Object {
                Join-Path $_.FullName "blender.exe"
            } | Where-Object { Test-Path $_ } | Select-Object -First 1
            if ($found) { $blender = $found; break }
        }
    }
}

if ($blender) {
    Write-Host "Blender: $blender"
    & $blender --version
    Write-Host ""
    . (Join-Path $PSScriptRoot "Find-MepPython.ps1")
    $py = Get-MepPython
    $run = if ($py) { if ($py.Prefix) { "$($py.Exe) $($py.Prefix)".Trim() } else { $py.Exe } } else { "python" }
    Write-Host "Test render (128 samples, khoang 1-3 phut):"
    Write-Host ("  {0} renderer\render_cabinet.py --demo --quality blender --samples 128 --output cabinet_blender.png" -f $run)
    Write-Host ""
    Write-Host "V-Ray (tuy chon, can license + addon):"
    Write-Host ("  {0} renderer\render_cabinet.py --demo --quality blender --engine vray --samples 256 --output cabinet_vray.png" -f $run)
}
else {
    Write-Host "Blender CHUA cai."
    Write-Host ""
    Write-Host "Tai Blender (mien phi): https://www.blender.org/download/"
    Write-Host "Sau khi cai, chay lai script nay."
    Write-Host ""
    Write-Host "V-Ray for Blender (tuy chon, tra phi): https://www.chaos.com/vray/blender"
    Write-Host "Plugin se tu fallback Cycles neu chua co V-Ray."
    Write-Host ""
    Write-Host "Trong thoi gian cho, dung Pillow fallback:"
    Write-Host "  python renderer\render_cabinet.py --demo --quality photoreal --output cabinet.png"
}

Write-Host ""
Write-Host "Cai assets renderer vao plugin:"
Write-Host "  .\scripts\install-renderer-devices.ps1"
