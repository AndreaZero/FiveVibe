[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$vendorRoot = Join-Path $projectRoot 'vendor'
New-Item -ItemType Directory -Path $vendorRoot -Force | Out-Null
$archive = Join-Path $vendorRoot 'ScriptHookVDotNetEnhanced-v1.1.0.6.zip'
$url = 'https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases/download/v1.1.0.6/ScriptHookVDotNetEnhanced-v1.1.0.6.zip'
$expected = 'f10da8819fa6814fb0a04553567d2516934eac96b16af06ad8f315f0da00473a'
if (!(Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $url -OutFile $archive -TimeoutSec 45 }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Unexpected SDK hash. Download rejected.' }
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $vendorRoot 'shvdne') -Force
Write-Output 'SHVDNE v1.1.0.6 SDK downloaded and verified. No game files changed.'
