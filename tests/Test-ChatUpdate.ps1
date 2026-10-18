$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $projectRoot ('build\update-check-' + [Guid]::NewGuid().ToString('N'))
$statePath = Join-Path $projectRoot 'build\last-chat-update.json'
$aiStatePath = Join-Path $projectRoot 'build\openai-configuration.json'
$previousState = if (Test-Path -LiteralPath $statePath) { [IO.File]::ReadAllText($statePath) } else { $null }
$previousAiState = if (Test-Path -LiteralPath $aiStatePath) { [IO.File]::ReadAllText($aiStatePath) } else { $null }
$expectedHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'dist\FiveVibe\scripts\FiveVibe.dll') -Algorithm SHA256).Hash
$nl = [Environment]::NewLine
try {
    $modern = Join-Path $fixture 'modern'
    New-Item -ItemType Directory -Path (Join-Path $modern 'scripts') -Force | Out-Null
    $dll = Join-Path $modern 'scripts\FiveVibe.dll'
    $config = Join-Path $modern 'scripts\FiveVibe.ini'
    [IO.File]::WriteAllText($dll,'previous-dll-fixture')
    [IO.File]::WriteAllText($config,'user-config-to-preserve')
    [IO.File]::WriteAllText((Join-Path $modern 'ScriptHookVDotNet.asi'),'runtime-fixture')
    $originalHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    & (Join-Path $projectRoot 'scripts\Update-Chat.ps1') -GamePath $modern -WhatIf
    if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $originalHash) { throw 'WhatIf changed the installation.' }
    & (Join-Path $projectRoot 'scripts\Update-Chat.ps1') -GamePath $modern
    if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $expectedHash -or [IO.File]::ReadAllText($config) -ne 'user-config-to-preserve') { throw 'DLL update or configuration preservation failed.' }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath (Join-Path $state.BackupPath 'scripts\FiveVibe.dll') -Algorithm SHA256).Hash -ne $originalHash) { throw 'Backup hash mismatch.' }
    $envFixture = Join-Path $modern 'configuration.env'
    [IO.File]::WriteAllText($envFixture,'OPENAI_API_KEY=test-fixture-only')
    [IO.File]::WriteAllText($config,('[Chat]'+$nl+'OpenKey=F7'+$nl+'[AI]'+$nl+'Provider=Local'+$nl+'Model=gpt-4.1-mini'+$nl+'[Driving]'+$nl+'CruiseSpeedKmh=45'))
    & (Join-Path $projectRoot 'scripts\Configure-OpenAI.ps1') -GamePath $modern -EnvFilePath $envFixture
    $configured = [IO.File]::ReadAllText($config)
    if ($configured -notmatch 'Provider=OpenAI' -or $configured -notmatch 'OpenKey=F7' -or $configured -notmatch 'CruiseSpeedKmh=45' -or !$configured.Contains('EnvFile='+$envFixture) -or $configured.Contains('test-fixture-only')) { throw 'OpenAI configuration leaked a key or lost settings.' }
    $legacy = Join-Path $fixture 'legacy'
    New-Item -ItemType Directory -Path (Join-Path $legacy 'scripts') -Force | Out-Null
    $legacyDll = Join-Path $legacy 'scripts\GtaAi.dll'
    $legacyIni = Join-Path $legacy 'scripts\GtaAi.ini'
    [IO.File]::WriteAllText($legacyDll,'legacy-dll-fixture')
    [IO.File]::WriteAllText($legacyIni,('[AI]'+$nl+'Provider=OpenAI'+$nl+'EnvFile=private.env'))
    [IO.File]::WriteAllText((Join-Path $legacy 'ScriptHookVDotNet.asi'),'runtime-fixture')
    $locked = [IO.File]::Open($legacyDll,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try {
        $rejected=$false
        try { & (Join-Path $projectRoot 'scripts\Update-Chat.ps1') -GamePath $legacy } catch { $rejected=$true }
        if (!$rejected -or (Test-Path -LiteralPath (Join-Path $legacy 'scripts\FiveVibe.dll'))) { throw 'Locked legacy DLL did not block migration safely.' }
    } finally { $locked.Dispose() }
    & (Join-Path $projectRoot 'scripts\Update-Chat.ps1') -GamePath $legacy
    if ((Test-Path -LiteralPath $legacyDll) -or (Get-FileHash -LiteralPath (Join-Path $legacy 'scripts\FiveVibe.dll') -Algorithm SHA256).Hash -ne $expectedHash -or
        [IO.File]::ReadAllText((Join-Path $legacy 'scripts\FiveVibe.ini')) -ne [IO.File]::ReadAllText($legacyIni)) { throw 'Legacy migration failed or changed configuration.' }
    $migration = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    & (Join-Path $projectRoot 'scripts\Uninstall.ps1') -BackupPath $migration.BackupPath
    if ([IO.File]::ReadAllText($legacyDll) -ne 'legacy-dll-fixture' -or (Test-Path -LiteralPath (Join-Path $legacy 'scripts\FiveVibe.dll'))) { throw 'Migration rollback failed.' }
    $settings = Join-Path $fixture 'settings.xml'
    $originalSettings = '<Settings><Windowed value="1" /><PauseOnFocusLoss value="1" /><Other value="42" /></Settings>'
    [IO.File]::WriteAllText($settings,$originalSettings)
    & (Join-Path $projectRoot 'scripts\Enable-BackgroundPlay.ps1') -SettingsPath $settings -WhatIf
    if ([IO.File]::ReadAllText($settings) -ne $originalSettings) { throw 'Background WhatIf changed settings.' }
    & (Join-Path $projectRoot 'scripts\Enable-BackgroundPlay.ps1') -SettingsPath $settings
    if ([IO.File]::ReadAllText($settings) -ne $originalSettings.Replace('PauseOnFocusLoss value="1"','PauseOnFocusLoss value="0"')) { throw 'Background helper changed unrelated settings.' }
    Write-Output 'Update, backup, WhatIf, locked-file rejection, env reference, legacy migration, rollback and background settings checks: OK.'
} finally {
    if ($null -ne $previousState) { [IO.File]::WriteAllText($statePath,$previousState) }
    elseif (Test-Path -LiteralPath $statePath) { Remove-Item -LiteralPath $statePath }
    if ($null -ne $previousAiState) { [IO.File]::WriteAllText($aiStatePath,$previousAiState) }
    elseif (Test-Path -LiteralPath $aiStatePath) { Remove-Item -LiteralPath $aiStatePath }
}
