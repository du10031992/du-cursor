# Khoi phuc ElectricalToolControl.xaml bi script patch lam trong / loi MC3000.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"

function Find-FirstExisting([string[]]$Paths) {
    foreach ($p in $Paths) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

$xaml = Find-FirstExisting @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml")
)

if (-not $xaml) {
    throw "Khong tim thay ElectricalToolControl.xaml trong $PluginSourceRoot"
}

Write-Host "==> Repair ElectricalToolControl.xaml"
Write-Host "   File: $xaml"

$bak = "$xaml.bak"
$prePatch = "$xaml.pre-water-fire.bak"

function Test-XamlLooksValid([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    return ($Text -match '(?is)<\s*UserControl\b') -and ($Text -match '(?is)</\s*UserControl\s*>')
}

function Write-XamlUtf8NoBom([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

# 1) Backup gan nhat (script cu)
if ((Test-Path $bak) -and (Test-XamlLooksValid (Get-Content $bak -Raw -Encoding UTF8))) {
    Copy-Item $bak $xaml -Force
    Write-Host "   OK khoi phuc tu $bak"
    exit 0
}

if ((Test-Path $prePatch) -and (Test-XamlLooksValid (Get-Content $prePatch -Raw -Encoding UTF8))) {
    Copy-Item $prePatch $xaml -Force
    Write-Host "   OK khoi phuc tu $prePatch"
    exit 0
}

# 2) Git restore trong MepPanelMvp
$git = Get-Command git -ErrorAction SilentlyContinue
if ($git) {
    Push-Location $PluginSourceRoot
    try {
        $rel = Resolve-Path -Relative $xaml
        git checkout -- $rel 2>$null
        if ($LASTEXITCODE -eq 0) {
            $restored = Get-Content $xaml -Raw -Encoding UTF8
            if (Test-XamlLooksValid $restored) {
                Write-Host "   OK git checkout $rel"
                Pop-Location
                exit 0
            }
        }
    }
    finally {
        Pop-Location
    }
}

# 3) Huong dan thu cong
throw @"
Khong khoi phuc duoc XAML tu backup/git.

File hien tai co the bi trong -> loi MC3000 Root element is missing.

Cach sua:
  1. Mo Visual Studio -> Team Explorer -> Undo Changes tren:
       src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml
     (hoac git checkout file do trong thu muc MepPanelMvp)

  2. Chay lai:
       .\scripts\build-plugin-release.ps1

Neu khong co git: copy lai file XAML tu may backup / ban cu cua MepPanelMvp.
"@
