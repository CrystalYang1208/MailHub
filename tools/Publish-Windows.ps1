param([string]$OutputDirectory='artifacts/MailHub-Win11-x64')
. "$PSScriptRoot/Environment.ps1"
if(!(Test-Path node_modules)){Invoke-Checked 'npm.cmd' @('ci','--ignore-scripts')}
Invoke-Checked 'npm.cmd' @('run','build')
$destination=[IO.Path]::GetFullPath((Join-Path $MailHubRoot $OutputDirectory))
Invoke-Checked $Dotnet @('publish','backend/src/MailHub.Host/MailHub.Host.csproj','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:DebugType=None','-p:RestoreLockedMode=false','-o',$destination)
Copy-Item '运行演示.cmd','运行正式版.cmd' -Destination $destination -Force
Copy-Item 'licenses' -Destination $destination -Recurse -Force
Copy-Item 'docs/course' -Destination (Join-Path $destination '说明文档') -Recurse -Force
Write-Host "Win11运行目录已生成：$destination；请在Win11本机实际验收。"
