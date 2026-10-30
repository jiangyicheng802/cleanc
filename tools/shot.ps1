param(
    [string]$Exe = "dist\CleanC.exe",
    [string]$ArgLine = "",
    [string]$Out = "_artifacts\ui.png",
    [int]$DelayMs = 3500,
    [string]$WindowTitle = ""
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinCap {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int nMax);
    public static IntPtr FindWindowByPid(uint pid, string titlePart) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint wp;
            GetWindowThreadProcessId(h, out wp);
            if (wp != pid) return true;
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, 512);
            string t = sb.ToString();
            if (titlePart.Length == 0 || t.Contains(titlePart)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@
[WinCap]::SetProcessDPIAware() | Out-Null

$exePath = (Resolve-Path $Exe).Path
Start-Sleep -Milliseconds 1200
$p = $null
$argList = @()
if ($ArgLine -ne "") { $argList = $ArgLine -split " " }
for ($try = 0; $try -lt 6; $try++) {
    try {
        if ($argList.Count -gt 0) { $p = Start-Process -FilePath $exePath -ArgumentList $argList -PassThru }
        else { $p = Start-Process -FilePath $exePath -PassThru }
        break
    } catch {
        Start-Sleep -Milliseconds 900
    }
}
if ($p -eq $null) { throw "cannot start app (blocked)" }
Start-Sleep -Milliseconds 900
for ($i = 0; $i -lt 60; $i++) {
    $p.Refresh()
    if ($p.MainWindowHandle -ne 0) { break }
    Start-Sleep -Milliseconds 150
}
if ($p.MainWindowHandle -eq 0) { throw "main window not found" }
$mainHwnd = $p.MainWindowHandle
[WinCap]::SetWindowPos($mainHwnd, [IntPtr]::Zero, -4000, -4000, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null
Start-Sleep -Milliseconds $DelayMs

$hwnd = $mainHwnd
if ($WindowTitle -ne "") {
    for ($i = 0; $i -lt 20; $i++) {
        $hwnd = [WinCap]::FindWindowByPid([uint32]$p.Id, $WindowTitle)
        if ($hwnd -ne 0) { break }
        Start-Sleep -Milliseconds 200
    }
    if ($hwnd -eq 0) { $hwnd = $mainHwnd }
}

$rect = New-Object WinCap+RECT
[WinCap]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
$outDir = Split-Path -Parent (Join-Path (Get-Location) $Out)
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [WinCap]::PrintWindow($hwnd, $hdc, 2)
$g.ReleaseHdc($hdc)
if (-not $ok) { $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h))) }
$g.Dispose()
$outPath = Join-Path (Get-Location) $Out
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$p.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 2
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
Write-Output ("saved " + $outPath + " (" + $w + "x" + $h + ")")
