# Cai bundle plugin cho MAY CLIENT.
# May client chi tro URL License Server trung tam; KHONG chay License Server va KHONG giu database.
param(
    [Parameter(Mandatory = $true)]
    [string]$LicenseServerUrl,
    [string]$InstallRoot = (Join-Path $env:ProgramData "Autodesk\ApplicationPlugins"),
    [switch]$AllowLocalServer
)

$ErrorActionPreference = "Stop"

try {
    $uri = [Uri]$LicenseServerUrl
}
catch {
    throw "LicenseServerUrl khong hop le. Vi du: https://license.congty.vn/"
}

if ($uri.Scheme -notin @("http", "https") -or -not $uri.IsAbsoluteUri) {
    throw "LicenseServerUrl phai la URL http/https day du."
}

if (-not $AllowLocalServer -and $uri.Host -in @("localhost", "127.0.0.1", "::1")) {
    throw "May client phai tro toi License Server trung tam, khong dung localhost. Vi du: https://license.congty.vn/"
}

if (Get-Process -Name "acad" -ErrorAction SilentlyContinue) {
    throw "AutoCAD dang mo. Hay dong AutoCAD truoc khi cai/cap nhat plugin."
}

$bundleSource = Join-Path $PSScriptRoot "MepPanel.Plugin.bundle"
if (-not (Test-Path $bundleSource)) {
    $bundleSource = Join-Path $PSScriptRoot "..\bundle\MepPanel.Plugin.bundle"
}
if (-not (Test-Path (Join-Path $bundleSource "Contents\MepPanel.AutoCAD.dll"))) {
    throw "Khong tim thay MepPanel.Plugin.bundle can cai."
}

$bundleDestination = Join-Path $InstallRoot "MepPanel.Plugin.bundle"
New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
if (Test-Path $bundleDestination) {
    Remove-Item $bundleDestination -Recurse -Force -Confirm:$false
}
Copy-Item $bundleSource $bundleDestination -Recurse -Force

$configPath = Join-Path $bundleDestination "Contents\MepPanel.config.json"
$configExamplePath = Join-Path $bundleDestination "Contents\MepPanel.config.json.example"
$config = [ordered]@{
    licenseServerUrl = $uri.AbsoluteUri
}

if (Test-Path $configExamplePath) {
    try {
        $example = Get-Content $configExamplePath -Raw | ConvertFrom-Json
        if ($example.pipeLibraryDwg) {
            $config.pipeLibraryDwg = [string]$example.pipeLibraryDwg
        }
    }
    catch {
        # URL van du de plugin ket noi Server; template loi khong chan cai dat.
    }
}

[System.IO.File]::WriteAllText(
    $configPath,
    ($config | ConvertTo-Json -Depth 4),
    (New-Object System.Text.UTF8Encoding $false))

Get-ChildItem (Join-Path $bundleDestination "Contents") -Filter "*.dll" -File |
    ForEach-Object { Unblock-File $_.FullName -ErrorAction SilentlyContinue }

Write-Host ""
Write-Host "Da cai plugin client: $bundleDestination"
Write-Host "License Server trung tam: $($uri.AbsoluteUri)"
Write-Host "Mo AutoCAD -> MEPDB -> dang nhap."
