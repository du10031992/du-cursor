# Khoi phuc ElectricalToolControl.xaml bi patch lam trong / loi MC3000.
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

function Test-XamlLooksValid([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    return ($Text -match '(?is)<\s*UserControl\b') -and ($Text -match '(?is)</\s*UserControl\s*>')
}

function Read-TextUtf8([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.Encoding]::Unicode.GetString($bytes)
    }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Try-RestoreFromFile([string]$Source, [string]$Target) {
    $text = Read-TextUtf8 $Source
    if (-not (Test-XamlLooksValid $text)) {
        return $false
    }
    Copy-Item $Source $Target -Force
    Write-Host "   OK khoi phuc tu $Source"
    return $true
}

function Test-GitRepo([string]$Dir) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return $false
    }
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    try {
        git -C $Dir rev-parse --is-inside-work-tree 2>$null | Out-Null
        return ($LASTEXITCODE -eq 0)
    }
    finally {
        $ErrorActionPreference = $prev
    }
}

function Try-GitCheckout([string]$RepoRoot, [string]$XamlPath) {
    if (-not (Test-GitRepo $RepoRoot)) {
        Write-Host "   (bo qua git - $RepoRoot khong phai git repo)"
        return $false
    }

    $rel = $XamlPath.Substring($RepoRoot.Length).TrimStart('\', '/')
    if ([string]::IsNullOrWhiteSpace($rel)) {
        return $false
    }

    $prev = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    try {
        git -C $RepoRoot checkout -- $rel 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   (git checkout that bai trong $RepoRoot)"
            return $false
        }
    }
    finally {
        $ErrorActionPreference = $prev
    }

    $restored = Read-TextUtf8 $XamlPath
    if (Test-XamlLooksValid $restored) {
        Write-Host "   OK git checkout $rel"
        return $true
    }

    Write-Host "   (git checkout xong nhung XAML van khong hop le)"
    return $false
}

$PluginSourceRoot = (Resolve-Path $PluginSourceRoot).Path

$xaml = Find-FirstExisting @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml")
)

if (-not $xaml) {
    throw "Khong tim thay ElectricalToolControl.xaml trong $PluginSourceRoot"
}

Write-Host "==> Repair ElectricalToolControl.xaml"
Write-Host "   File: $xaml"

$bakCandidates = @(
    "$xaml.bak",
    "$xaml.pre-water-fire.bak",
    (Join-Path (Split-Path $xaml) "ElectricalToolControl.xaml.orig")
)

foreach ($bak in $bakCandidates) {
    if (Try-RestoreFromFile $bak $xaml) {
        exit 0
    }
}

if (Try-GitCheckout $PluginSourceRoot $xaml) {
    exit 0
}

# Thu git o thu muc cha (neu MepPanelMvp nam trong repo lon hon)
$parent = Split-Path $PluginSourceRoot -Parent
if ($parent -and (Try-GitCheckout $parent $xaml)) {
    exit 0
}

Write-Host ""
Write-Host "KHONG khoi phuc duoc XAML tu backup/git." -ForegroundColor Yellow
Write-Host ""
Write-Host "File co the bi trong -> loi MC3000 Root element is missing."
Write-Host ""
Write-Host "Cach sua (chon 1):"
Write-Host "  A) Visual Studio: mo ElectricalToolControl.xaml -> chuot phai -> Undo Changes"
Write-Host "     (hoac Local History / Previous Version neu co)"
Write-Host ""
Write-Host "  B) Neu MepPanelMvp co git:"
Write-Host "     cd $PluginSourceRoot"
Write-Host "     git checkout -- src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml"
Write-Host ""
Write-Host "  C) Copy file XAML tu may backup / ban cu cua MepPanelMvp"
Write-Host ""
Write-Host "Sau do:"
Write-Host "  cd C:\Users\DU_COMPUTER\Desktop\AI"
Write-Host "  git pull origin cursor/water-pccc-visual-cc24"
Write-Host "  .\scripts\build-plugin-release.ps1"
Write-Host ""

exit 1
