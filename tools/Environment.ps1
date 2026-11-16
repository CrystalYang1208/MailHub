param()
$ErrorActionPreference = 'Stop'
$script:MailHubRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $script:MailHubRoot
$env:OPENSPEC_TELEMETRY = '0'
$env:TEMP = Join-Path $script:MailHubRoot '.local/temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force $env:TEMP | Out-Null
# Use the installed SDK from PATH, or an explicit executable supplied by the developer.
if ($env:MAILHUB_DOTNET) {
  $script:Dotnet = (Get-Command $env:MAILHUB_DOTNET -ErrorAction Stop).Source
} else { $script:Dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (!$env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $script:MailHubRoot '.local/dotnet-home' }
if (!$env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = Join-Path $script:MailHubRoot '.local/nuget' }
$env:MAILHUB_DOTNET = $script:Dotnet
$env:MAILHUB_TEST_ROOT = Join-Path $script:MailHubRoot '.local/stage4/backend-data'
function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
  & $Executable @Arguments
  if ($LASTEXITCODE -ne 0) { throw "Command failed: $Executable $($Arguments -join ' ')" }
}
