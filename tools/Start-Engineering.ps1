param([switch]$NoBuild, [switch]$Open)
. "$PSScriptRoot/Environment.ps1"
if(!$NoBuild){Invoke-Checked 'npm.cmd' @('run','build'); Invoke-Checked $Dotnet @('build','backend/MailHub.slnx','--no-restore','--nologo')}
$run = Join-Path $MailHubRoot ('.local/runs/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $run | Out-Null
$env:MAILHUB_ROOT=$MailHubRoot
$env:MAILHUB_ENGINEERING='1'
$env:MAILHUB_ENGINEERING_DATA=Join-Path $MailHubRoot '.local/development'
$env:MAILHUB_STARTUP_FILE=Join-Path $run 'startup.json'
$dll=Join-Path $MailHubRoot 'backend/src/MailHub.Host/bin/Debug/net10.0/MailHub.Host.dll'
$child=Start-Process -FilePath $Dotnet -ArgumentList @('"'+$dll+'"') -WorkingDirectory $MailHubRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'stdout.log') -RedirectStandardError (Join-Path $run 'stderr.log')
@{pid=$child.Id;executable=$Dotnet;startupFile=$env:MAILHUB_STARTUP_FILE} | ConvertTo-Json | Set-Content (Join-Path $run 'process.json')
for($i=0;$i -lt 100;$i++){if(Test-Path $env:MAILHUB_STARTUP_FILE){break};if($child.HasExited){throw "Host stopped; inspect $run"};Start-Sleep -Milliseconds 200}
if(!(Test-Path $env:MAILHUB_STARTUP_FILE)){throw "Startup timeout; inspect $run"}
$state=Get-Content $env:MAILHUB_STARTUP_FILE -Raw | ConvertFrom-Json
$response=Invoke-RestMethod ($state.origin+'/_health')
if(!$response.ok){throw 'Health check failed'}
Write-Host "Started process $($child.Id). Private startup file: $env:MAILHUB_STARTUP_FILE"
Write-Host 'Open the url value in that private file within 3 minutes, then click 检查工程联通. Do not share the URL.'
if($Open){Start-Process $state.url}
