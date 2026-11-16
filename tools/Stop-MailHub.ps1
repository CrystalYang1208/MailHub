param([Parameter(Mandatory=$true)][string]$ProcessFile)
$ErrorActionPreference='Stop'
$state=Get-Content -LiteralPath $ProcessFile -Raw -Encoding UTF8 | ConvertFrom-Json
$targetId=[int]$state.pid
$process=Get-CimInstance Win32_Process -Filter "ProcessId = $targetId"
if(!$process){Write-Host 'MailHub进程已停止。';return}
if([IO.Path]::GetFullPath($process.ExecutablePath) -ne [IO.Path]::GetFullPath($state.executable) -or !$process.CommandLine.Contains($state.dll)){
 throw '进程身份不匹配，拒绝停止。请关闭原MailHub程序窗口。'
}
Stop-Process -Id $targetId
Write-Host '指定的MailHub进程已停止。'
