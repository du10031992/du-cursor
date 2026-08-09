# Cai License Server thanh Windows Service: tu chay khi bat may,
# khong can mo Visual Studio va khong can giu cua so PowerShell.
# Chay PowerShell "Run as Administrator".
param(
    [string]$InstallDir = "C:\MepPanel\LicenseServer",
    [int]$Port = 5268,
    # Mac dinh mo LAN de may AutoCAD khac ket noi duoc.
    [switch]$LocalhostOnly,
    [string]$ServiceName = "MepPanelLicense",
    [string]$DatabasePath = "C:\MepPanel\du-cursor\MepPanel.LicenseServer\mep-panel-license.db",
    # Key de dang nhap trang /admin (header X-Admin-ApiKey).
    [string]$AdminApiKey = "MEP-PANEL-ADMIN-TEST-2026",
    # Bo trong = tu sinh key ky JWT. Doi key se buoc dang nhap lai.
    [string]$JwtKey,
    # Dung SMS thuc te thay vi OTP thu nghiem 123456.
    [switch]$UseRealSms,
    [string]$SmsWebhookUrl,
    [string]$SmsApiKey
)

$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Hay mo PowerShell bang 'Run as Administrator' roi chay lai."
}

$exe = Join-Path $InstallDir "MepPanel.LicenseServer.exe"
if (-not (Test-Path $exe)) {
    throw @"
Khong tim thay $exe

Chay truoc:
  .\scripts\publish-license-server.ps1
"@
}

$bindHost = if ($LocalhostOnly) { "localhost" } else { "0.0.0.0" }
$urls = "http://${bindHost}:$Port"

if ($UseRealSms -and -not $SmsWebhookUrl) {
    throw "Dung -UseRealSms thi phai truyen -SmsWebhookUrl (va -SmsApiKey neu nha cung cap yeu cau)."
}

if (-not $AdminApiKey.Trim()) {
    throw "AdminApiKey khong duoc de trong."
}

if (-not $JwtKey) {
    $bytes = New-Object 'System.Byte[]' 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $JwtKey = [Convert]::ToBase64String($bytes)
}

if ($JwtKey.Length -lt 32) {
    throw "JwtKey phai dai tu 32 ky tu tro len."
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "==> Xoa service cu $ServiceName"
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
        $existing.WaitForStatus("Stopped", (New-TimeSpan -Seconds 30))
    }
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "==> Tao service $ServiceName"
$binPath = '"{0}" --urls {1}' -f $exe, $urls
sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= "MepPanel License Server" | Out-Null
sc.exe description $ServiceName "Kiem soat license/quyen plugin MepPanel cho cac may AutoCAD." | Out-Null

# Cau hinh doc tu registry cua service (khong phu thuoc user dang nhap).
# Dat tuong minh de KHONG bi appsettings.Production.json ghi de bang gia tri placeholder
# (truoc day gay 401 o /admin va khong dang nhap duoc OTP).
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$environment = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "MEP_PANEL_LICENSE_DB_PATH=$DatabasePath",
    "Admin__ApiKey=$AdminApiKey",
    "Jwt__Key=$JwtKey",
    "Security__RequireHttps=false"
)

if ($UseRealSms) {
    $environment += "LicenseSettings__TestMode=false"
    $environment += "Sms__WebhookUrl=$SmsWebhookUrl"
    if ($SmsApiKey) { $environment += "Sms__ApiKey=$SmsApiKey" }
}
else {
    # LAN noi bo, chua co nha cung cap SMS: giu OTP thu nghiem de dang nhap duoc.
    $environment += "LicenseSettings__TestMode=true"
}

Set-ItemProperty -Path $regPath -Name "Environment" -Value $environment -Type MultiString

$dbDir = Split-Path $DatabasePath -Parent
New-Item -ItemType Directory -Force -Path $dbDir | Out-Null

if (-not $LocalhostOnly) {
    Write-Host "==> Mo firewall TCP $Port cho LAN"
    Remove-NetFirewallRule -DisplayName "MepPanel License Server" -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName "MepPanel License Server" `
        -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private, Domain | Out-Null
}

Write-Host "==> Khoi dong service"
Start-Service -Name $ServiceName
(Get-Service -Name $ServiceName).WaitForStatus("Running", (New-TimeSpan -Seconds 30))

Write-Host ""
Write-Host "Service dang chay. Tu bat khi khoi dong Windows."
Write-Host "  Admin tren may nay : http://localhost:$Port/admin"
Write-Host "  Admin API Key      : $AdminApiKey"

if ($UseRealSms) {
    Write-Host "  OTP                : gui qua SMS ($SmsWebhookUrl)"
}
else {
    Write-Host "  OTP                : 123456 (TestMode - chi dung LAN noi bo)"
}

if (-not $LocalhostOnly) {
    $lanIp = (Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike "127.*" -and $_.PrefixOrigin -ne "WellKnown" } |
        Select-Object -First 1 -ExpandProperty IPAddress)
    if ($lanIp) {
        Write-Host "  URL cho may khac   : http://${lanIp}:$Port/"
        Write-Host ""
        Write-Host "Tren tung may AutoCAD:"
        Write-Host "  .\Install-MepPanel-Client.ps1 -LicenseServerUrl 'http://${lanIp}:$Port/'"
    }
}

if (-not $UseRealSms) {
    Write-Host ""
    Write-Host "CANH BAO: OTP dang la ma co dinh 123456. Truoc khi mo ra Internet, cai lai voi:" -ForegroundColor Yellow
    Write-Host "  .\scripts\install-license-server-service.ps1 -AdminApiKey '<key-rieng>' -UseRealSms -SmsWebhookUrl '<url>'" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Lenh quan ly:"
Write-Host "  Stop-Service $ServiceName"
Write-Host "  Start-Service $ServiceName"
Write-Host "  Get-Service $ServiceName"
