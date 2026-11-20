@echo off
setlocal
cd /d "%~dp0"
title RAM Guardian Dashboard

set DASH_EXE=%~dp0src\RAMGuardian.Dashboard\bin\Release\RAMGuardian.Dashboard.exe
if not exist "%DASH_EXE%" (
    set DASH_EXE=C:\Program Files\RAM Guardian\RAMGuardian.Dashboard.exe
)

start "" "%DASH_EXE%"
exit /b 0
