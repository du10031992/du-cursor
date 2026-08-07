# Tim Python: py / python / python3 / Python trong Blender.
function Get-MepPython {
    foreach ($cmd in @("py", "python", "python3")) {
        $found = Get-Command $cmd -ErrorAction SilentlyContinue
        if (-not $found) { continue }
        try {
            if ($cmd -eq "py") {
                $null = & $cmd -3 --version 2>&1
                if ($LASTEXITCODE -eq 0) { return @{ Exe = $cmd; Prefix = "-3"; Kind = "launcher" } }
            }
            else {
                $ver = & $cmd --version 2>&1 | Out-String
                if ($ver -match "Python" -and $ver -notmatch "was not found" -and $ver -notmatch "Microsoft Store") {
                    return @{ Exe = $found.Source; Prefix = ""; Kind = "python" }
                }
            }
        } catch {}
    }

    $candidates = @(
        "$env:LocalAppData\Programs\Python\Python313\python.exe",
        "$env:LocalAppData\Programs\Python\Python312\python.exe",
        "$env:LocalAppData\Programs\Python\Python311\python.exe",
        "$env:LocalAppData\Programs\Python\Python310\python.exe",
        "$env:ProgramFiles\Python312\python.exe",
        "$env:ProgramFiles\Python311\python.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return @{ Exe = $p; Prefix = ""; Kind = "python" } }
    }

    # Python di kem Blender (khong can cai Python rieng)
    $bf = Join-Path ${env:ProgramFiles} "Blender Foundation"
    if (Test-Path $bf) {
        $blenderPy = Get-ChildItem $bf -Recurse -Filter "python.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\python\\bin\\python\.exe$' } |
            Select-Object -First 1
        if ($blenderPy) {
            return @{ Exe = $blenderPy.FullName; Prefix = ""; Kind = "blender-python" }
        }
    }

    return $null
}

function Get-MepBlender {
    $cmd = Get-Command blender -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $bf = Join-Path ${env:ProgramFiles} "Blender Foundation"
    if (Test-Path $bf) {
        $exe = Get-ChildItem $bf -Directory | Sort-Object Name -Descending | ForEach-Object {
            Join-Path $_.FullName "blender.exe"
        } | Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($exe) { return $exe }
    }
    return $null
}
