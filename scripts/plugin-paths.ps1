# Duong dan plugin thong nhat (doc tu plugin.local.json).
$ErrorActionPreference = "Stop"

function Read-PluginLocalConfig {
    param([string]$RepoRoot)
    $path = Join-Path $RepoRoot "plugin.local.json"
    if (-not (Test-Path $path)) {
        return $null
    }
    return Get-Content $path -Raw | ConvertFrom-Json
}

function Get-PluginInstallRoot {
    param([string]$RepoRoot)
    $config = Read-PluginLocalConfig -RepoRoot $RepoRoot
    if ($config -and $config.pluginInstallRoot) {
        return $config.pluginInstallRoot
    }
    $parent = Split-Path $RepoRoot -Parent
    if ($parent) {
        return Join-Path $parent "plugin"
    }
    return Join-Path $RepoRoot "plugin"
}

function Get-PluginInstallBundlePath {
    param([string]$RepoRoot)
    $root = Get-PluginInstallRoot -RepoRoot $RepoRoot
    return Join-Path $root "MepPanel.Plugin.bundle"
}

function Get-PluginInstallContentsPath {
    param([string]$RepoRoot)
    return Join-Path (Get-PluginInstallBundlePath -RepoRoot $RepoRoot) "Contents"
}

function Get-AppPluginsLinkPath {
    return Join-Path $env:ProgramData "Autodesk\ApplicationPlugins\MepPanel.Plugin.bundle"
}

function Test-ReparsePoint {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }
    $item = Get-Item -LiteralPath $Path -Force
    return $item.Attributes -band [System.IO.FileAttributes]::ReparsePoint
}

function Set-AppPluginsJunction {
    param(
        [string]$LinkPath,
        [string]$TargetPath
    )

    $TargetPath = [System.IO.Path]::GetFullPath($TargetPath)
    if (-not (Test-Path -LiteralPath $TargetPath)) {
        throw "Target khong ton tai: $TargetPath"
    }

    if (Test-Path -LiteralPath $LinkPath) {
        # Junction/thu muc cu: luon -Recurse -Force, khong hoi Confirm (build tu dong).
        Remove-Item -LiteralPath $LinkPath -Recurse -Force -Confirm:$false -ErrorAction Stop
    }

    $parent = Split-Path $LinkPath -Parent
    New-Item -ItemType Directory -Force -Path $parent | Out-Null

    try {
        New-Item -ItemType Junction -Path $LinkPath -Target $TargetPath -Force | Out-Null
        Write-Host "==> AutoCAD load tu: $LinkPath -> $TargetPath"
        return $true
    }
    catch {
        Write-Host "==> Khong tao duoc junction. Copy truc tiep vao ApplicationPlugins..." -ForegroundColor Yellow
        Write-Host "    ($($_.Exception.Message))"
        Copy-Item -LiteralPath $TargetPath -Destination $LinkPath -Recurse -Force
        Write-Host "==> Da copy vao $LinkPath (nen bat Developer Mode hoac Admin de dung 1 ban duy nhat)"
        return $false
    }
}
