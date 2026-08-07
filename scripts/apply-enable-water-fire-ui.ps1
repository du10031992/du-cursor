# Bat nut HỆ NƯỚC / BÁO CHÁY trong ElectricalToolControl (WPF dang stub IsEnabled=False).
# Chen Click -> MEPWATER / MEPFIRE + copy WaterFireCommands.cs.
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

$xaml = Find-FirstExisting @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\electricaltoolcontrol.xaml"),
    (Join-Path $PluginSourceRoot "UI\ElectricalToolControl.xaml")
)
$cs = Find-FirstExisting @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml.cs"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml.cs"),
    (Join-Path $PluginSourceRoot "UI\ElectricalToolControl.xaml.cs")
)

if (-not $xaml) {
    Write-Host "   (bo qua - khong tim thay ElectricalToolControl.xaml)"
    Write-Host "   Hay mo file XAML panel, xoa IsEnabled=`"False`" o nut HỆ NƯỚC / BÁO CHÁY"
}
else {
    $raw = Get-Content $xaml -Raw -Encoding UTF8
    $orig = $raw

    $raw = $raw -replace 'ELECTRICAL \+ HVAC SYSTEM\s*[·•\-]?\s*v0\.13\.0', 'ELECTRICAL + HVAC + NUOC + PCCC · v0.4.0'
    $raw = $raw -replace 'ELECTRICAL \+ HVAC SYSTEM', 'ELECTRICAL + HVAC + NUOC + PCCC'

    function Patch-Button([string]$Input, [string]$Needle, [string]$Handler) {
        $rx = [regex]'(?is)<Button\b[^>]*?(?:/>|>.*?</Button>)'
        $sb = New-Object System.Text.StringBuilder
        $last = 0
        $n = 0
        foreach ($m in $rx.Matches($Input)) {
            $block = $m.Value
            if ($block -notlike "*$Needle*") {
                continue
            }

            $newBlock = $block
            $newBlock = [regex]::Replace($newBlock, '\sIsEnabled\s*=\s*("False"|''False'')', '', 'IgnoreCase')
            if ($newBlock -notlike "*$Handler*") {
                if ($newBlock -match '/>\s*$') {
                    $newBlock = [regex]::Replace($newBlock, '\s*/>\s*$', " Click=`"$Handler`" />")
                }
                else {
                    $newBlock = [regex]::Replace($newBlock, '^(?is)(<Button\b)([^>]*?)(>)', "`$1`$2 Click=`"$Handler`"`$3")
                }
            }

            if ($newBlock -ne $block) {
                [void]$sb.Append($Input.Substring($last, $m.Index - $last))
                [void]$sb.Append($newBlock)
                $last = $m.Index + $m.Length
                $n++
            }
        }
        if ($n -eq 0) { return $Input }
        [void]$sb.Append($Input.Substring($last))
        Write-Host "   OK patch $n button(s) chua '$Needle' -> $Handler"
        return $sb.ToString()
    }

    $raw = Patch-Button $raw "NƯỚC" "HeNuoc_Click"
    $raw = Patch-Button $raw "CHÁY" "BaoChay_Click"
    # ASCII fallbacks neu file bi encode khac
    $raw = Patch-Button $raw "NUOC" "HeNuoc_Click"
    $raw = Patch-Button $raw "CHAY" "BaoChay_Click"

    if ($raw -ne $orig) {
        Set-Content -Path $xaml -Value $raw -Encoding UTF8
        Write-Host "   Updated XAML: $xaml"
    }
    else {
        Write-Host "   (XAML khong doi - kiem tra chu NƯỚC/CHÁY trong $xaml)"
    }
}

if ($cs) {
    $code = Get-Content $cs -Raw -Encoding UTF8
    $codeOrig = $code

    if ($code -notmatch 'HeNuoc_Click') {
        $handler = @'

        private void HeNuoc_Click(object sender, RoutedEventArgs e)
        {
            AutoCadCommandDispatcher.Queue("MEPWATER");
        }

        private void BaoChay_Click(object sender, RoutedEventArgs e)
        {
            AutoCadCommandDispatcher.Queue("MEPFIRE");
        }
'@
        if ($code -match '(?s)private void Hvac_Click\s*\([^)]*\)\s*\{.*?\}') {
            $code = [regex]::Replace($code, '(?s)(private void Hvac_Click\s*\([^)]*\)\s*\{.*?\})', "`$1$handler", 1)
        }
        elseif ($code -match 'void InitializeComponent\s*\(') {
            $code = [regex]::Replace($code, '([^\n]*void InitializeComponent\s*\()', ($handler + "`r`n`r`n        `$1"), 1)
        }
        else {
            $code = $code.TrimEnd() + "`r`n" + $handler + "`r`n"
        }
        Write-Host "   OK chen HeNuoc_Click / BaoChay_Click"
    }
    else {
        Write-Host "   (code-behind da co HeNuoc_Click)"
    }

    if ($code -ne $codeOrig) {
        Set-Content -Path $cs -Value $code -Encoding UTF8
        Write-Host "   Updated CS: $cs"
    }
}
else {
    Write-Host "   (bo qua code-behind - khong tim thay .xaml.cs)"
}

$cmdSrc = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Commands\WaterFireCommands.cs"
$cmdDirs = @(
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD")
)
if (Test-Path $cmdSrc) {
    $dstDir = $cmdDirs[0]
    if (-not (Test-Path (Split-Path $dstDir))) {
        $dstDir = $cmdDirs[1]
    }
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
    $dst = Join-Path $dstDir "WaterFireCommands.cs"
    Copy-Item $cmdSrc $dst -Force
    Write-Host "   OK WaterFireCommands.cs -> $dst"
}

Write-Host "   Xong enable water/fire UI."
