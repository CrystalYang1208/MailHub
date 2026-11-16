@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
set MAILHUB_ENGINEERING=
if exist "MailHub.Host.exe" (
  start "MailHub" "MailHub.Host.exe"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "tools\Start-MailHub.ps1" -Open
  if errorlevel 1 pause
)
endlocal
