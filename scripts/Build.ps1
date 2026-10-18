[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$apiDll = Join-Path $projectRoot 'vendor\shvdne\ScriptHookVDotNet3.dll'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler is required.' }
if (!(Test-Path -LiteralPath $apiDll)) { throw 'Run scripts\Get-Dependencies.ps1 first to fetch the upstream SHVDNE SDK.' }
if (!(Test-Path -LiteralPath $framework)) { $framework = Split-Path -Parent $compiler }
$buildRoot = Join-Path $projectRoot 'build'
$distRoot = Join-Path $projectRoot 'dist\FiveVibe'
New-Item -ItemType Directory -Path $buildRoot,(Join-Path $distRoot 'scripts') -Force | Out-Null
$references = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll','System.Web.Extensions.dll')
$referenceArgs = @($references | ForEach-Object { '/reference:' + (Join-Path $framework $_) })
$commonArgs = @('/nologo','/noconfig','/nostdlib+','/langversion:5','/optimize+','/warn:4') + $referenceArgs
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$pageResource = '/resource:' + (Join-Path $projectRoot 'companion\index.html') + ',FiveVibe.Companion.html'
& $compiler @commonArgs '/target:library' ('/out:' + (Join-Path $distRoot 'scripts\FiveVibe.dll')) ('/reference:' + $apiDll) $pageResource @sources
if ($LASTEXITCODE -ne 0) { throw 'Mod compilation failed.' }
$testSources = @('src\VehicleSelection.cs','src\RealWorldService.cs','src\CameraDirectorLogic.cs','src\CharacterSwitchMonitor.cs','src\SessionSettings.cs','src\PlaceCatalog.cs','src\CommandPlan.cs','src\OpenAiPlanner.cs','src\AiConfiguration.cs','src\HudLayout.cs','tests\PlannerTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @commonArgs '/target:exe' ('/out:' + (Join-Path $buildRoot 'PlannerTests.exe')) @testSources
if ($LASTEXITCODE -ne 0) { throw 'Check compilation failed.' }
& (Join-Path $buildRoot 'PlannerTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
$missionSources = @('src\VehicleSelection.cs','src\RealWorldService.cs','src\CameraDirectorLogic.cs','src\MissionFlow.cs','src\SessionSettings.cs','src\PlaceCatalog.cs','src\CommandPlan.cs','tests\MissionTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @commonArgs '/target:exe' ('/out:' + (Join-Path $buildRoot 'MissionTests.exe')) @missionSources
if ($LASTEXITCODE -ne 0) { throw 'Mission checks failed to compile.' }
& (Join-Path $buildRoot 'MissionTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Mission checks failed.' }
$cameraSources = @('src\VehicleSelection.cs','src\RealWorldService.cs','src\CameraDirectorLogic.cs','src\SessionSettings.cs','src\PlaceCatalog.cs','src\CommandPlan.cs','tests\CameraTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @commonArgs '/target:exe' ('/out:' + (Join-Path $buildRoot 'CameraTests.exe')) @cameraSources
if ($LASTEXITCODE -ne 0) { throw 'Camera checks failed to compile.' }
& (Join-Path $buildRoot 'CameraTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Camera checks failed.' }
$ambientSources = @('src\VehicleSelection.cs','src\RealWorldService.cs','src\CameraDirectorLogic.cs','src\SessionSettings.cs','src\PlaceCatalog.cs','src\CommandPlan.cs','tests\AmbientTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @commonArgs '/target:exe' ('/out:' + (Join-Path $buildRoot 'AmbientTests.exe')) @ambientSources
if ($LASTEXITCODE -ne 0) { throw 'Ambient checks failed to compile.' }
& (Join-Path $buildRoot 'AmbientTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Ambient checks failed.' }
$weatherProbeSources = @('src\VehicleSelection.cs','src\RealWorldService.cs','src\CameraDirectorLogic.cs','src\SessionSettings.cs','src\PlaceCatalog.cs','src\CommandPlan.cs','tests\RealWeatherCheck.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @commonArgs '/target:exe' ('/out:' + (Join-Path $buildRoot 'RealWeatherCheck.exe')) @weatherProbeSources
if ($LASTEXITCODE -ne 0) { throw 'Weather reference check failed to compile.' }
$probeSources = @('src\VehicleSelection.cs','src\RealWorldService.cs','src\CameraDirectorLogic.cs','src\SessionSettings.cs','src\PlaceCatalog.cs','src\CommandPlan.cs','src\OpenAiPlanner.cs','src\AiConfiguration.cs','tests\AiConnectionCheck.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @commonArgs '/target:exe' ('/out:' + (Join-Path $buildRoot 'AiConnectionCheck.exe')) @probeSources
if ($LASTEXITCODE -ne 0) { throw 'AI connection check compilation failed.' }
$companionSources = @('src\CompanionServer.cs','tests\CompanionTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @commonArgs '/target:exe' ('/out:' + (Join-Path $buildRoot 'CompanionTests.exe')) $pageResource @companionSources
if ($LASTEXITCODE -ne 0) { throw 'Companion checks failed to compile.' }
& (Join-Path $buildRoot 'CompanionTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Companion checks failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\FiveVibe.ini') -Destination (Join-Path $distRoot 'scripts\FiveVibe.ini')
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\FiveVibe.locations.example.json') -Destination (Join-Path $distRoot 'scripts\FiveVibe.locations.example.json')
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Open-Companion.ps1') -Destination $distRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $distRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md'),(Join-Path $projectRoot 'CONTRIBUTING.md') -Destination $distRoot
New-Item -ItemType Directory -Path (Join-Path $distRoot 'docs') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -Filter '*.md' | Copy-Item -Destination (Join-Path $distRoot 'docs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $distRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'SECURITY.md') -Destination $distRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Enable-BackgroundPlay.ps1') -Destination $distRoot
Get-FileHash -LiteralPath (Join-Path $distRoot 'scripts\FiveVibe.dll') -Algorithm SHA256 | Format-List
Write-Output ('Built FiveVibe: ' + $distRoot)
Write-Output 'Compilation does not confirm compatibility or native in-game behavior.'
