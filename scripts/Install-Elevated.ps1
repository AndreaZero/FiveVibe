[CmdletBinding()]
param([string]$GamePath,[switch]$UpdateOnly,[switch]$EnableOpenAi,[switch]$UseVerifiedInstalledHook)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$GamePath) {
    $localRecord = Join-Path $projectRoot 'compatibility.local.json'
    if (Test-Path -LiteralPath $localRecord) { $GamePath = (Get-Content -LiteralPath $localRecord -Raw | ConvertFrom-Json).gamePath }
    if (!$GamePath) { throw 'Pass -GamePath with your GTA Enhanced installation directory.' }
}
$logName = if ($EnableOpenAi) { 'openai-setup-elevated' } elseif ($UpdateOnly) { 'update-elevated' } else { 'install-elevated' }
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'build') -Force | Out-Null
$logPath = Join-Path $projectRoot ('build\' + $logName + '.log')
$resultPath = Join-Path $projectRoot ('build\' + $logName + '-result.json')
try {
    & {
        if ($UpdateOnly) { & (Join-Path $PSScriptRoot 'Update-Chat.ps1') -GamePath $GamePath }
        else { & (Join-Path $PSScriptRoot 'Install.ps1') -GamePath $GamePath -UseVerifiedInstalledHook:$UseVerifiedInstalledHook }
        if ($EnableOpenAi) { & (Join-Path $PSScriptRoot 'Configure-OpenAI.ps1') -GamePath $GamePath }
    } *>&1 | Out-File -LiteralPath $logPath -Encoding UTF8
    [pscustomobject]@{ Success=$true; CompletedAt=(Get-Date).ToString('o'); LogPath=$logPath } | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
    exit 0
} catch {
    $_ | Out-String | Add-Content -LiteralPath $logPath -Encoding UTF8
    [pscustomobject]@{ Success=$false; Error=$_.Exception.Message; CompletedAt=(Get-Date).ToString('o'); LogPath=$logPath } | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
    exit 1
}
