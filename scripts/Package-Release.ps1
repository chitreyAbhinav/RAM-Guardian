# ==============================================================================
# RAM Guardian - Release Packaging Script (Milestone 7)
# ==============================================================================

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$pkgDir = "$projectRoot\installer\RAMGuardian-v1.0.0-windows-x64"
$zipPath = "$projectRoot\installer\RAMGuardian-v1.0.0-windows-x64.zip"

Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "          RAM GUARDIAN — MASTER RELEASE PACKAGING              " -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Clean previous staging
if (Test-Path $pkgDir) { Remove-Item $pkgDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null
New-Item -ItemType Directory -Path "$pkgDir\config-templates" -Force | Out-Null
New-Item -ItemType Directory -Path "$pkgDir\docs" -Force | Out-Null

# 2. Build Solution
Write-Host "Compiling solution in Release | x64 mode..." -ForegroundColor Cyan
$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"
& $msbuild "$projectRoot\RAMGuardian.sln" /p:Configuration=Release /p:Platform=x64 /nologo /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Packaging aborted."
    exit 1
}

# 3. Copy Binaries
Write-Host "Staging release binaries..." -ForegroundColor Cyan
$serviceBinDir = "$projectRoot\src\RAMGuardian.Service\bin\Release"
$dashBinDir = "$projectRoot\src\RAMGuardian.Dashboard\bin\Release"

Get-ChildItem -Path $serviceBinDir -Filter "*.exe" | Copy-Item -Destination $pkgDir -Force
Get-ChildItem -Path $serviceBinDir -Filter "*.dll" | Copy-Item -Destination $pkgDir -Force
Get-ChildItem -Path $dashBinDir -Filter "*.exe" | Copy-Item -Destination $pkgDir -Force
Copy-Item -Path "$projectRoot\assets\icons\ramguardian.ico" -Destination "$pkgDir\ramguardian.ico" -Force

# 4. Copy Documentation
Write-Host "Staging documentation..." -ForegroundColor Cyan
Copy-Item -Path "$projectRoot\README.md" -Destination "$pkgDir\docs\README.md" -Force
Copy-Item -Path "$projectRoot\CHANGELOG.md" -Destination "$pkgDir\docs\CHANGELOG.md" -Force
Get-ChildItem -Path "$projectRoot\documentation" -Filter "*.md" | Copy-Item -Destination "$pkgDir\docs" -Force

# 5. Create Configuration Templates
Write-Host "Generating configuration templates..." -ForegroundColor Cyan

$configTemplate = @'
{
  "OperatingMode": "MonitorOnly",
  "MonitoringIntervalSeconds": 15,
  "DashboardRefreshIntervalSeconds": 5,
  "ElevatedAvailableRamMB": 500.0,
  "HighAvailableRamMB": 250.0,
  "CriticalAvailableRamMB": 150.0,
  "ElevatedCommitPercentage": 78.0,
  "HighCommitPercentage": 82.0,
  "CriticalCommitPercentage": 90.0,
  "HighPagingPagesPerSec": 1500.0,
  "CriticalPagingPagesPerSec": 2500.0,
  "CooldownMinutes": 5,
  "EnableNotifications": true,
  "LaunchDashboardOnLogin": false,
  "MinimizeDashboardToTray": true,
  "LogRetentionDays": 14,
  "MaxLogFileSizeBytes": 10485760
}
'@
Set-Content -Path "$pkgDir\config-templates\config.template.json" -Value $configTemplate -Encoding UTF8

$protectedProcTemplate = @'
[
  "AutoHotkey",
  "AutoHotkeyUX",
  "code",
  "devenv",
  "git",
  "node",
  "python",
  "powershell",
  "cmd"
]
'@
Set-Content -Path "$pkgDir\config-templates\protected-processes.template.json" -Value $protectedProcTemplate -Encoding UTF8

$protectedSvcTemplate = @'
[
  "VMware",
  "VMAuthdService",
  "VMUSBArbService"
]
'@
Set-Content -Path "$pkgDir\config-templates\protected-services.template.json" -Value $protectedSvcTemplate -Encoding UTF8

# 6. Copy Scripts and create CMD Launchers
Copy-Item -Path "$projectRoot\scripts\Install-RAMGuardian.ps1" -Destination "$pkgDir\Install-RAMGuardian.ps1" -Force
Copy-Item -Path "$projectRoot\scripts\Uninstall-RAMGuardian.ps1" -Destination "$pkgDir\Uninstall-RAMGuardian.ps1" -Force

# Quick Double-Click CMD Launchers
$installCmd = @'
@echo off
echo Requesting administrator privileges to install RAM Guardian Service...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0Install-RAMGuardian.ps1\"' -Verb RunAs"
pause
'@
Set-Content -Path "$pkgDir\Install-Service.cmd" -Value $installCmd -Encoding ASCII

$uninstallCmd = @'
@echo off
echo Requesting administrator privileges to uninstall RAM Guardian Service...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0Uninstall-RAMGuardian.ps1\"' -Verb RunAs"
pause
'@
Set-Content -Path "$pkgDir\Uninstall-Service.cmd" -Value $uninstallCmd -Encoding ASCII

$startDashCmd = @'
@echo off
start "" "%~dp0RAMGuardian.Dashboard.exe"
'@
Set-Content -Path "$pkgDir\Start-Dashboard.cmd" -Value $startDashCmd -Encoding ASCII

$runConsoleCmd = @'
@echo off
"%~dp0RAMGuardian.Service.exe" --console
pause
'@
Set-Content -Path "$pkgDir\Run-Console-Monitor.cmd" -Value $runConsoleCmd -Encoding ASCII

# 7. Compress to standalone ZIP
Write-Host "Creating standalone ZIP archive..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($pkgDir, $zipPath)

$zipInfo = Get-Item $zipPath
$zipSizeKB = [math]::Round($zipInfo.Length / 1KB, 1)

Write-Host ""
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "   PACKAGING COMPLETE (Milestone 7)                            " -ForegroundColor Green
Write-Host "   Distribution Folder: $pkgDir" -ForegroundColor Green
Write-Host "   ZIP Archive:         $zipPath ($zipSizeKB KB)" -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Green
