@echo off
setlocal
cd /d "%~dp0"
title RAM Guardian - Install Windows Service

:: Check for Administrator elevation
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ELEVATION REQUIRED] Requesting Administrator privileges...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b 0
)

echo ==============================================================
echo   Installing RAM Guardian as an Automatic Windows Service
echo ==============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install-RAMGuardian.ps1"
echo.
pause
exit /b %errorLevel%
