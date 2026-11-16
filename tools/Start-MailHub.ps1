param([switch]$Demo,[switch]$NoBuild,[switch]$Open)
. "$PSScriptRoot/Environment.ps1"
if([Environment]::OSVersion.Version.Build -lt 22000){throw '正式交付运行环境为Windows11'}
$env:MAILHUB_ENGINEERING=''
if(!$NoBuild){
 if(!(Test-Path 'node_modules')){Invoke-Checked 'npm.cmd' @('ci','--ignore-scripts')}
 Invoke-Checked $Dotnet @('restore','backend/MailHub.slnx','--locked-mode','--nologo')
 Invoke-Checked 'npm.cmd' @('run','build')
 Invoke-Checked $Dotnet @('build','backend/MailHub.slnx','--no-restore','--nologo')
}
$run=Join-Path $MailHubRoot ('.local/runs/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $run | Out-Null
$env:MAILHUB_ROOT=$MailHubRoot
$mode=if($Demo){'demo'}else{'mailhub'}
$env:MAILHUB_DATA=Join-Path $MailHubRoot ('.local/'+$mode)
$env:MAILHUB_STARTUP_FILE=Join-Path $run 'startup.json'
$dll=Join-Path $MailHubRoot 'backend/src/MailHub.Host/bin/Debug/net10.0/MailHub.Host.dll'
$arguments=@('"'+$dll+'"','--no-browser');if($Demo){$arguments+='--demo'}
$child=Start-Process -FilePath $Dotnet -ArgumentList $arguments -WorkingDirectory $MailHubRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'stdout.log') -RedirectStandardError (Join-Path $run 'stderr.log')
for($i=0;$i -lt 150;$i++){
 if(Test-Path $env:MAILHUB_STARTUP_FILE){break}
 if($child.HasExited){if($child.ExitCode -eq 0){Write-Host '已向现有MailHub实例发送打开请求。';return};throw "程序启动失败，查看本机 $run"}
 Start-Sleep -Milliseconds 200
}
if(!(Test-Path $env:MAILHUB_STARTUP_FILE)){throw "启动超时，查看本机 $run"}
@{pid=$child.Id;executable=$Dotnet;dll=$dll;startupFile=$env:MAILHUB_STARTUP_FILE} | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $run 'process.json')
$meta=Get-Content -Raw -Encoding UTF8 $env:MAILHUB_STARTUP_FILE | ConvertFrom-Json
Write-Host "MailHub已启动，进程$($child.Id)。停止用：tools/Stop-MailHub.ps1 -ProcessFile `"$run/process.json`""
if($Open){Start-Process $meta.url}else{Write-Host '一次性启动链接仅保存在本机startup.json，请勿分享。'}
