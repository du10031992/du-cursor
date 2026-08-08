# Thu thap source that lien quan den ve so do (eLockViolation).
# KHONG sua code. Chi doc + dump ra elock-source-dump.log de phan tich chinh xac.
param(
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

$LocalConfig = Join-Path $Root "plugin.local.json"
if (-not $PluginSourceRoot) {
    if (Test-Path $LocalConfig) {
        $cfg = Get-Content $LocalConfig -Raw | ConvertFrom-Json
        if ($cfg.pluginSourceRoot) { $PluginSourceRoot = $cfg.pluginSourceRoot }
    }
}
if (-not $PluginSourceRoot -or -not (Test-Path $PluginSourceRoot)) {
    throw "Chua cau hinh pluginSourceRoot. Truyen: .\scripts\collect-elock-source.ps1 -PluginSourceRoot C:\MepPanel\MepPanelMvp"
}

$autoRoot = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD"
if (-not (Test-Path $autoRoot)) { throw "Khong thay $autoRoot" }

$out = New-Object System.Collections.Generic.List[string]
function Add-Line([string]$m) { $out.Add($m) }

Add-Line "==== ELOCK SOURCE DUMP ===="
Add-Line ("PluginSourceRoot: " + $PluginSourceRoot)
Add-Line ("Time: " + (Get-Date -Format "s"))
Add-Line ""

# Tieu chi: file lien quan truc tiep den ve so do
$nameMatch = 'HvacConfiguration|PanelConfiguration|HvacCommands|PanelCommands|ConfigurationHost|HvacConfigurationHost|PaletteHost|WaterFireCommands|HvacSupplyAir|SchematicDrawing|MepDocumentContext|AutoCadCommandDispatcher'
$keywordMatch = 'BuildSchematic|CommitEdits|StartTransaction|DrawingService|ExecuteInCommandContextAsync|LockDocument|eLock|AppendEntity'

$files = Get-ChildItem $autoRoot -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|package_staging)\\' }

$selected = @()
foreach ($f in $files) {
    $raw = [System.IO.File]::ReadAllText($f.FullName)
    if ($f.Name -match $nameMatch -or $raw -match $keywordMatch) {
        $selected += [pscustomobject]@{ File = $f; Text = $raw }
    }
}

Add-Line ("Selected files: " + $selected.Count)
foreach ($s in $selected) {
    $rel = $s.File.FullName.Substring($PluginSourceRoot.Length).TrimStart('\', '/')
    Add-Line ("  - " + $rel + "  (" + $s.Text.Length + " chars)")
}
Add-Line ""

foreach ($s in $selected) {
    $rel = $s.File.FullName.Substring($PluginSourceRoot.Length).TrimStart('\', '/')
    Add-Line ("======================================================================")
    Add-Line ("FILE: " + $rel)
    Add-Line ("======================================================================")
    $lines = $s.Text -split "`r?`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        Add-Line (("{0,5}| " -f ($i + 1)) + $lines[$i])
    }
    Add-Line ""
}

$logPath = Join-Path $Root "elock-source-dump.log"
$enc = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($logPath, ($out -join "`r`n"), $enc)
Write-Host ("==> Da ghi: " + $logPath)
Write-Host ("    So file: " + $selected.Count)
Write-Host "    Hay upload file elock-source-dump.log len chat."
