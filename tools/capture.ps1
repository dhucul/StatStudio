param(
    [Parameter(Mandatory = $true)]
    [string]$Exe,
    [Parameter(Mandatory = $true)]
    [string]$Out,
    [string[]]$AppArgs = @(),
    [string]$WindowTitle = ""   # optional: capture the process window whose title contains this
)

$ErrorActionPreference = 'Stop'

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
public class WinCap {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int v);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static IntPtr Find(uint pid, string titleContains) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) => {
            uint wp; GetWindowThreadProcessId(h, out wp);
            if (wp != pid || !IsWindowVisible(h)) return true;
            var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
            if (sb.ToString().Contains(titleContains)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static Bitmap Grab(IntPtr h) {
        RECT r;
        if (h == IntPtr.Zero || !GetWindowRect(h, out r)) throw new InvalidOperationException("Unable to read the window bounds.");
        int w = r.Right - r.Left, ht = r.Bottom - r.Top;
        if (w <= 0 || ht <= 0) throw new InvalidOperationException("The window has invalid bounds.");
        var bmp = new Bitmap(w, ht);
        try {
            using (var g = Graphics.FromImage(bmp)) {
                IntPtr hdc = g.GetHdc();
                try {
                    if (!PrintWindow(h, hdc, 2)) throw new InvalidOperationException("The window could not be rendered.");
                }
                finally { g.ReleaseHdc(hdc); }
            }
        }
        catch { bmp.Dispose(); throw; }   // the caller only disposes a bitmap it actually received
        return bmp;
    }
}
"@

try { [WinCap]::SetProcessDpiAwareness(2) | Out-Null } catch {}

$p = $null
$bmp = $null
try {
    $p = if ($AppArgs.Count -gt 0) { Start-Process $Exe -ArgumentList $AppArgs -PassThru }
         else { Start-Process $Exe -PassThru }
    for ($i = 0; $i -lt 50 -and $p.MainWindowHandle -eq 0; $i++) {
        if ($p.HasExited) { throw "The application exited before creating a window." }
        Start-Sleep -Milliseconds 200
        $p.Refresh()
    }
    Start-Sleep -Seconds 2

    if ($p.HasExited) { throw "The application exited before capture." }
    $h = $p.MainWindowHandle
    if ($WindowTitle -ne "") {
        $hh = [WinCap]::Find([uint32]$p.Id, $WindowTitle)
        if ($hh -ne [IntPtr]::Zero) { $h = $hh }
    }
    if ($h -eq [IntPtr]::Zero) { throw "No capturable application window was created." }
    [WinCap]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
    [WinCap]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 800

    $bmp = [WinCap]::Grab($h)
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $w = $bmp.Width; $ht = $bmp.Height
    Write-Output "Captured ${w}x${ht} -> $Out  (pid $($p.Id))"
}
finally {
    if ($null -ne $bmp) { $bmp.Dispose() }
    if ($null -ne $p -and -not $p.HasExited) {
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    }
}
