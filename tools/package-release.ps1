param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$clioReleaseRoot = Split-Path -Parent $PSScriptRoot
$clioVersion = 'sage-37'
$clioBuild = Join-Path $clioReleaseRoot ('build\' + $clioVersion)
$clioPackageName = 'clio-' + $clioVersion + '-windows'
$clioStage = Join-Path $clioReleaseRoot ('build\release-stage\' + [Guid]::NewGuid().ToString('N') + '\' + $clioPackageName)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $clioReleaseRoot 'build\releases' }
New-Item -ItemType Directory -Force -Path $clioStage, $OutputDirectory | Out-Null
foreach ($clioBinary in @('Clio.exe', 'Clio.Simulation.dll')) {
    $clioBinarySource = Join-Path $clioBuild $clioBinary
    if (-not (Test-Path -LiteralPath $clioBinarySource -PathType Leaf)) { throw 'Run build.ps1 before packaging.' }
    Copy-Item -LiteralPath $clioBinarySource -Destination $clioStage -Force
}
$clioPackageTools = Join-Path $clioStage 'tools'
New-Item -ItemType Directory -Force -Path $clioPackageTools | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'prepare-adviser-voice.ps1'), (Join-Path $PSScriptRoot 'enable-offline-voice.ps1') -Destination $clioPackageTools -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'voice-licenses') -Destination $clioPackageTools -Recurse -Force
Copy-Item -LiteralPath (Join-Path $clioReleaseRoot 'src\Clio.Desktop\Assets\Advisers\Voice\README.md') -Destination (Join-Path $clioStage 'voice-credits.md') -Force
$clioSetupCommand = @'
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\enable-offline-voice.ps1"
if errorlevel 1 (
  echo Voice setup did not finish. Clio can still use installed Windows speech.
)
pause
'@
Set-Content -LiteralPath (Join-Path $clioStage 'Enable offline adviser voice.cmd') -Value $clioSetupCommand -Encoding Ascii
$clioReadme = @'
CLIO: THE LIVING ATLAS — sage-37
Experimental Windows desktop prototype. Updated September 13, 2026.

PLAY
Extract the entire ZIP to a folder, then open Clio.exe. Keep
Clio.Simulation.dll beside it. Windows 10 or 11 with .NET Framework 4.x
is the intended environment. No compiler, account or connection is needed.

New stories use Guided beginning: meet the First Adviser, end the first
turn, then spend influence on Gather or regional Move. Wildlife can be
inspected from turn 2. Move previews destinations on the map. The adviser
reports supply changes and animal sightings. V mutes or unmutes narration.
F11 returns the full-screen game to a window.

Uncheck Guided beginning in New story for the older game modes.
Save before updating and retain a copy of your original story. This build
writes CLIO-STORY-16 and records the guided-wildlife upgrade; earlier
versions cannot read all new saves.

VOICE
The two synthetic British opening recordings are included. Live reports
use an installed Windows voice. For the same optional British voice in
live reports, close Clio and run Enable offline adviser voice.cmd. That
explicit setup downloads approximately 97 MB from the pinned upstream
Piper and voice-model releases, verifies their SHA-256 checksums and
installs them beside the game. Reopen Clio afterward. The game performs
no automatic downloads and sends no narration text to a service.

See voice-credits.md and tools/voice-licenses for credits and notices.

STATUS
Compilation and archive integrity verified. Gameplay and audio have not
been verified for this release. The prototype is still in development.

Source: https://github.com/mcrombie/clio
Source revision: local e9dab30aff51bbb33bb117c93017e854b6f91d36
Project: https://cromblog.vercel.app/games#clio
'@
Set-Content -LiteralPath (Join-Path $clioStage 'README.txt') -Value $clioReadme -Encoding UTF8
$clioChecksums = @('Clio.exe', 'Clio.Simulation.dll') | ForEach-Object {
    ((Get-FileHash -LiteralPath (Join-Path $clioStage $_) -Algorithm SHA256).Hash.ToLowerInvariant()) + '  ' + $_
}
Set-Content -LiteralPath (Join-Path $clioStage 'SHA256SUMS.txt') -Value $clioChecksums -Encoding Ascii
$clioArchive = Join-Path $OutputDirectory ($clioPackageName + '.zip')
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$clioArchiveStream = [System.IO.File]::Open($clioArchive, [System.IO.FileMode]::Create)
$clioZip = New-Object System.IO.Compression.ZipArchive($clioArchiveStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
try {
    foreach ($clioFile in (Get-ChildItem -LiteralPath $clioStage -Recurse -File)) {
        $clioEntryName = $clioPackageName + '/' + $clioFile.FullName.Substring($clioStage.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($clioZip, $clioFile.FullName, $clioEntryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $clioZip.Dispose(); $clioArchiveStream.Dispose() }
$clioArchiveHash = (Get-FileHash -LiteralPath $clioArchive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $OutputDirectory ($clioPackageName + '.sha256')) -Value ($clioArchiveHash + '  ' + $clioPackageName + '.zip') -Encoding Ascii
@{
    version = $clioVersion
    sourceRevision = 'e9dab30aff51bbb33bb117c93017e854b6f91d36'
    archive = $clioPackageName + '.zip'
    bytes = (Get-Item -LiteralPath $clioArchive).Length
    sha256 = $clioArchiveHash
    platform = 'Windows 10 or 11 with .NET Framework 4.x'
    voice = 'Opening recordings included; Windows speech for live reports; optional offline British voice setup.'
    verification = 'Compilation and archive integrity verified. Gameplay and audio have not been verified for this release.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'release.json') -Encoding UTF8
Write-Output $clioArchive
Write-Output $clioArchiveHash
