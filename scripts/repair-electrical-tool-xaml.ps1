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

# Native git + ErrorActionPreference=Stop co the throw. Dung cmd de khong crash.
function Invoke-GitSafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string[]]$GitArgs
    )

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return 127
    }
    if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
        return 128
    }

    $argLine = ($GitArgs | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }) -join ' '

    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        cmd.exe /c "git -C `"$RepoRoot`" $argLine >nul 2>nul" | Out-Null
        return $LASTEXITCODE
    }
    catch {
        return 1
    }
    finally {
        $ErrorActionPreference = $prev
    }
}

function Try-GitCheckout([string]$RepoRoot, [string]$XamlPath) {
    if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
        Write-Host "   (bo qua git - $RepoRoot khong phai git repo)"
        return $false
    }

    $rel = $XamlPath.Substring($RepoRoot.Length).TrimStart('\', '/')
    if ([string]::IsNullOrWhiteSpace($rel)) {
        return $false
    }

    $code = Invoke-GitSafe -RepoRoot $RepoRoot -GitArgs @('checkout', '--', $rel)
    if ($code -ne 0) {
        Write-Host "   (git checkout that bai trong $RepoRoot)"
        return $false
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

# Ban khoi phuc tu DLL v0.13 (trong repo du-cursor)
$Root = Split-Path -Parent $PSScriptRoot
$template = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml"
if (Try-RestoreFromFile $template $xaml) {
    Write-Host "   (da dung template khoi phuc tu repo - panel UI tu DLL v0.13)"
    exit 0
}

# Neu XAML hien tai van hop le -> khong can repair
$current = Read-TextUtf8 $xaml
if (Test-XamlLooksValid $current) {
    Write-Host "   XAML hien tai van hop le - khong can repair"
    exit 0
}

Write-Host ""
Write-Host "KHONG khoi phuc duoc XAML tu backup/git/template." -ForegroundColor Yellow
Write-Host ""
Write-Host "File co the bi trong -> loi MC3000 Root element is missing."
Write-Host ""
Write-Host "Sau do:"
Write-Host "  cd C:\MepPanel\du-cursor"
Write-Host "  git pull origin cursor/water-pccc-visual-cc24"
Write-Host "  .\scripts\build-plugin-release.ps1"
Write-Host ""

exit 1
