# Kiem tra may client co ket noi dung License Server trung tam hay khong.
# Chay tren may AutoCAD sau khi cai plugin.
param(
    [string]$LicenseServerUrl,
    [string]$BundleContents = (Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle\Contents")
)

$ErrorActionPreference = "Stop"

if (-not $LicenseServerUrl) {
    $configPath = Join-Path $BundleContents "MepPanel.config.json"
    if (-not (Test-Path $configPath)) {
        throw "Khong tim thay $configPath. Cai plugin truoc bang Install-MepPanel-Client.ps1."
    }

    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    $LicenseServerUrl = $config.licenseServerUrl
    if (-not $LicenseServerUrl) {
        throw "MepPanel.config.json chua co licenseServerUrl."
    }
}

$uri = [Uri]$LicenseServerUrl
Write-Host "==> License Server cau hinh: $($uri.AbsoluteUri)"

if ($uri.IsLoopback) {
    Write-Host "CANH BAO: dang tro ve localhost. May client phai tro toi Server trung tam." -ForegroundColor Yellow
}

$healthUrl = [Uri]::new($uri, "health").AbsoluteUri
Write-Host "==> Kiem tra $healthUrl"

try {
    $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 20
}
catch {
    throw @"
Khong ket noi duoc License Server: $healthUrl

Kiem tra:
  1. Server trung tam dang chay
  2. Ten mien/IP va cong mo dung (HTTPS 443)
  3. Certificate HTTPS hop le (plugin kiem tra certificate voi server tu xa)
  4. Firewall/proxy cho phep may nay ra Internet

Chi tiet: $($_.Exception.Message)
"@
}

if ($response.StatusCode -ne 200) {
    throw "Server tra ve HTTP $($response.StatusCode)."
}

$health = $response.Content | ConvertFrom-Json
Write-Host ""
Write-Host "Ket noi OK."
Write-Host "  adminUi     : $($health.adminUi)"
Write-Host "  databasePath: $($health.databasePath)"
Write-Host "  version     : $($health.version)"
Write-Host ""
Write-Host "Mo AutoCAD -> MEPDB -> dang nhap bang SDT da tao tren /admin."
