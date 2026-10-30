# Icon generator wrapper.
# The actual drawing lives in tools/make-icon.py (pure standard library, no Pillow needed)
# so the diagonal-split logo stays identical to Theme.DrawLogo in the app.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$script = Join-Path $root "make-icon.py"

$candidates = New-Object System.Collections.ArrayList
foreach ($name in @("python", "python3", "py")) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { [void]$candidates.Add($cmd.Source) }
}

$done = $false
foreach ($c in $candidates) {
    try {
        & $c $script
        if ($LASTEXITCODE -eq 0) { $done = $true; break }
    } catch {
    }
}
if (-not $done) { throw "icon generation failed: no working python found, run tools/make-icon.py manually" }
