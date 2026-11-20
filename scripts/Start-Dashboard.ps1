# ==============================================================================
# RAM Guardian - Dashboard Launcher
# ==============================================================================

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$dashBin = "$projectRoot\src\RAMGuardian.Dashboard\bin\Release\RAMGuardian.Dashboard.exe"

if (-not (Test-Path $dashBin)) {
    Write-Host "Compiling RAM Guardian first..." -ForegroundColor Yellow
    & "$scriptDir\Build-Release.ps1"
}

Write-Host "Launching RAM Guardian Dashboard..." -ForegroundColor Cyan
Start-Process -FilePath $dashBin
