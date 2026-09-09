param([switch]$Test, [switch]$Render)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'The Windows .NET Framework C# compiler is required for this build script.' }
$outputPath = Join-Path $projectRoot 'build\automatic-28'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$coreSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Clio.Simulation') -Filter '*.cs' | ForEach-Object { $_.FullName })
$coreLibrary = Join-Path $outputPath 'Clio.Simulation.dll'
& $compilerPath /nologo /target:library /optimize+ /warn:4 "/out:$coreLibrary" $coreSources
if ($LASTEXITCODE -ne 0) { throw 'Simulation compilation failed.' }
$desktopSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Clio.Desktop') -Filter '*.cs' | ForEach-Object { $_.FullName })
$desktopApp = Join-Path $outputPath 'Clio.exe'
$desktopIcon = Join-Path $projectRoot 'src\Clio.Desktop\Clio.ico'
$portraitRoot = Join-Path $projectRoot 'src\Clio.Desktop\Assets\Advisers'
$portraitResources = @('sula', 'tavo', 'yara', 'lian') | ForEach-Object {
    $portraitFile = Join-Path $portraitRoot ($_ + '.png')
    if (-not (Test-Path -LiteralPath $portraitFile -PathType Leaf)) { throw "Missing adviser portrait: $portraitFile" }
    "/resource:$portraitFile,Clio.Advisers.$_.png"
}
& $compilerPath /nologo /target:winexe /optimize+ /warn:4 "/out:$desktopApp" "/win32icon:$desktopIcon" "/reference:$coreLibrary" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $portraitResources $desktopSources
if ($LASTEXITCODE -ne 0) { throw 'Desktop compilation failed.' }
Write-Output "Built $desktopApp"
if ($Test) {
    $testSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName })
    $testApp = Join-Path $outputPath 'Clio.Tests.exe'
    & $compilerPath /nologo /target:exe /optimize+ /warn:4 "/out:$testApp" "/reference:$coreLibrary" $testSources
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testApp
    if ($LASTEXITCODE -ne 0) { throw 'Simulation tests failed.' }
}
if ($Render) {
    $artifactPath = Join-Path $projectRoot 'artifacts'
    New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
    $smokeError = Join-Path $artifactPath 'smoke-error.log'
    $smokeOutput = Join-Path $artifactPath 'smoke-output.log'
    $renderRun = Start-Process -FilePath $desktopApp -ArgumentList @('--smoke', ('"' + $artifactPath + '"')) -WindowStyle Hidden -PassThru -Wait -RedirectStandardError $smokeError -RedirectStandardOutput $smokeOutput
    if ($renderRun.ExitCode -ne 0) { Get-Content -LiteralPath $smokeError; throw 'Desktop smoke checks failed.' }
    Get-Content -LiteralPath $smokeOutput
}
# Both the desktop shortcut and the project launcher follow this stable record.
# Publish only after compilation and any requested checks have completed.
$currentBuildFile = Join-Path $projectRoot 'build\current-build.txt'
$completedBuild = Split-Path -Leaf $outputPath
if (-not (Test-Path -LiteralPath $currentBuildFile) -or (Get-Content -LiteralPath $currentBuildFile -Raw).Trim() -ne $completedBuild) {
    Set-Content -LiteralPath $currentBuildFile -Value $completedBuild -Encoding Ascii
}
