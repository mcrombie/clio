$ErrorActionPreference = 'Stop'
$clioPackageRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'prepare-adviser-voice.ps1')
$clioVoiceSource = Join-Path $clioPackageRoot 'build\narration\runtime'
$clioVoiceDestination = Join-Path $clioPackageRoot 'voice'
New-Item -ItemType Directory -Path $clioVoiceDestination -Force | Out-Null
Get-ChildItem -LiteralPath $clioVoiceSource | Copy-Item -Destination $clioVoiceDestination -Recurse -Force
Write-Host 'Offline First Adviser voice is ready. Close and reopen Clio to use it.'
