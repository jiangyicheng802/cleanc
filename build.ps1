$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root
New-Item -ItemType Directory -Force -Path dist, assets, _artifacts | Out-Null

if (-not (Test-Path "assets\app.ico")) {
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\make-icon.ps1")
    if ($LASTEXITCODE -ne 0) { throw "icon generation failed" }
}

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$srcs = Get-ChildItem (Join-Path $root "src") -Filter "*.cs" | Sort-Object Name | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /optimize /codepage:65001 /win32manifest:app.manifest /win32icon:assets\app.ico /out:dist\CleanC.exe $srcs /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }

$cn = "C" + [char]0x76D8 + [char]0x6E05 + [char]0x7406 + ".exe"
Copy-Item "dist\CleanC.exe" (Join-Path "dist" $cn) -Force

$exe = Get-Item "dist\CleanC.exe"
Write-Output ("Build OK: " + $exe.FullName + " (" + [math]::Round($exe.Length / 1024, 1) + " KB)")
Write-Output ("Deliverable: " + (Join-Path "dist" $cn))
