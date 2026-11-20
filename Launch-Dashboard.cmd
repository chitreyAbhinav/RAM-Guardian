@echo off
setlocal
cd /d "%~dp0"
title RAM Guardian Dashboard
set DASH_EXE=src\RAMGuardian.Dashboard\bin\Release\RAMGuardian.Dashboard.exe

if not exist "%DASH_EXE%" (
    echo [RAM Guardian] Release binaries not found. Building first...
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Build-Release.ps1
)

start "" "%DASH_EXE%"
exit /b 0
