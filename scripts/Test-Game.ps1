[CmdletBinding()]
param([Parameter(Mandatory)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$localManifest = Join-Path (Split-Path -Parent $PSScriptRoot) 'compatibility.local.json'
$manifest = if (Test-Path -LiteralPath $localManifest) { Get-Content -LiteralPath $localManifest -Raw | ConvertFrom-Json } else { $null }
$exe = Get-Item -LiteralPath (Join-Path $GamePath 'GTA5_Enhanced.exe')
$installed = $exe.VersionInfo.FileVersion
$verified = $manifest.scriptHookRuntimeVerification
$hookMatches = $false
if ($verified -and $verified.runtimeLoadingVerified -and $verified.gameVersion -eq $installed -and
    (Test-Path -LiteralPath (Join-Path $GamePath 'ScriptHookV.dll')) -and
    (Test-Path -LiteralPath (Join-Path $GamePath 'dinput8.dll'))) {
    $hookMatches = (Get-FileHash -LiteralPath (Join-Path $GamePath 'ScriptHookV.dll') -Algorithm SHA256).Hash -eq $verified.scriptHookSha256 -and
        (Get-FileHash -LiteralPath (Join-Path $GamePath 'dinput8.dll') -Algorithm SHA256).Hash -eq $verified.asiLoaderSha256
}
[pscustomobject]@{
    GamePath = $GamePath
    InstalledVersion = $installed
    ScriptHookDeclaredVersion = $manifest.scriptHookDeclaredEnhancedVersion
    DeclaredVersionMatches = $installed -eq $manifest.scriptHookDeclaredEnhancedVersion
    ScriptHookVerifiedByUser = $hookMatches
    ScriptHookInstalled = Test-Path -LiteralPath (Join-Path $GamePath 'ScriptHookV.dll')
    AsiLoaderInstalled = Test-Path -LiteralPath (Join-Path $GamePath 'dinput8.dll')
    DotNetInstalled = Test-Path -LiteralPath (Join-Path $GamePath 'ScriptHookVDotNet.asi')
    FiveVibeInstalled = Test-Path -LiteralPath (Join-Path $GamePath 'scripts\FiveVibe.dll')
    InGameTested = $false
} | ConvertTo-Json
