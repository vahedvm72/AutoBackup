[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Out = "shot.png",
    [string]$Arguments = "",
    [int]$DelayMs = 1500,
    [switch]$KeepOpen
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class WinShotNative
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachFlag);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

[WinShotNative]::SetProcessDPIAware() | Out-Null

$exePath = (Resolve-Path $Exe).Path
$startArgs = @{ FilePath = $exePath; PassThru = $true }
if ($Arguments) {
    $startArgs.ArgumentList = $Arguments
    $startArgs.UseShellExecute = $false
}
$process = Start-Process @startArgs

$handle = [IntPtr]::Zero
for ($i = 0; $i -lt 80; $i++) {
    Start-Sleep -Milliseconds 200
    $process.Refresh()
    if ($process.HasExited) { throw "Process exited before a window appeared." }
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
        $handle = $process.MainWindowHandle
        break
    }
}

if ($handle -eq [IntPtr]::Zero) { throw "No main window was found." }
$topMost = [IntPtr](-1)
$noTopMost = [IntPtr](-2)
[WinShotNative]::ShowWindow($handle, 9) | Out-Null
[WinShotNative]::SetWindowPos($handle, $topMost, 0, 0, 0, 0, 0x0053) | Out-Null
$foreground = [WinShotNative]::GetForegroundWindow()
$foregroundPid = 0
$foregroundThread = [WinShotNative]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid)
$currentThread = [WinShotNative]::GetCurrentThreadId()
$attached = $false
if ($foregroundThread -ne 0 -and $foregroundThread -ne $currentThread) {
    $attached = [WinShotNative]::AttachThreadInput($currentThread, $foregroundThread, $true)
}
[WinShotNative]::SetForegroundWindow($handle) | Out-Null
if ($attached) {
    [WinShotNative]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null
}
Start-Sleep -Milliseconds $DelayMs

$rect = New-Object WinShotNative+RECT
$width = 0; $height = 0
if ([WinShotNative]::DwmGetWindowAttribute($handle, 9, [ref]$rect, 16) -eq 0) {
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
}
if ($width -le 0 -or $height -le 0) {
    [WinShotNative]::GetWindowRect($handle, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
}
if ($width -le 0 -or $height -le 0) { throw "Window bounds could not be read." }

$outPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Out))
$outDir = [System.IO.Path]::GetDirectoryName($outPath)
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
$graphics.Dispose()
$bitmap.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

$process.Refresh()
Write-Output ("PID={0} TITLE={1} RECT={2},{3} {4}x{5} FILE={6}" -f $process.Id, $process.MainWindowTitle, $rect.Left, $rect.Top, $width, $height, $outPath)

if (-not $KeepOpen) {
    if ($process.CloseMainWindow()) {
        $process.WaitForExit(3000) | Out-Null
    }
    if (-not $process.HasExited) {
        $process.Kill()
    }
}
