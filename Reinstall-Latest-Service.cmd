@echo off
setlocal
cd /d %~dp0
title RAM Guardian - Fresh Reinstall Latest Service

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ELEVATION REQUIRED] Requesting Administrator privileges...
    powershell -NoProfile -ExecutionPolicy Bypass -Command Start-Process cmd -ArgumentList '/c ""%~f0""' -Verb RunAs
    exit /b 0
)

echo ================================================================
echo   RAM GUARDIAN - COMPLETE CLEAN UNINSTALL AND FRESH REINSTALL
echo ================================================================
echo.

echo [Step 1/4] Stopping existing service and killing lingering processes...
sc stop RAMGuardian >nul 2>&1
taskkill /f /im RAMGuardian.Service.exe >nul 2>&1
taskkill /f /im RAMGuardian.Dashboard.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo [Step 2/4] Removing old Windows Service registration...
sc delete RAMGuardian >nul 2>&1
timeout /t 1 /nobreak >nul

echo [Step 3/4] Installing latest version binaries to C:\Program Files\RAM Guardian...
powershell -NoProfile -ExecutionPolicy Bypass -File %~dp0scripts\Install-RAMGuardian.ps1

echo.
echo ================================================================
echo   FRESH INSTALLATION COMPLETE!
echo   Service: RAMGuardian (GamingFocus Default / 65-70%% RAM Target)
echo ================================================================
echo.
pause
exit /b 0
