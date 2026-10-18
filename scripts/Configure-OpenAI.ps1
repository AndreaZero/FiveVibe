[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$GamePath,
    [string]$EnvFilePath=''
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(!$EnvFilePath) { $EnvFilePath=Join-Path $projectRoot '.env' }
$EnvFilePath=[IO.Path]::GetFullPath($EnvFilePath)
if(!(Test-Path -LiteralPath $EnvFilePath)) { throw 'Env file not found. Create it before configuring FiveVibe.' }
if($EnvFilePath.IndexOfAny([char[]]@([char]10,[char]13)) -ge 0) { throw 'Invalid env path.' }
$target=Join-Path $GamePath 'scripts\FiveVibe.ini'
if(!(Test-Path -LiteralPath $target)) { throw 'Installed FiveVibe configuration not found.' }

function Set-AiIniValue([string]$Text,[string]$Name,[string]$Value) {
    $lines=New-Object 'System.Collections.Generic.List[string]'
    $lines.AddRange([string[]]($Text -split '\r?\n'))
    $start=-1
    for($i=0;$i -lt $lines.Count;$i++) {
        if($lines[$i] -match '^\s*\[AI\]\s*$') { $start=$i; break }
    }
    if($start -lt 0) { $lines.Add('[AI]'); $lines.Add($Name+'='+$Value) }
    else {
        $end=$lines.Count
        for($i=$start+1;$i -lt $lines.Count;$i++) { if($lines[$i] -match '^\s*\[') { $end=$i; break } }
        $found=$false
        for($i=$start+1;$i -lt $end;$i++) {
            if($lines[$i] -match ('^\s*'+[regex]::Escape($Name)+'\s*=')) { $lines[$i]=$Name+'='+$Value; $found=$true }
        }
        if(!$found) { $lines.Insert($end,$Name+'='+$Value) }
    }
    return ($lines -join [Environment]::NewLine)
}

$content=Get-Content -LiteralPath $target -Raw
$content=Set-AiIniValue $content 'Provider' 'OpenAI'
$content=Set-AiIniValue $content 'EnvFile' $EnvFilePath
if(!$PSCmdlet.ShouldProcess($target,'Enable OpenAI using the local env file without copying its key')) { return }
$backupRoot=Join-Path $projectRoot ('build\backups\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-openai')
New-Item -ItemType Directory -Path (Join-Path $backupRoot 'scripts') -Force | Out-Null
Copy-Item -LiteralPath $target -Destination (Join-Path $backupRoot 'scripts\FiveVibe.ini')
$staging=Join-Path (Split-Path -Parent $target) ('FiveVibe-config-'+[Guid]::NewGuid().ToString('N')+'.tmp')
try {
    [IO.File]::WriteAllText($staging,$content,(New-Object Text.UTF8Encoding($false)))
    $expectedHash=(Get-FileHash -LiteralPath $staging -Algorithm SHA256).Hash
    [pscustomobject]@{GamePath=$GamePath;Files=@([pscustomobject]@{RelativePath='scripts\FiveVibe.ini';HadOriginal=$true;InstalledHash=$expectedHash})} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backupRoot 'install.json') -Encoding UTF8
    [IO.File]::Replace($staging,$target,[NullString]::Value)
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Installed configuration hash mismatch.' }
} finally {
    if(Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging }
}
[pscustomobject]@{Success=$true;EnvFile=$EnvFilePath;Provider='OpenAI';BackupPath=$backupRoot;ConfigHash=$expectedHash} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'build\openai-configuration.json') -Encoding UTF8
Write-Output ('OpenAI enabled. Key stays in the local env file. Configuration backup: '+$backupRoot)
