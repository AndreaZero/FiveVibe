[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$tracked = @(git -C $projectRoot ls-files)
if ($LASTEXITCODE -ne 0 -or !$tracked.Count) { throw 'Stage the intended public files first.' }
$problems = New-Object 'System.Collections.Generic.List[string]'
foreach ($relative in $tracked) {
    if ($relative -match '(^|/)(vendor|build|dist)/|(^|/)\.env($|\.(?!example$))|\.local\.(ini|json)$|compatibility.*\.json$|\.(dll|exe|asi|pdb|zip|log)$') { $problems.Add($relative + ': private or generated path'); continue }
    $file = Join-Path $projectRoot $relative
    if (!(Test-Path -LiteralPath $file)) { continue }
    $text = [IO.File]::ReadAllText($file)
    if ($text -match 'sk-(?:proj-)?[A-Za-z0-9_-]{30,}' -or $text -match 'gh[opsu]_[A-Za-z0-9]{30,}') { $problems.Add($relative + ': credential-like value') }
    if ($text -match '(?i)C:[\\/]+Users[\\/]+(?!Public[\\/])' -or $text -match '@live\.it') { $problems.Add($relative + ': personal local details') }
    if ($relative -eq '.env.example' -and $text -match '(?m)^OPENAI_API_KEY=.+$') { $problems.Add($relative + ': API key example must be empty') }
}
if ($problems.Count) { throw ('Public-file review failed: ' + ($problems -join '; ')) }
Write-Output ('Public-file review passed for ' + $tracked.Count + ' tracked files. No credentials or local artifacts detected.')
