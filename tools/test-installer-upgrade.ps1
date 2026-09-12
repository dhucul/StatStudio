param(
    [Parameter(Mandatory = $true)]
    [string]$InstalledDir
)

# Compile installer-upgrade-probe.iss first. The probe reads registry/directory
# state and writes its report, then aborts initialization without installing.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$probe = Join-Path $repoRoot 'dist\upgrade-probe\UpgradeProbe.exe'
$fixtureRoot = Join-Path $repoRoot ("dist\upgrade-probe\test-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$empty = Join-Path $fixtureRoot 'empty'
$foreign = Join-Path $fixtureRoot 'foreign'
New-Item -ItemType Directory -Path $empty, $foreign | Out-Null
$sentinel = Join-Path $foreign 'unrelated.json'
'preserve this file' | Set-Content -LiteralPath $sentinel
$sentinelHash = (Get-FileHash -LiteralPath $sentinel).Hash
$installedExe = Join-Path $InstalledDir 'StatStudio.exe'
$installedHash = (Get-FileHash -LiteralPath $installedExe).Hash

$cases = @(
    @{ Name='existing-install'; Target=$InstalledDir; Registered='true'; Allowed='true' },
    @{ Name='existing-case-insensitive'; Target=$InstalledDir.ToUpperInvariant(); Registered='true'; Allowed='true' },
    @{ Name='empty-folder'; Target=$empty; Registered='false'; Allowed='true' },
    @{ Name='new-folder'; Target=(Join-Path $fixtureRoot 'new'); Registered='false'; Allowed='true' },
    @{ Name='unrelated-folder'; Target=$foreign; Registered='false'; Allowed='false' }
)
foreach ($case in $cases) {
    $report = Join-Path $fixtureRoot ($case.Name + '.txt')
    $run = Start-Process -FilePath $probe -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
        "/TARGET=`"$($case.Target)`"", "/REPORT=`"$report`""
    )
    # A nonzero exit is expected: InitializeSetup deliberately returns False.
    if (-not (Test-Path -LiteralPath $report)) { throw "Probe did not report for $($case.Name) (exit $($run.ExitCode))." }
    $lines = Get-Content -LiteralPath $report
    if ($lines -notcontains "registered=$($case.Registered)" -or $lines -notcontains "allowed=$($case.Allowed)") {
        throw "Upgrade validation failed for $($case.Name): $($lines -join ', ')"
    }
    Write-Output "PASS $($case.Name)"
}
if ((Get-FileHash -LiteralPath $installedExe).Hash -ne $installedHash) { throw 'Installed application changed during the probe.' }
if ((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash) { throw 'Unrelated file changed during the probe.' }
Write-Output 'PASS installed application and unrelated file unchanged'
Write-Output "Reports: $fixtureRoot"
