# Builds the StatStudio installer:
#   1. Publishes a self-contained win-x64 build (no .NET prerequisite) to dist\publish
#   2. Compiles installer\StatStudio.iss into dist\StatStudioSetup.exe
#
# Usage:  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try
{
    $publish = [System.IO.Path]::GetFullPath((Join-Path $root 'dist\publish'))
    $distRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'dist')) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $publish.StartsWith($distRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a publish directory outside dist: $publish"
    }
    if (Test-Path -LiteralPath $publish) {
        Remove-Item -LiteralPath $publish -Recurse -Force
    }

    Write-Host "==> Publishing self-contained build..." -ForegroundColor Cyan
    dotnet publish src/StatStudio.Wpf/StatStudio.Wpf.csproj `
        -c Release -r win-x64 --self-contained true -o $publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        throw "Inno Setup (ISCC.exe) not found. Install it with:  winget install JRSoftware.InnoSetup"
    }

    Write-Host "==> Compiling installer with $iscc ..." -ForegroundColor Cyan
    & $iscc "installer\StatStudio.iss"
    if ($LASTEXITCODE -ne 0) { throw "ISCC compile failed." }

    Write-Host "==> Done: dist\StatStudioSetup.exe" -ForegroundColor Green
}
finally { Pop-Location }
