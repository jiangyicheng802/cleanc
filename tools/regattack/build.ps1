$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

& $csc /nologo /target:exe /optimize /codepage:65001 /out:RegAttackSim.exe RegAttackSim.cs /r:System.dll /r:System.Core.dll
if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }

$exe = Get-Item "RegAttackSim.exe"
Write-Output ("Build OK: " + $exe.FullName + " (" + [math]::Round($exe.Length / 1024, 1) + " KB)")
