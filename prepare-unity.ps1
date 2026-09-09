param([Parameter(Mandatory=$true)][string]$UnityProject)
$ErrorActionPreference = 'Stop'
$resolvedUnity = (Resolve-Path -LiteralPath $UnityProject).Path
$assetsPath = Join-Path $resolvedUnity 'Assets'
if (-not (Test-Path -LiteralPath $assetsPath)) { throw 'Choose an existing Unity project with an Assets directory.' }
$clioTarget = Join-Path $assetsPath 'Clio'
$simulationTarget = Join-Path $clioTarget 'Simulation'
$presentationTarget = Join-Path $clioTarget 'Presentation'
if (Test-Path -LiteralPath $clioTarget) { throw 'Assets\Clio already exists. Use a fresh project or review the existing files before importing.' }
New-Item -ItemType Directory -Path $simulationTarget,$presentationTarget -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src\Clio.Simulation') -Filter '*.cs' | Copy-Item -Destination $simulationTarget
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'unity\Assets\Clio\Presentation') -Force | Copy-Item -Destination $presentationTarget -Recurse
@'
{
  "name": "Clio.Simulation",
  "rootNamespace": "Clio.Simulation",
  "references": [],
  "noEngineReferences": true,
  "autoReferenced": true
}
'@ | Set-Content -LiteralPath (Join-Path $simulationTarget 'Clio.Simulation.asmdef') -Encoding UTF8
Write-Output "Imported Clio source into $clioTarget"
Write-Output 'Open the Unity project and follow unity\README.md. This script does not configure HDRP assets or install Unity.'
