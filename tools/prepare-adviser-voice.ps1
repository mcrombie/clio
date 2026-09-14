$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$clioRoot = Split-Path -Parent $PSScriptRoot
$voiceWork = Join-Path $clioRoot 'build\narration'
$voiceRuntime = Join-Path $voiceWork 'runtime'
New-Item -ItemType Directory -Force -Path $voiceWork, $voiceRuntime | Out-Null

function Get-VoiceDownload([string]$Uri, [string]$Name, [string]$Sha256) {
    $destination = Join-Path $voiceWork $Name
    if ((Test-Path -LiteralPath $destination) -and ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $Sha256)) { return $destination }
    Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $Sha256) { throw "Voice download checksum differs: $Name" }
    return $destination
}

$piperArchive = Get-VoiceDownload 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip' 'piper_windows_amd64.zip' 'F3C58906402B24F3A96D92145F58ACBA6D86C9B5DB896D207F78DC80811EFCEA'
$modelBase = 'https://huggingface.co/rhasspy/piper-voices/resolve/1162a9173d0ce503555aed757976b7a9912eae4c/en/en_GB/northern_english_male/medium/'
$model = Get-VoiceDownload ($modelBase + 'en_GB-northern_english_male-medium.onnx') 'en_GB-northern_english_male-medium.onnx' '57A219AE8E638873DB7D18893304BE5069C42868F392BB95C3FF17F0690D0689'
$config = Get-VoiceDownload ($modelBase + 'en_GB-northern_english_male-medium.onnx.json') 'en_GB-northern_english_male-medium.onnx.json' '69557ED3D974463453E9B0C09DD99A7ED0E52B8B87B64B357DBEEB2540A97D47'

Expand-Archive -LiteralPath $piperArchive -DestinationPath (Join-Path $voiceWork 'engine') -Force
Copy-Item -Path (Join-Path $voiceWork 'engine\piper\*') -Destination $voiceRuntime -Recurse -Force
Copy-Item -LiteralPath $model, $config -Destination $voiceRuntime -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'voice-licenses') -Destination $voiceRuntime -Recurse -Force
Write-Host "Offline First Adviser voice prepared at $voiceRuntime"
Write-Host 'Run build.ps1 to include it in the desktop build. No audio has been played.'
