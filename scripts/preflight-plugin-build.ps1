# Kiem tra loi build pho bien truoc khi dotnet build (Windows).
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$issues = @()

function Read-TextUtf8([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Test-XamlLooksValid([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    return ($Text -match '(?is)<\s*UserControl\b') -and ($Text -match '(?is)</\s*UserControl\s*>')
}

$xaml = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml"
if (-not (Test-Path $xaml)) {
    $xaml = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml"
}
if (Test-Path $xaml) {
    $xamlText = Read-TextUtf8 $xaml
    if (-not (Test-XamlLooksValid $xamlText)) {
        $issues += "ElectricalToolControl.xaml trong/trong -> chay repair-electrical-tool-xaml.ps1"
    }
}

$cs = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\ElectricalToolControl.xaml.cs"
if (-not (Test-Path $cs)) {
    $cs = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\ElectricalToolControl.xaml.cs"
}
if (Test-Path $cs) {
    $csText = Read-TextUtf8 $cs
    if ($csText -match '(?s)\}\s*\}\s*private\s+void\s+HeNuoc_Click') {
        $issues += "HeNuoc_Click nam ngoai class -> chay lai build (script tu sua code-behind)"
    }
}

$wf = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Commands\WaterFireCommands.cs"
if (Test-Path $wf) {
    $wfText = Read-TextUtf8 $wf
    if ($wfText -match 'using Autodesk\.AutoCAD\.Runtime' -and $wfText -match 'catch\s*\(\s*Exception\b' -and $wfText -notmatch 'using Exception = System\.Exception') {
        $issues += "WaterFireCommands.cs CS0104 Exception ambiguous -> pull repo moi va build lai"
    }
}

$disp = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\UI\AutoCadCommandDispatcher.cs"
if (-not (Test-Path $disp)) {
    $disp = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\ui\AutoCadCommandDispatcher.cs"
}
if (Test-Path $disp) {
    $dispText = Read-TextUtf8 $disp
    if ($dispText -match '\bApplication\.DocumentManager\b') {
        $issues += "AutoCadCommandDispatcher.cs CS0104 Application ambiguous -> chay lai build (apply-ui-dispatcher-patch)"
    }
    if ($dispText -notmatch 'AcApp\.DocumentManager') {
        $issues += "AutoCadCommandDispatcher thieu AcApp alias"
    }
}

$client = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Licensing\LicenseApiClient.cs"
if (Test-Path $client) {
    $clientText = Read-TextUtf8 $client
    if ($clientText -match 'Task<RequestOtpResponse>' -and $clientText -notmatch 'class\s+RequestOtpResponse') {
        $licDir = Split-Path $client -Parent
        $hasDto = $false
        Get-ChildItem $licDir -Filter *.cs | ForEach-Object {
            if ((Read-TextUtf8 $_.FullName) -match 'class\s+RequestOtpResponse') { $hasDto = $true }
        }
        if (-not $hasDto) {
            $issues += "LicenseApiClient thieu RequestOtpResponse (CS0246) -> chay lai build"
        }
    }
}

if ($issues.Count -gt 0) {
    Write-Host "==> Preflight FAILED" -ForegroundColor Red
    foreach ($i in $issues) { Write-Host "   - $i" -ForegroundColor Yellow }
    throw "Preflight that bai. Sua cac loi tren roi build lai."
}

Write-Host "==> Preflight OK"
