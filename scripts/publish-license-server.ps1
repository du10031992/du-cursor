# Publish License Server thanh exe chay doc lap (khong can Visual Studio).
param(
    [string]$InstallDir = "C:\MepPanel\LicenseServer",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "MepPanel.LicenseServer\MepPanel.LicenseServer.csproj"

if (-not (Test-Path $Project)) {
    throw "Khong tim thay $Project"
}

# Service/exe dang chay se khoa file khi publish.
$service = Get-Service -Name "MepPanelLicense" -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq "Running") {
    Write-Host "==> Dung service MepPanelLicense de ghi de file"
    Stop-Service -Name "MepPanelLicense"
    $service.WaitForStatus("Stopped", (New-TimeSpan -Seconds 30))
}
Get-Process -Name "MepPanel.LicenseServer" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "==> Publish License Server -> $InstallDir"
dotnet publish $Project -c $Configuration -o $InstallDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish that bai."
}

if ($service) {
    Write-Host "==> Khoi dong lai service MepPanelLicense"
    Start-Service -Name "MepPanelLicense"
}

Write-Host ""
Write-Host "Xong. File chay: $(Join-Path $InstallDir 'MepPanel.LicenseServer.exe')"
Write-Host "Chay tam thoi (khong can Visual Studio):"
Write-Host "  & '$(Join-Path $InstallDir 'MepPanel.LicenseServer.exe')' --urls http://0.0.0.0:5268"
Write-Host ""
Write-Host "Chay nen tu dong theo Windows:"
Write-Host "  .\scripts\install-license-server-service.ps1"
