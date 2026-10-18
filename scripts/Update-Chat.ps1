[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot 'dist\FiveVibe\scripts\FiveVibe.dll'
$gameScripts = Join-Path ([IO.Path]::GetFullPath($GamePath)) 'scripts'
$target = Join-Path $gameScripts 'FiveVibe.dll'
$legacy = Join-Path $gameScripts 'GtaAi.dll'
$config = Join-Path $gameScripts 'FiveVibe.ini'
$oldConfig = Join-Path $gameScripts 'GtaAi.ini'
if (!(Test-Path -LiteralPath $source)) { throw 'Build FiveVibe first.' }
if (!(Test-Path -LiteralPath $target) -and !(Test-Path -LiteralPath $legacy)) { throw 'No existing FiveVibe or legacy GTA AI installation was found. Use Install.ps1.' }
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'ScriptHookVDotNet.asi'))) { throw 'SHVDNE runtime is missing.' }
$expectedHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $expectedHash -and !(Test-Path -LiteralPath $legacy)) { Write-Output 'FiveVibe is already up to date.'; return }
if (!$PSCmdlet.ShouldProcess($gameScripts,'Update FiveVibe with backup, preserve configuration and migrate legacy GTA AI')) { return }
foreach ($dllPath in @($target,$legacy)) {
    if (Test-Path -LiteralPath $dllPath) {
        $lockCheck = [IO.File]::Open($dllPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
        $lockCheck.Dispose()
    }
}
$backupRoot = Join-Path $projectRoot ('build\backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-fivevibe')
New-Item -ItemType Directory -Path (Join-Path $backupRoot 'scripts') -Force | Out-Null
$entries = New-Object 'System.Collections.Generic.List[object]'
$operations = @(@{ Target=$target; Source=$source; Remove=$false })
if (!(Test-Path -LiteralPath $config)) {
    $configSource = if (Test-Path -LiteralPath $oldConfig) { $oldConfig } else { Join-Path $projectRoot 'config\FiveVibe.ini' }
    $operations += @{ Target=$config; Source=$configSource; Remove=$false }
}
if (Test-Path -LiteralPath $legacy) { $operations += @{ Target=$legacy; Source=$legacy; Remove=$true } }
foreach ($operation in $operations) {
    $relative = 'scripts\' + [IO.Path]::GetFileName($operation.Target)
    $exists = Test-Path -LiteralPath $operation.Target
    if ($exists) { Copy-Item -LiteralPath $operation.Target -Destination (Join-Path $backupRoot $relative) }
    $entries.Add([pscustomobject]@{ RelativePath=$relative; HadOriginal=$exists; InstalledHash=(Get-FileHash -LiteralPath $operation.Source -Algorithm SHA256).Hash })
}
[pscustomobject]@{ GamePath=[IO.Path]::GetFullPath($GamePath); Files=@($entries.ToArray()) } | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $backupRoot 'install.json') -Encoding UTF8
$applied = New-Object 'System.Collections.Generic.List[object]'
try {
    foreach ($operation in $operations) {
        if ($operation.Remove) { Remove-Item -LiteralPath $operation.Target; $applied.Add($operation); continue }
        $staging = $operation.Target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
        try {
            Copy-Item -LiteralPath $operation.Source -Destination $staging
            if ((Get-FileHash -LiteralPath $staging -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $operation.Source -Algorithm SHA256).Hash) { throw 'Staged file hash mismatch.' }
            if (Test-Path -LiteralPath $operation.Target) { [IO.File]::Replace($staging,$operation.Target,[NullString]::Value) }
            else { [IO.File]::Move($staging,$operation.Target) }
            $applied.Add($operation)
        } finally { if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging } }
    }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Installed DLL hash mismatch.' }
} catch {
    foreach ($operation in $applied) {
        $saved = Join-Path $backupRoot ('scripts\' + [IO.Path]::GetFileName($operation.Target))
        if (Test-Path -LiteralPath $saved) { Copy-Item -LiteralPath $saved -Destination $operation.Target -Force }
        elseif (Test-Path -LiteralPath $operation.Target) { Remove-Item -LiteralPath $operation.Target }
    }
    throw
}
[pscustomobject]@{ Success=$true; BackupPath=$backupRoot; InstalledHash=$expectedHash; GamePath=[IO.Path]::GetFullPath($GamePath); FileVersion=(Get-Item -LiteralPath $source).VersionInfo.FileVersion } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'build\last-chat-update.json') -Encoding UTF8
Write-Output ('FiveVibe updated. Backup: ' + $backupRoot)
Write-Output 'Start GTA Story Mode. F7 opens chat; Open-Companion.ps1 opens the browser panel.'
