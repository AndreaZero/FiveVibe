[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$discoveryPath = Join-Path $env:LOCALAPPDATA 'FiveVibe\companion.json'
if (!(Test-Path -LiteralPath $discoveryPath)) { throw 'Start GTA Story Mode with FiveVibe loaded, then run this script again.' }
$session = Get-Content -LiteralPath $discoveryPath -Raw | ConvertFrom-Json
if ($session.url -notmatch '^http://127\.0\.0\.1:\d{4,5}/#[a-f0-9]{64}$') { throw 'Invalid companion session URL.' }
if (!(Get-Process -Id $session.processId -ErrorAction SilentlyContinue)) { throw 'The previous GTA session has ended. Start GTA first.' }
Start-Process $session.url
Write-Output 'FiveVibe companion opened in your default browser.'
