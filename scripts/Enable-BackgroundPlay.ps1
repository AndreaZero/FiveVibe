[CmdletBinding(SupportsShouldProcess)]
param([string]$SettingsPath = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Rockstar Games\GTAV Enhanced\settings.xml'))
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $SettingsPath)) { throw 'GTA Enhanced settings.xml was not found. Pass -SettingsPath explicitly.' }
if (Get-Process -Name GTA5_Enhanced -ErrorAction SilentlyContinue) { throw 'Close GTA before changing settings.xml.' }
$text = [IO.File]::ReadAllText($SettingsPath)
$pattern = '<PauseOnFocusLoss\s+value="[01]"\s*/>'
if ($text -notmatch $pattern) { throw 'PauseOnFocusLoss setting was not found; no file changed.' }
$updated = [regex]::Replace($text, $pattern, '<PauseOnFocusLoss value="0" />')
if ($text -eq $updated) { Write-Output 'Background play is already enabled.'; return }
if (!$PSCmdlet.ShouldProcess($SettingsPath, 'Disable Pause on Focus Loss, preserving every other setting and making a backup')) { return }
$backupPath = $SettingsPath + '.fivevibe-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.bak'
Copy-Item -LiteralPath $SettingsPath -Destination $backupPath
$stage = $SettingsPath + '.fivevibe.tmp'
try {
    [IO.File]::WriteAllText($stage,$updated,(New-Object Text.UTF8Encoding($false)))
    [IO.File]::Replace($stage,$SettingsPath,[NullString]::Value)
} finally { if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage } }
Write-Output ('Background play enabled. Settings backup: ' + $backupPath)
