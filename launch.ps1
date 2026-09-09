param([switch]$ResolveOnly)
$ErrorActionPreference = 'Stop'
$clioBuildRoot = Join-Path $PSScriptRoot 'build'
$clioCurrentFile = Join-Path $clioBuildRoot 'current-build.txt'

function Resolve-ClioExecutable {
    if (-not (Test-Path -LiteralPath $clioCurrentFile)) { return $null }
    $clioVersion = (Get-Content -LiteralPath $clioCurrentFile -Raw).Trim()
    if ($clioVersion -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'The current Clio build record is invalid.' }
    $clioVersionPath = Join-Path $clioBuildRoot $clioVersion
    $clioExecutable = Join-Path $clioVersionPath 'Clio.exe'
    if ((Test-Path -LiteralPath $clioExecutable -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $clioVersionPath 'Clio.Simulation.dll') -PathType Leaf)) {
        return $clioExecutable
    }
    return $null
}

try {
    $clioTarget = Resolve-ClioExecutable
    if (-not $clioTarget) {
        if ($ResolveOnly) { throw 'No completed Clio build is available.' }
        & (Join-Path $PSScriptRoot 'build.ps1') | Out-Null
        $clioTarget = Resolve-ClioExecutable
        if (-not $clioTarget) { throw 'The Clio build did not produce a launchable application.' }
    }
    if ($ResolveOnly) { Write-Output $clioTarget; return }
    # Clio is the interactive application the user explicitly chose to open.
    Start-Process -FilePath $clioTarget -WorkingDirectory (Split-Path -Parent $clioTarget)
}
catch {
    if ($ResolveOnly) { throw }
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("Clio could not start.`r`n`r`n" + $_.Exception.Message, 'Clio', 'OK', 'Error') | Out-Null
    exit 1
}
