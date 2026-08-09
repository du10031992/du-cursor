# Chay License Server tren Windows (HTTP 5268, SQLite local).
# Dong AutoCAD khong bat buoc. Giữ cửa sổ nay mo khi dung plugin.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$ServerDir = Join-Path $Root "MepPanel.LicenseServer"

Set-Location $ServerDir
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5268"

Write-Host "==> License Server: http://localhost:5268"
Write-Host "==> Admin: http://localhost:5268/admin"
Write-Host "==> OTP test: 123456 (TestMode=true)"
Write-Host ""

dotnet run --no-launch-profile --project (Join-Path $ServerDir "MepPanel.LicenseServer.csproj")
