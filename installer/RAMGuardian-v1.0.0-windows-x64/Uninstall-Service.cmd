@echo off
echo Requesting administrator privileges to uninstall RAM Guardian Service...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0Uninstall-RAMGuardian.ps1\"' -Verb RunAs"
pause
