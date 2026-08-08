# Bat nut HE NUOC / BAO CHAY - patch XAML an toan (ASCII only, tranh loi parse PS).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Enable HE NUOC / BAO CHAY buttons (ElectricalToolControl)"

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

function Write-TextUtf8NoBom([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

function Read-TextUtf8([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.Encoding]::Unicode.GetString($bytes)
    }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Enable-DisabledWaterFireButtons([string]$Text) {
    $script:wfBtnIdx = 0
    $handlers = @('HeNuoc_Click', 'BaoChay_Click')
    return [regex]::Replace($Text, '(?is)(<Button\b)([^>]*?)\s+IsEnabled\s*=\s*"False"([^>]*>)', {
        param($m)
        $before = $m.Groups[2].Value
        $after = $m.Groups[3].Value
        if ($script:wfBtnIdx -ge $handlers.Count) {
            return $m.Groups[1].Value + $before + $after
        }
        $handler = $handlers[$script:wfBtnIdx]
        $script:wfBtnIdx++
        if ($before -match '(?i)Click\s*=' -or $after -match '(?i)Click\s*=') {
            $combined = $before + $after
            return $m.Groups[1].Value + ($combined -replace '\s+IsEnabled\s*=\s*"False"\s*', ' ')
        }
        return ($m.Groups[1].Value + $before.TrimEnd() + " Click=`"$handler`"" + $after) -replace '\s+IsEnabled\s*=\s*"False"\s*', ' '
    })
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
            $prevEa = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            & $repair -PluginSourceRoot $PluginSourceRoot
            $repairExit = $LASTEXITCODE
            $ErrorActionPreference = $prevEa
            $raw = Read-TextUtf8 $xaml
            if ($repairExit -ne 0 -and -not (Test-XamlLooksValid $raw)) {
                throw @"
ElectricalToolControl.xaml van khong hop le (MC3000 Root element is missing).

Xem huong dan repair o tren, hoac Undo Changes file XAML trong Visual Studio.
Roi chay lai: .\scripts\build-plugin-release.ps1
"@
            }
        }
    }

    if (-not (Test-XamlLooksValid $raw)) {
        throw @"
ElectricalToolControl.xaml van khong hop le (MC3000 Root element is missing).

Chay:
  .\scripts\repair-electrical-tool-xaml.ps1 -PluginSourceRoot `"$PluginSourceRoot`"

Roi chay lai build-plugin-release.ps1
"@
    }

    $orig = $raw
    $bak = "$xaml.pre-water-fire.bak"
    if (-not (Test-Path $bak)) {
        Copy-Item $xaml $bak -Force
        Write-Host "   Backup: $bak"
    }

    $raw = $raw -replace 'ELECTRICAL \+ HVAC SYSTEM[^<]*v0\.13\.0', 'ELECTRICAL + HVAC + NUOC + PCCC v0.4.0'
    $raw = Enable-DisabledWaterFireButtons $raw

    if (-not (Test-XamlLooksValid $raw)) {
        throw "Patch XAML that bai - file khong con UserControl. Da giu ban backup $bak"
    }

    if ($raw -ne $orig) {
        Write-TextUtf8NoBom $xaml $raw
        Write-Host "   Updated XAML: $xaml"
    }
    else {
        Write-Host "   (XAML khong doi - co the da bat nut truoc do)"
    }
}

function Remove-MethodBlock([string]$Text, [string]$MethodName) {
    $pattern = "(?ms)^\s*private\s+void\s+$MethodName\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}\s*"
    return [regex]::Replace($Text, $pattern, '')
}

function Remove-WaterFireHandlerBlocks([string]$Text) {
    # Xoa moi block HeNuoc/BaoChay (ke ca bi chen ngoai class -> CS8803)
    $Text = Remove-MethodBlock $Text 'HeNuoc_Click'
    $Text = Remove-MethodBlock $Text 'BaoChay_Click'
    return $Text
}

function Insert-HandlersInsideClass([string]$Text, [string]$Handlers) {
    if ($Text -match '(?s)private\s+void\s+Hvac_Click\s*\([^)]*\)\s*\{.*?\}') {
        return [regex]::Replace($Text, '(?s)(private\s+void\s+Hvac_Click\s*\([^)]*\)\s*\{.*?\})', "`$1$Handlers", 1)
    }

    $anchor = [regex]::Match($Text, '(?m)^(\s*)(\[DebuggerNonUserCode\]|public\s+void\s+InitializeComponent|#region\s+Component)')
    if ($anchor.Success) {
        return $Text.Insert($anchor.Index, $Handlers + "`r`n")
    }

    # Chen truoc dau } dong class (ngay truoc } dong namespace)
    $last = $Text.LastIndexOf('}')
    if ($last -lt 0) { return ($Text.TrimEnd() + $Handlers) }
    $second = $Text.LastIndexOf('}', $last - 1)
    if ($second -lt 0) { return $Text.Insert($last, $Handlers + "`r`n") }
    return $Text.Insert($second, $Handlers + "`r`n")
}

if ($cs) {
    $code = Read-TextUtf8 $cs
    $codeOrig = $code

    $directHandler = @'

        private void HeNuoc_Click(object sender, RoutedEventArgs e)
        {
            AutoCadCommandDispatcher.Queue("MEPWATER");
        }

        private void BaoChay_Click(object sender, RoutedEventArgs e)
        {
            AutoCadCommandDispatcher.Queue("MEPFIRE");
        }
'@

    # Luon go bo handler nam sai cho (ngoai class) roi chen lai dung cho
    $cleaned = Remove-WaterFireHandlerBlocks $code
    $needInject = ($cleaned -notmatch 'ShowWaterMenu')
    if ($needInject -or ($cleaned -ne $code)) {
        if ($needInject) {
            $code = Insert-HandlersInsideClass $cleaned $directHandler
            Write-Host "   OK chen HeNuoc_Click / BaoChay_Click trong class"
        }
        else {
            $code = $cleaned
            Write-Host "   OK go handler nam ngoai class (giu ban dung trong class)"
        }
    }

    if ($code -ne $codeOrig) {
        Write-TextUtf8NoBom $cs $code
        Write-Host "   Updated CS: $cs"
    }
}

$cmdSrc = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Commands\WaterFireCommands.cs"
$cmdDstDir = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands"
if (Test-Path $cmdSrc) {
    New-Item -ItemType Directory -Force -Path $cmdDstDir | Out-Null
    $cmdDst = Join-Path $cmdDstDir "WaterFireCommands.cs"
    Copy-Item $cmdSrc $cmdDst -Force
    # CS0104: Exception trung Autodesk.AutoCAD.Runtime vs System
    $cmdText = Read-TextUtf8 $cmdDst
    if ($cmdText -match 'using Autodesk\.AutoCAD\.Runtime' -and $cmdText -match 'catch\s*\(\s*Exception\b') {
        if ($cmdText -notmatch 'using Exception = System\.Exception') {
            $cmdText = $cmdText -replace '(using Autodesk\.AutoCAD\.Runtime;\r?\n)', "`$1using Exception = System.Exception;`r`n"
            Write-TextUtf8NoBom $cmdDst $cmdText
            Write-Host "   OK fix CS0104 Exception alias"
        }
    }
    Write-Host "   OK WaterFireCommands.cs"
}

Write-Host "   Xong enable water/fire UI."
