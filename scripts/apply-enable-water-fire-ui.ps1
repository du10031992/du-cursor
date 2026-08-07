# Bat nut HỆ NƯỚC / BÁO CHÁY — patch XAML an toan (khong lam trong file).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Enable HỆ NƯỚC / BÁO CHÁY buttons (ElectricalToolControl)"

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

function Write-XamlUtf8NoBom([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

function Read-TextUtf8([string]$Path) {
    # Doc ca UTF-8 va UTF-16 (Visual Studio doi khi luu UTF-16)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.Encoding]::Unicode.GetString($bytes)
    }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Enable-WaterFireButton([string]$Text, [string[]]$Labels, [string]$ClickHandler) {
    $out = $Text
    foreach ($label in $Labels) {
        # Bo IsEnabled=False tren Button chua nhan he thong
        $patDisable = '(?is)(<Button\b(?=[^>]*' + [regex]::Escape($label) + ')[^>]*?)\s+IsEnabled\s*=\s*("False"|''False'')'
        $out = [regex]::Replace($out, $patDisable, '$1')

        # Them Click neu chua co (self-closing)
        $patSelf = '(?is)(<Button\b(?=[^>]*' + [regex]::Escape($label) + ')(?![^>]*\bClick=)[^>]*?)(\s*/>)'
        $out = [regex]::Replace($out, $patSelf, ('$1 Click="' + $ClickHandler + '"$2'))

        # Them Click neu chua co (opening tag)
        $patOpen = '(?is)(<Button\b(?=[^>]*' + [regex]::Escape($label) + ')(?![^>]*\bClick=)[^>]*?)(>)'
        $out = [regex]::Replace($out, $patOpen, ('$1 Click="' + $ClickHandler + '"$2'))
    }
    return $out
}

$xaml = Find-FirstExisting @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\electricaltoolcontrol.xaml")
)
$cs = Find-FirstExisting @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml.cs"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml.cs")
)

if (-not $xaml) {
    Write-Host "   (bo qua - khong tim thay ElectricalToolControl.xaml)"
}
else {
    $raw = Read-TextUtf8 $xaml
    if (-not (Test-XamlLooksValid $raw)) {
        $repair = Join-Path $Root "scripts\repair-electrical-tool-xaml.ps1"
        if (Test-Path $repair) {
            Write-Host "   XAML hong/trong -> thu repair truoc..."
            & $repair -PluginSourceRoot $PluginSourceRoot
            $raw = Read-TextUtf8 $xaml
        }
    }

    if (-not (Test-XamlLooksValid $raw)) {
        throw @"
ElectricalToolControl.xaml van khong hop le (MC3000 Root element is missing).

Chay:
  .\scripts\repair-electrical-tool-xaml.ps1 -PluginSourceRoot `"$PluginSourceRoot`"
  (hoac Undo Changes file XAML trong Visual Studio / git checkout)

Roi chay lai build-plugin-release.ps1
"@
    }

    $orig = $raw
    $bak = "$xaml.pre-water-fire.bak"
    if (-not (Test-Path $bak)) {
        Copy-Item $xaml $bak -Force
        Write-Host "   Backup: $bak"
    }

    $raw = $raw -replace 'ELECTRICAL \+ HVAC SYSTEM\s*[·•\-]?\s*v0\.13\.0', 'ELECTRICAL + HVAC + NUOC + PCCC · v0.4.0'

    $raw = Enable-WaterFireButton $raw @('NƯỚC', 'Hệ nước', 'HE NUOC') 'HeNuoc_Click'
    $raw = Enable-WaterFireButton $raw @('CHÁY', 'Báo cháy', 'BAO CHAY', 'PCCC') 'BaoChay_Click'

    if (-not (Test-XamlLooksValid $raw)) {
        throw "Patch XAML that bai — file khong con UserControl. Da giu ban backup $bak"
    }

    if ($raw -ne $orig) {
        Write-XamlUtf8NoBom $xaml $raw
        Write-Host "   Updated XAML: $xaml"
    }
    else {
        Write-Host "   (XAML da bat nut hoac khong tim thay nut NƯỚC/CHÁY)"
    }
}

if ($cs) {
    $code = Read-TextUtf8 $cs
    $codeOrig = $code

    $directHandler = @'

        private void HeNuoc_Click(object sender, RoutedEventArgs e)
        {
            try { MepPanelMvp.Commands.WaterFireCommands.ShowWaterMenu(); }
            catch { AutoCadCommandDispatcher.Queue("MEPWATER"); }
        }

        private void BaoChay_Click(object sender, RoutedEventArgs e)
        {
            try { MepPanelMvp.Commands.WaterFireCommands.ShowFireMenu(); }
            catch { AutoCadCommandDispatcher.Queue("MEPFIRE"); }
        }
'@

    if ($code -notmatch 'ShowWaterMenu') {
        if ($code -match '(?s)private void Hvac_Click\s*\([^)]*\)\s*\{.*?\}') {
            $code = [regex]::Replace($code, '(?s)(private void Hvac_Click\s*\([^)]*\)\s*\{.*?\})', "`$1$directHandler", 1)
        }
        elseif ($code -match 'HeNuoc_Click') {
            $code = [regex]::Replace(
                $code,
                '(?s)private void HeNuoc_Click\s*\([^)]*\)\s*\{.*?\}\s*private void BaoChay_Click\s*\([^)]*\)\s*\{.*?\}',
                $directHandler.Trim(),
                1)
        }
        else {
            $code = $code.TrimEnd() + "`r`n" + $directHandler + "`r`n"
        }
        Write-Host "   OK cap nhat code-behind click handlers"
    }

    if ($code -ne $codeOrig) {
        Write-XamlUtf8NoBom $cs $code
        Write-Host "   Updated CS: $cs"
    }
}

$cmdSrc = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Commands\WaterFireCommands.cs"
$cmdDstDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands"
if (Test-Path $cmdSrc) {
    New-Item -ItemType Directory -Force -Path $cmdDstDir | Out-Null
    Copy-Item $cmdSrc (Join-Path $cmdDstDir "WaterFireCommands.cs") -Force
    Write-Host "   OK WaterFireCommands.cs"
}

Write-Host "   Xong enable water/fire UI."
