@echo off
echo Requesting administrator privileges to install RAM Guardian Service...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0Install-RAMGuardian.ps1\"' -Verb RunAs"
pause
