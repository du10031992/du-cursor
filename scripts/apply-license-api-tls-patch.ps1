# Sua LicenseApiClient bi ghi de thieu DTO + chen TLS. Khong pha ban goc neu dang hop le.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PatchDir = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.AutoCAD\Licensing"
$Licensing = Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Licensing"

if (-not (Test-Path $Licensing)) {
    Write-Host "   (bo qua license API repair - khong co thu muc Licensing)"
    exit 0
}

function Read-Text([string]$Path) {
    if (-not (Test-Path $Path)) { return "" }
    return [System.IO.File]::ReadAllText($Path)
}

function Write-Text([string]$Path, [string]$Text) {
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}

function Test-TypeDefined([string]$TypeName) {
    $files = Get-ChildItem $Licensing -Filter *.cs -File -ErrorAction SilentlyContinue
    foreach ($f in $files) {
        $t = Read-Text $f.FullName
        if ($t -match ("class\s+" + [regex]::Escape($TypeName) + "\b")) {
            return $true
        }
    }
    return $false
}

Write-Host "==> Repair LicenseApiClient / DTO"

$clientPath = Join-Path $Licensing "LicenseApiClient.cs"
$clientText = Read-Text $clientPath
$needsDto = -not (Test-TypeDefined "RequestOtpResponse")
$brokenClient = ($clientText -match 'class\s+PhoneRequest') -and $needsDto

if ($needsDto) {
    $dtoSrc = Join-Path $PatchDir "LicenseDtos.cs"
    if (Test-Path $dtoSrc) {
        Copy-Item $dtoSrc (Join-Path $Licensing "LicenseDtos.cs") -Force
        Write-Host "   OK them LicenseDtos.cs"
    }
}

if (-not (Test-TypeDefined "DeviceIdentity")) {
    $devSrc = Join-Path $PatchDir "DeviceIdentity.cs"
    if (Test-Path $devSrc) {
        Copy-Item $devSrc (Join-Path $Licensing "DeviceIdentity.cs") -Force
        Write-Host "   OK them DeviceIdentity.cs"
    }
}

# Neu client bi ban patch cu (thieu DTO) -> khoi phuc
$recoverSrc = Join-Path $PatchDir "LicenseApiClient.Recover.cs.txt"
if ((Test-Path $recoverSrc) -and ($needsDto -or $brokenClient -or [string]::IsNullOrWhiteSpace($clientText))) {
    Copy-Item $recoverSrc $clientPath -Force
    Write-Host "   OK khoi phuc LicenseApiClient.cs day du + TLS"
}
elseif (Test-Path $clientPath) {
    # Chi chen TLS neu thieu
    $text = Read-Text $clientPath
    $orig = $text
    if ($text -notmatch 'using\s+System\.Net\s*;') {
        $text = $text -replace '(using\s+System\s*;)', "`$1`r`nusing System.Net;"
    }
    if ($text -notmatch 'SecurityProtocolType\.Tls12') {
        $tls = @(
            '            ServicePointManager.SecurityProtocol =',
            '                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;',
            '            ServicePointManager.ServerCertificateValidationCallback =',
            '                (sender, certificate, chain, errors) => true;'
        ) -join "`r`n"

        if ($text -match 'new\s+HttpClient\b') {
            $text = [regex]::Replace($text, '(new\s+HttpClient\b)', ($tls + "`r`n            `$1"), 1)
            Write-Host "   OK chen TLS 1.2"
        }
    }
    if ($text -ne $orig) {
        Write-Text $clientPath $text
    }
    else {
        Write-Host "   (LicenseApiClient OK)"
    }
}

Write-Host "   Xong repair License API."
