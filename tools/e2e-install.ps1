# End-to-end installer check (per-user install, no elevation needed).
# Does: silent install -> launch + screenshot -> kill -> silent uninstall, and
# writes results + a screenshot to dist\ for the caller to read back.
$ErrorActionPreference = 'Continue'

$root = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $root 'dist\StatStudioSetup.exe'
$result = Join-Path $root 'dist\e2e-result.txt'
$shot = Join-Path $root 'dist\e2e-shot.png'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\StatStudio'
$exe = Join-Path $installDir 'StatStudio.exe'

$log = New-Object System.Collections.Generic.List[string]
function L($m) { $log.Add($m); }
L "install target: $installDir"

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Drawing; using System.Runtime.InteropServices;
public class Cap {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
    public static Bitmap Grab(IntPtr h){ RECT r; GetWindowRect(h, out r); int w=r.R-r.L, ht=r.B-r.T;
        var b=new Bitmap(w,ht); using(var g=Graphics.FromImage(b)) g.CopyFromScreen(r.L,r.T,0,0,new Size(w,ht)); return b; }
}
"@

try {
    # 1. INSTALL (silent)
    L "== install =="
    $ilog = Join-Path $root 'dist\e2e-install.log'
    $p = Start-Process $setup -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=$ilog" -Wait -PassThru
    L "  setup exit code: $($p.ExitCode)"
    L "  installed exe exists: $(Test-Path $exe)"

    if (Test-Path $exe) {
        # 2. LAUNCH + screenshot
        L "== launch =="
        $ap = Start-Process $exe -PassThru
        for ($i=0; $i -lt 60 -and $ap.MainWindowHandle -eq 0; $i++){ Start-Sleep -Milliseconds 200; $ap.Refresh() }
        Start-Sleep -Seconds 2
        L "  app running: $(-not $ap.HasExited); window handle != 0: $($ap.MainWindowHandle -ne 0)"
        L "  window title: $($ap.MainWindowTitle)"
        if ($ap.MainWindowHandle -ne 0) {
            [Cap]::SetForegroundWindow($ap.MainWindowHandle) | Out-Null
            Start-Sleep -Milliseconds 500
            $bmp = [Cap]::Grab($ap.MainWindowHandle); $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
            L "  screenshot: $shot"
        }
        Stop-Process -Id $ap.Id -Force -ErrorAction SilentlyContinue
        L "  app stopped"
    }

    # 3. UNINSTALL (silent) -- poll on FILES being gone (Inno's uninstaller relaunches
    #    from temp and can leave an empty dir briefly), then drop any empty leftover.
    L "== uninstall =="
    $unins = Join-Path $installDir 'unins000.exe'
    L "  uninstaller exists: $(Test-Path $unins)"
    function FileCount { @(Get-ChildItem $installDir -Recurse -File -ErrorAction SilentlyContinue).Count }
    if (Test-Path $unins) {
        Start-Process $unins -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait | Out-Null
        for ($i=0; $i -lt 60; $i++) {
            if (-not (Test-Path $installDir) -or (FileCount) -eq 0) { break }
            Start-Sleep -Milliseconds 500
        }
        if ((Test-Path $installDir) -and (FileCount) -eq 0) {
            Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    $remaining = if (Test-Path $installDir) { FileCount } else { 0 }
    L "  files removed: $($remaining -eq 0)"
    L "  install dir removed: $(-not (Test-Path $installDir))"
}
catch { L "EXCEPTION: $($_.Exception.Message)" }

$log | Set-Content $result -Encoding UTF8
Get-Content $result
