# Patch MepCabinetRenderService trong MepPanelMvp de goi Blender 3D.
# 1) Neu da co file: sua in-place (args + timeout) - an toan, khong ghi de toan bo.
# 2) Neu chua co: copy patch day du.
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceRoot
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PatchFile = Join-Path $Root "patches\MepPanelMvp\src\MepPanel.Blocks.AutoCAD\Drawing\MepCabinetRenderService.cs"

$candidates = @(
    (Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\Drawing\MepCabinetRenderService.cs"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Drawing\MepCabinetRenderService.cs"),
    (Join-Path $PluginSourceRoot "src\MepPanel.AutoCAD\Services\MepCabinetRenderService.cs")
)

$existing = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

function Patch-CallPythonRenderer {
    param([string]$Path)

    $text = [System.IO.File]::ReadAllText($Path)
    $orig = $text
    $changed = $false

    # Da co Blender prompt -> khong can sua nua
    if ($text -match 'Blender3D|--quality') {
        Write-Host "   Da co Blender args: $Path"
        return $true
    }

    # Tang timeout 30s -> 600s (Blender can nhieu thoi gian)
    if ($text -match 'WaitForExit\s*\(\s*30000\s*\)') {
        $text = $text -replace 'WaitForExit\s*\(\s*30000\s*\)', 'WaitForExit(600000)'
        $changed = $true
        Write-Host "   + Timeout 30s -> 600s"
    }
    elseif ($text -match 'WaitForExit\s*\(\s*\d+\s*\)' -and $text -notmatch 'WaitForExit\s*\(\s*600000\s*\)') {
        $text = [regex]::Replace($text, 'WaitForExit\s*\(\s*\d+\s*\)', 'WaitForExit(600000)', 1)
        $changed = $true
        Write-Host "   + Timeout -> 600s"
    }

    # Them --quality blender --mode interior vao args
    # Pattern cu: $"\"{scriptPath}\" --input \"{inputPath}\" --output \"{outputPath}\""
    $patterns = @(
        @{
            Old = '\$"\\"\{scriptPath\}\\" --input \\"\{inputPath\}\\" --output \\"\{outputPath\}\\""'
            New = '"\"" + scriptPath + "\" --mode interior --quality blender --engine cycles --samples 256 --input \"" + inputPath + "\" --output \"" + outputPath + "\""'
        }
    )

    # String replace don gian hon - nhieu bien the
    if ($text -match '--input' -and $text -notmatch '--quality') {
        # Thay khoi args assignment
        $newArgsBlock = @'
            string args = inputPath != null
                ? "\"" + scriptPath + "\" --mode interior --quality blender --engine cycles --samples 256 --input \"" + inputPath + "\" --output \"" + outputPath + "\""
                : "\"" + scriptPath + "\" --mode interior --quality blender --engine cycles --samples 256 --demo --output \"" + outputPath + "\"";
'@

        # Match C# interpolated or concatenated args
        $rx = '(?s)string\s+args\s*=\s*inputPath\s*!=\s*null\s*\?[\s\S]{10,400}?outputPath[^;]+;'
        if ([regex]::IsMatch($text, $rx)) {
            $text = [regex]::Replace($text, $rx, $newArgsBlock.Trim(), 1)
            $changed = $true
            Write-Host "   + Them --quality blender vao args"
        }
        else {
            # Fallback: chen flag sau scriptPath trong chuoi
            if ($text -match '--demo --output' -or $text -match '--input') {
                $text = $text.Replace('--demo --output', '--mode interior --quality blender --engine cycles --samples 256 --demo --output')
                $text = $text.Replace('--input', '--mode interior --quality blender --engine cycles --samples 256 --input')
                # Tranh duplicate neu ca 2 nhanh deu match (chi 1 lan moi nganh)
                while ($text -match '(--mode interior --quality blender --engine cycles --samples 256 ){2}') {
                    $text = $text.Replace('--mode interior --quality blender --engine cycles --samples 256 --mode interior --quality blender --engine cycles --samples 256 ', '--mode interior --quality blender --engine cycles --samples 256 ')
                }
                $changed = $true
                Write-Host "   + Chen --quality blender vao chuoi args"
            }
        }
    }

    # Thong bao dang render Blender
    if ($changed -and $text -match 'Dang render' -and $text -notmatch 'Blender') {
        $text = $text.Replace('Dang render...', 'Dang render Blender 3D (2-5 phut)...')
        $text = $text.Replace('Dang render...', 'Dang render Blender 3D (2-5 phut)...')
    }

    if ($changed) {
        [System.IO.File]::WriteAllText($Path, $text)
        Write-Host "   OK patch in-place: $Path"
        return $true
    }

    Write-Host "   (khong match pattern args - se ghi de file patch)"
    return $false
}

if ($existing) {
    $ok = Patch-CallPythonRenderer -Path $existing
    if (-not $ok) {
        # Backup + ghi de bang ban Blender day du
        $bak = $existing + ".bak"
        Copy-Item $existing $bak -Force
        Copy-Item $PatchFile $existing -Force
        Write-Host "   OK ghi de (backup: $bak)"
    }
}
else {
    $targetDir = Join-Path $PluginSourceRoot "src\MepPanel.Blocks.AutoCAD\Drawing"
    if (-not (Test-Path (Split-Path $targetDir))) {
        Write-Host "   (bo qua - khong co MepPanel.Blocks.AutoCAD trong source)"
        Write-Host "   Python renderer mac dinh --quality blender: chi can install-renderer-devices.ps1"
        exit 0
    }
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    Copy-Item $PatchFile (Join-Path $targetDir "MepCabinetRenderService.cs") -Force
    Write-Host "   OK copy moi MepCabinetRenderService.cs"
}

Write-Host "   Xong. Build MepPanel.AutoCAD se nhan patch (khong can build Blocks rieng)."
