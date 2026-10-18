[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$BackupPath)
$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath (Join-Path $BackupPath 'install.json') -Raw | ConvertFrom-Json
$gameRoot = [IO.Path]::GetFullPath($manifest.GamePath).TrimEnd('\') + '\'
if (Get-Process -Name 'GTA5_Enhanced' -ErrorAction SilentlyContinue) { throw 'Close GTA before restoring files.' }
foreach ($entry in $manifest.Files) {
    $target = [IO.Path]::GetFullPath((Join-Path $gameRoot $entry.RelativePath))
    if (!$target.StartsWith($gameRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid restore path.' }
    if (Test-Path -LiteralPath $target) {
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.InstalledHash) {
            Write-Warning ('File changed since installation, keeping it: ' + $target)
            continue
        }
    }
    if ($PSCmdlet.ShouldProcess($target, 'Restore the previous file or remove the file installed by the mod')) {
        if ($entry.HadOriginal) { Copy-Item -LiteralPath (Join-Path $BackupPath $entry.RelativePath) -Destination $target -Force }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
}
Write-Output 'Restore completed. Later modifications and private credentials are preserved.'
