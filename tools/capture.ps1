param(
    [string]$Exe,
    [string]$Out,
    [string[]]$AppArgs = @(),
    [string]$WindowTitle = ""   # optional: capture the process window whose title contains this
)

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
        RECT r; GetWindowRect(h, out r);
        int w = r.Right - r.Left, ht = r.Bottom - r.Top;
        var bmp = new Bitmap(w, ht);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, ht));
        return bmp;
    }
}
"@

try { [WinCap]::SetProcessDpiAwareness(2) | Out-Null } catch {}

$p = if ($AppArgs.Count -gt 0) { Start-Process $Exe -ArgumentList $AppArgs -PassThru }
     else { Start-Process $Exe -PassThru }
for ($i = 0; $i -lt 50 -and $p.MainWindowHandle -eq 0; $i++) {
    Start-Sleep -Milliseconds 200
    $p.Refresh()
}
Start-Sleep -Seconds 2

$h = $p.MainWindowHandle
if ($WindowTitle -ne "") {
    $hh = [WinCap]::Find([uint32]$p.Id, $WindowTitle)
    if ($hh -ne [IntPtr]::Zero) { $h = $hh }
}
[WinCap]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
[WinCap]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 800

$bmp = [WinCap]::Grab($h)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$w = $bmp.Width; $ht = $bmp.Height
$bmp.Dispose()
Write-Output "Captured ${w}x${ht} -> $Out  (pid $($p.Id))"
Stop-Process -Id $p.Id -Force
