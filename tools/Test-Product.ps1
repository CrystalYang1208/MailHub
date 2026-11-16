param([ValidateSet('all','chrome','edge')][string]$Browser='all',[switch]$SkipBrowser)
. "$PSScriptRoot/Environment.ps1"
if(!(Test-Path node_modules)){Invoke-Checked 'npm.cmd' @('ci','--ignore-scripts')}
Invoke-Checked $Dotnet @('restore','backend/MailHub.slnx','--locked-mode','--nologo')
Invoke-Checked $Dotnet @('format','backend/MailHub.slnx','--verify-no-changes','--no-restore')
foreach($task in @('typecheck','lint','format:check','test','build','contracts:lint','contracts:check','specs:check')){Invoke-Checked 'npm.cmd' @('run',$task)}
Invoke-Checked $Dotnet @('test','backend/MailHub.slnx','--no-restore','--nologo','--logger','trx;LogFileName=product.trx','--results-directory','.local/product-tests')
if(!$SkipBrowser){$arguments=@('run','test:e2e');if($Browser -ne 'all'){$arguments+=@('--',"--project=$Browser")};Invoke-Checked 'npm.cmd' $arguments}
else{Write-Host '已跳过浏览器测试；这不代表浏览器验收通过。'}
