# Tim Python tren Windows (py / python / python3).
function Get-MepPython {
    foreach ($cmd in @("py", "python", "python3")) {
        $exe = Get-Command $cmd -ErrorAction SilentlyContinue
        if ($exe) {
            try {
                if ($cmd -eq "py") {
                    & $cmd -3 --version 2>$null | Out-Null
                    if ($LASTEXITCODE -eq 0) { return @{ Exe = $cmd; Prefix = "-3 " } }
                }
                else {
                    & $cmd --version 2>$null | Out-Null
                    if ($LASTEXITCODE -eq 0) { return @{ Exe = $cmd; Prefix = "" } }
                }
            } catch {}
        }
    }
    # Duong dan thuong gap
    $candidates = @(
        "$env:LocalAppData\Programs\Python\Python312\python.exe",
        "$env:LocalAppData\Programs\Python\Python311\python.exe",
        "$env:LocalAppData\Programs\Python\Python310\python.exe",
        "$env:ProgramFiles\Python312\python.exe",
        "$env:ProgramFiles\Python311\python.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return @{ Exe = $p; Prefix = "" } }
    }
    return $null
}
