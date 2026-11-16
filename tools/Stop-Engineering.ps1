param([Parameter(Mandatory=$true)][string]$ProcessFile)
. "$PSScriptRoot/Environment.ps1"
$resolved=(Resolve-Path -LiteralPath $ProcessFile).Path
$allowed=Join-Path $MailHubRoot '.local/runs/'
if(!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Process file outside engineering runs'}
$state=Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
$process=Get-Process -Id $state.pid -ErrorAction SilentlyContinue
if($process){
  $details=Get-CimInstance Win32_Process -Filter "ProcessId = $($state.pid)"
  if($details.ExecutablePath -ne $state.executable -or !$details.CommandLine.Contains('MailHub.Host.dll')){throw 'PID identity mismatch; not stopping'}
  Stop-Process -Id $state.pid
}
