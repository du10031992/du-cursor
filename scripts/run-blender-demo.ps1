# Chay render Blender demo - tu tim python/python3/py.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Find-MepPython.ps1")

$py = Get-MepPython
if (-not $py) {
    Write-Host "Khong tim thay Python."
    Write-Host "Cai Python 3: https://www.python.org/downloads/"
    Write-Host "QUAN TRONG: tick 'Add python.exe to PATH' khi cai."
    Write-Host "Hoac cai tu Microsoft Store: Python 3.12"
    throw "Can Python 3"
}

$exe = $py.Exe
$prefix = $py.Prefix
Write-Host "Dung: $exe $($prefix)..."

# Cai pillow neu can
& $exe ($prefix.Trim() + " -m pip install pillow").Trim().Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries) 2>$null
try {
    if ($prefix) {
        & $exe -3 -m pip install pillow
    } else {
        & $exe -m pip install pillow
    }
} catch {}

$out = Join-Path $Root "cabinet_blender.png"
$script = Join-Path $Root "renderer\render_cabinet.py"
Write-Host "Render Blender (128 samples) -> $out"
if ($prefix) {
    & $exe -3 $script --demo --quality blender --samples 128 --output $out
} else {
    & $exe $script --demo --quality blender --samples 128 --output $out
}

if (Test-Path $out) {
    Write-Host "OK: $out"
    Start-Process $out
} else {
    throw "Khong tao duoc file output"
}
