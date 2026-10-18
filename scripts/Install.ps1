[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$GamePath,
    [switch]$UseVerifiedInstalledHook
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$exe = Get-Item -LiteralPath (Join-Path $GamePath 'GTA5_Enhanced.exe')
if (Get-Process -Name 'GTA5_Enhanced' -ErrorAction SilentlyContinue) { throw 'Close GTA before installing.' }

$officialOrigin = 'https://www.dev-c.enbdev.com'
if ($UseVerifiedInstalledHook) {
    # Accept the user's successful Story Mode test for exactly these existing files.
    # This does not bypass checks for another game update or a different hook download.
    $compatibility = Get-Content -LiteralPath (Join-Path $projectRoot 'compatibility.local.json') -Raw | ConvertFrom-Json
    $verified = $compatibility.scriptHookRuntimeVerification
    if (!$verified -or !$verified.runtimeLoadingVerified -or $verified.gameVersion -ne $exe.VersionInfo.FileVersion) {
        throw 'No local Story Mode verification exists for this game version.'
    }
    foreach ($item in @(
        @{ Name='ScriptHookV.dll'; Hash=$verified.scriptHookSha256 },
        @{ Name='dinput8.dll'; Hash=$verified.asiLoaderSha256 }
    )) {
        $installed = Join-Path $GamePath $item.Name
        if (!(Test-Path -LiteralPath $installed) -or (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne $item.Hash) {
            throw ('The verified file changed: ' + $item.Name + '. Verify Script Hook in Story Mode again first.')
        }
    }
    Write-Output ('Script Hook previously verified locally on GTA ' + $verified.gameVersion + ': keeping the installed runtime.')
} else {
    # Fresh installs still require the author's declared game versions.
    $page = (Invoke-WebRequest -Uri ($officialOrigin + '/gtav/scripthookv/') -TimeoutSec 20).Content
    $supportedBlock = [regex]::Match($page, '(?is)Supported patches\s*</th>\s*<td>(.*?)</td>')
    if (!$supportedBlock.Success) { throw 'Cannot read supported versions from the upstream website. Installation stopped.' }
    $supportedVersions = @([regex]::Matches($supportedBlock.Groups[1].Value, '\b1\.0\.\d+\.\d+\b') | ForEach-Object { $_.Value })
    if ($exe.VersionInfo.FileVersion -notin $supportedVersions) {
        throw ('Installation stopped: GTA ' + $exe.VersionInfo.FileVersion + '; the author lists ' + ($supportedVersions -join ', ') + '. No game files changed.')
    }
}
if (!(Test-Path -LiteralPath (Join-Path $projectRoot 'dist\FiveVibe\scripts\FiveVibe.dll'))) { throw 'Run scripts\Build.ps1 first.' }
if (!(Test-Path -LiteralPath (Join-Path $projectRoot 'vendor\shvdne\ScriptHookVDotNet3.dll'))) { throw 'SHVDNE SDK is missing.' }
if (!$PSCmdlet.ShouldProcess($GamePath, 'Install FiveVibe and SHVDNE with backups')) { return }

$files = @{}
if (!$UseVerifiedInstalledHook) {
    $downloadMatch = [regex]::Match($page, 'href="(/files/ScriptHookV_[\d._]+\.zip)"')
    if (!$downloadMatch.Success) { throw 'Script Hook V download was not found on the upstream website.' }
    $archive = Join-Path $projectRoot 'vendor\ScriptHookV.zip'
    Invoke-WebRequest -Uri ($officialOrigin + $downloadMatch.Groups[1].Value) -OutFile $archive -TimeoutSec 45
    $hookDirectory = Join-Path $projectRoot 'vendor\shv'
    Expand-Archive -LiteralPath $archive -DestinationPath $hookDirectory -Force
    foreach ($file in @('ScriptHookV.dll','dinput8.dll')) { $files[$file] = Join-Path $hookDirectory ('bin\' + $file) }
}
foreach ($file in @('ScriptHookVDotNet.asi','ScriptHookVDotNet2.dll','ScriptHookVDotNet3.dll','MinHook.x64.dll')) {
    $files[$file] = Join-Path $projectRoot ('vendor\shvdne\' + $file)
}
# Preserve any existing runtime configuration. A new install uses F6 so F4 can
# remain assigned to the user's Native Trainer.
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'ScriptHookVDotNet.ini'))) {
    $runtimeIni = Join-Path $projectRoot 'build\ScriptHookVDotNet.ini'
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $projectRoot 'vendor\shvdne\ScriptHookVDotNet.ini') -Raw
    [IO.File]::WriteAllText($runtimeIni, ($runtimeConfig -replace '(?m)^ConsoleKeyBinding=F4', 'ConsoleKeyBinding=F6'))
    $files['ScriptHookVDotNet.ini'] = $runtimeIni
}
$files['scripts\FiveVibe.dll'] = Join-Path $projectRoot 'dist\FiveVibe\scripts\FiveVibe.dll'
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'scripts\FiveVibe.ini'))) {
    $files['scripts\FiveVibe.ini'] = Join-Path $projectRoot 'dist\FiveVibe\scripts\FiveVibe.ini'
}
foreach ($source in $files.Values) { if (!(Test-Path -LiteralPath $source)) { throw ('Missing file: ' + $source) } }
$backupRoot = Join-Path $projectRoot ('build\backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$entries = @()
try {
    foreach ($relative in $files.Keys) {
        $destination = Join-Path $GamePath $relative
        $exists = Test-Path -LiteralPath $destination
        if ($exists) {
            $backup = Join-Path $backupRoot $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $backup
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        $entry = [pscustomobject]@{ RelativePath = $relative; HadOriginal = $exists; InstalledHash = (Get-FileHash -LiteralPath $files[$relative] -Algorithm SHA256).Hash }
        $entries += $entry
        # Persist rollback metadata before each mutation, including partial installs.
        [pscustomobject]@{ GamePath=$GamePath; Files=$entries } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backupRoot 'install.json') -Encoding UTF8
        Copy-Item -LiteralPath $files[$relative] -Destination $destination -Force
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.InstalledHash) {
            throw ('Copied file hash mismatch: ' + $relative)
        }
    }
} catch {
    Write-Warning ('Incomplete installation. To restore: scripts\Uninstall.ps1 -BackupPath "' + $backupRoot + '"')
    throw
}
Write-Output ('Installation completed. Backup: ' + $backupRoot)
Write-Output 'Start Story Mode with BattlEye disabled through the official Rockstar Launcher setting. F7 chat, F8 take over.'
