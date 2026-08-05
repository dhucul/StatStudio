param(
    [Parameter(Mandatory = $true)]
    [string]$InstallDir
)

# End-to-end installer check in a caller-provided, isolated directory.
# Performs silent install -> launch + screenshot -> stop -> silent uninstall.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $root 'dist\StatStudioSetup.exe'
$result = Join-Path $root 'dist\e2e-result.txt'
$shot = Join-Path $root 'dist\e2e-shot.png'
$installDir = [System.IO.Path]::GetFullPath($InstallDir)
$installRoot = [System.IO.Path]::GetPathRoot($installDir)
if ($installDir -eq $installRoot) { throw "InstallDir cannot be a filesystem root." }
if (Test-Path -LiteralPath $installDir) {
    throw "InstallDir must be an unused path so cleanup cannot affect an existing installation: $installDir"
}
$exe = Join-Path $installDir 'StatStudio.exe'

$log = New-Object System.Collections.Generic.List[string]
function L($message) { $log.Add($message) }
function FileCount {
    if (-not (Test-Path -LiteralPath $installDir)) { return 0 }
    return @(Get-ChildItem -LiteralPath $installDir -Recurse -File -ErrorAction SilentlyContinue).Count
}

L "install target: $installDir"

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class Cap {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
    public static Bitmap Grab(IntPtr h) {
        RECT r;
        if (h == IntPtr.Zero || !GetWindowRect(h, out r)) throw new InvalidOperationException("Unable to read window bounds.");
        int w = r.R-r.L, ht = r.B-r.T;
        if (w <= 0 || ht <= 0) throw new InvalidOperationException("The application window has invalid bounds.");
        var b = new Bitmap(w,ht);
        try {
            using(var g=Graphics.FromImage(b)) {
                IntPtr hdc = g.GetHdc();
                try {
                    if (!PrintWindow(h, hdc, 2)) throw new InvalidOperationException("The application window could not be rendered.");
                }
                finally { g.ReleaseHdc(hdc); }
            }
        }
        catch { b.Dispose(); throw; }   // the caller only disposes a bitmap it actually received
        return b;
    }
}
"@

$failed = $false
$installed = $false
$ap = $null
$bmp = $null
try {
    if (-not (Test-Path -LiteralPath $setup)) { throw "Installer not found: $setup" }

    L "== install =="
    $ilog = Join-Path $root 'dist\e2e-install.log'
    $p = Start-Process $setup -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',
        "/DIR=`"$installDir`"", "/LOG=`"$ilog`"" -Wait -PassThru
    L "  setup exit code: $($p.ExitCode)"
    if ($p.ExitCode -ne 0) { throw "Setup returned exit code $($p.ExitCode)." }
    if (-not (Test-Path -LiteralPath $exe)) { throw "Installation completed without creating $exe." }
    $installed = $true
    L "  installed exe exists: True"

    L "== launch =="
    $ap = Start-Process $exe -PassThru
    for ($i = 0; $i -lt 60 -and $ap.MainWindowHandle -eq 0; $i++) {
        if ($ap.HasExited) { throw "The installed application exited before creating a window." }
        Start-Sleep -Milliseconds 200
        $ap.Refresh()
    }
    if ($ap.HasExited -or $ap.MainWindowHandle -eq 0) { throw "The installed application did not create a window." }
    L "  app running: True; window handle != 0: True"
    L "  window title: $($ap.MainWindowTitle)"

    [Cap]::SetForegroundWindow($ap.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    $bmp = [Cap]::Grab($ap.MainWindowHandle)
    $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
    if (-not (Test-Path -LiteralPath $shot) -or (Get-Item -LiteralPath $shot).Length -eq 0) {
        throw "Screenshot creation failed."
    }
    L "  screenshot: $shot"
}
catch {
    $failed = $true
    L "EXCEPTION: $($_.Exception.Message)"
}
finally {
    if ($null -ne $bmp) { $bmp.Dispose() }
    if ($null -ne $ap -and -not $ap.HasExited) {
        Stop-Process -Id $ap.Id -Force -ErrorAction SilentlyContinue
        L "  app stopped"
    }

    try {
        L "== uninstall =="
        $unins = Join-Path $installDir 'unins000.exe'
        if ($installed -and -not (Test-Path -LiteralPath $unins)) {
            throw "Installed application has no uninstaller."
        }
        if (Test-Path -LiteralPath $unins) {
            $up = Start-Process $unins -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru
            if ($up.ExitCode -ne 0) { throw "Uninstaller returned exit code $($up.ExitCode)." }
            for ($i = 0; $i -lt 60 -and (FileCount) -gt 0; $i++) {
                Start-Sleep -Milliseconds 500
            }
        }

        $remaining = FileCount
        L "  files removed: $($remaining -eq 0)"
        if ($remaining -ne 0) { throw "Uninstall left $remaining file(s) behind." }
        if (Test-Path -LiteralPath $installDir) {
            Remove-Item -LiteralPath $installDir -Recurse -Force
        }
        L "  install dir removed: $(-not (Test-Path -LiteralPath $installDir))"
    }
    catch {
        $failed = $true
        L "CLEANUP EXCEPTION: $($_.Exception.Message)"
        # InstallDir was required to be absent at entry, so this fallback only removes
        # artifacts produced by this run.
        if (Test-Path -LiteralPath $installDir) {
            Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    $log | Set-Content -LiteralPath $result -Encoding UTF8
}

Get-Content -LiteralPath $result
if ($failed) { exit 1 }
