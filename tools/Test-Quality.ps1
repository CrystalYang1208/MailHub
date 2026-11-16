param([switch]$Mirror, [switch]$SkipInstall, [switch]$BuildOnlyRestore, [switch]$SkipBrowser)
. "$PSScriptRoot/Environment.ps1"
$folder = Join-Path $MailHubRoot '.local/stage4'
New-Item -ItemType Directory -Force $folder | Out-Null
Start-Transcript -Path (Join-Path $folder 'quality-transcript.txt') -Force | Out-Null
try {
  if (!$SkipInstall) { Invoke-Checked 'npm.cmd' @('ci','--ignore-scripts') }
  foreach($step in @('typecheck','lint','format:check','test','build','contracts:lint','contracts:check','specs:check')) { Invoke-Checked 'npm.cmd' @('run',$step) }
  $source = if($Mirror){'backend/NuGet.Mirror.Config'}else{'backend/NuGet.Config'}
  $restore = @('restore','backend/MailHub.slnx','--configfile',$source,'--locked-mode','--nologo')
  if($BuildOnlyRestore){$restore += '-p:NuGetAudit=false'; Write-Warning 'Build-only restore: NuGet vulnerability audit remains BLOCKED; not an audit PASS.'}
  $restoreLog = Join-Path $folder 'quality-restore.log'
  # Windows PowerShell 5.1 maps native stderr (including first-use notices) to
  # ErrorRecord. Check the native exit code instead of treating every notice as failure.
  $previousPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    & $Dotnet @restore 2>&1 | Tee-Object -FilePath $restoreLog
    $restoreExit = $LASTEXITCODE
  } finally { $ErrorActionPreference = $previousPreference }
  if($restoreExit -ne 0){throw 'NuGet restore failed; see quality-restore.log'}
  if(!$BuildOnlyRestore -and (Select-String -LiteralPath $restoreLog -Pattern 'NU1900|NU1905' -Quiet)){throw 'NuGet audit BLOCKED: security data unavailable. Review evidence; explicit -BuildOnlyRestore is for build verification only.'}
  Invoke-Checked $Dotnet @('format','whitespace','backend/MailHub.slnx','--verify-no-changes','--no-restore')
  Invoke-Checked $Dotnet @('build','backend/MailHub.slnx','--no-restore','--nologo')
  Invoke-Checked $Dotnet @('test','backend/MailHub.slnx','--no-build','--logger','trx;LogFileName=foundation.trx','--results-directory','.local/stage4/tests')
  if(!$SkipBrowser){Invoke-Checked 'npm.cmd' @('run','test:e2e')}
  & npm.cmd audit --json | Out-File (Join-Path $folder 'npm-audit.json') -Encoding utf8
  $audit = Get-Content (Join-Path $folder 'npm-audit.json') -Raw | ConvertFrom-Json
  if(!$audit.metadata){throw 'npm vulnerability audit unavailable'}
  if($audit.metadata.vulnerabilities.high -gt 0 -or $audit.metadata.vulnerabilities.critical -gt 0){throw 'npm high/critical advisories require review'}
  Write-Host "Engineering gates PASS; npm advisories: $($audit.metadata.vulnerabilities | ConvertTo-Json -Compress). See audit file."
} finally { Stop-Transcript | Out-Null }
