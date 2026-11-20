# ==============================================================================
# RAM Guardian - Safe Service & Application Uninstallation Script
# ==============================================================================

[CmdletBinding()]
param(
    [string]$InstallDir = "C:\Program Files\RAM Guardian",
    [switch]$PurgeData
)

$ErrorActionPreference = "Stop"

Write-Host "===============================================================" -ForegroundColor Yellow
Write-Host "         RAM GUARDIAN — UNINSTALLATION SCRIPT                  " -ForegroundColor Yellow
Write-Host "===============================================================" -ForegroundColor Yellow
Write-Host ""

# 1. Administrator check
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Elevation required: Please run PowerShell as Administrator to uninstall the Windows Service."
    exit 1
}

# 2. Stop and Delete Windows Service
$svc = Get-Service -Name "RAMGuardian" -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "Stopping RAMGuardian Windows Service..." -ForegroundColor Yellow
    Stop-Service -Name "RAMGuardian" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Host "Deleting RAMGuardian Windows Service registration..." -ForegroundColor Yellow
    & sc.exe delete "RAMGuardian" | Out-Null
    Write-Host "Service uninstalled." -ForegroundColor Green
} else {
    Write-Host "RAMGuardian service not currently registered." -ForegroundColor Gray
}

# 3. Remove Shortcuts
try {
    $desktopShortcut = "$([Environment]::GetFolderPath('Desktop'))\RAM Guardian.lnk"
    if (Test-Path $desktopShortcut) { Remove-Item $desktopShortcut -Force }

    $startShortcut = "$([Environment]::GetFolderPath('CommonPrograms'))\RAM Guardian.lnk"
    if (Test-Path $startShortcut) { Remove-Item $startShortcut -Force }

    Write-Host "Removed application shortcuts." -ForegroundColor Gray
} catch { }

# 4. Remove Installation Files
if (Test-Path $InstallDir) {
    Write-Host "Removing installed binaries from $InstallDir..." -ForegroundColor Yellow
    Remove-Item -Path $InstallDir -Recurse -Force
    Write-Host "Application directory removed." -ForegroundColor Gray
}

# 5. Data & Backups Preservation Check
$programData = "C:\ProgramData\RAM Guardian"
if ($PurgeData) {
    if (Test-Path $programData) {
        Write-Host "Purging all logs and backups from $programData..." -ForegroundColor Red
        Remove-Item -Path $programData -Recurse -Force
    }
} else {
    Write-Host ""
    Write-Host "[SAFETY NOTICE] User backups and diagnostic logs in $programData were PRESERVED." -ForegroundColor Cyan
    Write-Host "To remove them, delete the folder manually or rerun with: .\Uninstall-RAMGuardian.ps1 -PurgeData" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Uninstallation completed successfully." -ForegroundColor Green
