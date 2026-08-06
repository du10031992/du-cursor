# Render demo Blender - KHONG bat buoc cai Python rieng.
# Uu tien: python he thong -> python trong Blender -> goi blender truc tiep.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Find-MepPython.ps1")

$out = Join-Path $Root "cabinet_blender.png"
$layoutDemo = Join-Path $Root "renderer\blender\demo_layout.json"
$buildScene = Join-Path $Root "renderer\blender\build_scene.py"

# Tao layout demo JSON (khong can Python)
$demoJson = @'
{
  "name": "DB-01",
  "size": "H600xW500xD225",
  "row_modules": 18,
  "rows": [
    {
      "label": "Nguon vao",
      "devices": [
        {"name": "MCCB tong", "type": "MCCB", "modules": 4, "texture": null, "in_a": 100},
        {"name": "SPD", "type": "SPD", "modules": 2, "texture": null, "in_a": 0}
      ]
    },
    {
      "label": "Phan phoi",
      "devices": [
        {"name": "MCB 1", "type": "MCB 1P", "modules": 1, "texture": null, "in_a": 10},
        {"name": "MCB 2", "type": "MCB 1P", "modules": 1, "texture": null, "in_a": 10},
        {"name": "MCB 3", "type": "MCB 1P", "modules": 1, "texture": null, "in_a": 16},
        {"name": "MCB 4", "type": "MCB 1P", "modules": 1, "texture": null, "in_a": 16},
        {"name": "MCB 3P", "type": "MCB 3P", "modules": 3, "texture": null, "in_a": 16}
      ]
    },
    {
      "label": "Dieu khien",
      "devices": [
        {"name": "Contactor", "type": "CONTACTOR", "modules": 4, "texture": null, "in_a": 25},
        {"name": "Relay", "type": "RELAY", "modules": 3, "texture": null, "in_a": 25}
      ]
    }
  ],
  "render": {"engine": "cycles", "samples": 128, "width": 1920, "height": 1280}
}
'@

# Gan texture neu co PNG
$devicesDir = Join-Path $Root "renderer\devices"
$map = @{
    "MCCB" = "device_mccb_schneider.png"
    "SPD" = "device_spd.png"
    "MCB 1P" = "device_mcb1p_schneider.png"
    "MCB 3P" = "device_mcb3p_schneider.png"
    "CONTACTOR" = "device_contactor_ls.png"
    "RELAY" = "device_relay_ls.png"
}

$obj = $demoJson | ConvertFrom-Json
foreach ($row in $obj.rows) {
    foreach ($dev in $row.devices) {
        $fname = $map[$dev.type]
        if ($fname) {
            $p = Join-Path $devicesDir $fname
            if (Test-Path $p) { $dev.texture = $p }
        }
    }
}
# Ghi UTF-8 KHONG BOM (tranh JSONDecodeError trong Blender)
$jsonText = $obj | ConvertTo-Json -Depth 8
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($layoutDemo, $jsonText, $utf8NoBom)
Write-Host "Layout: $layoutDemo"

$blender = Get-MepBlender
if (-not $blender) {
    throw "Chua cai Blender. Tai: https://www.blender.org/download/"
}
Write-Host "Blender: $blender"
Write-Host "Render Cycles 128 samples (1-3 phut)..."

& $blender --background --factory-startup --python $buildScene -- --input $layoutDemo --output $out --engine cycles --samples 128
if ($LASTEXITCODE -ne 0) {
    throw "Blender render that bai (code $LASTEXITCODE)"
}

if (-not (Test-Path $out)) {
    throw "Khong tao duoc $out"
}

Write-Host "OK: $out"
Start-Process $out
