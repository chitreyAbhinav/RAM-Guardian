# ==============================================================================
# RAM Guardian - Automated Service & System Installation Script
# Platform: Windows 11 (ASUS VivoBook X515EA / 4 GB Fixed Hardware)
# ==============================================================================

[CmdletBinding()]
param(
    [string]$InstallDir = "C:\Program Files\RAM Guardian",
    [switch]$SourceFromBinaries
)

$ErrorActionPreference = "Stop"

Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "         RAM GUARDIAN — INSTALLATION & SERVICE REGISTRATION   " -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Administrator check
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Elevation required: Please run PowerShell as Administrator to register the Windows Service."
    Write-Host "You can still run RAM Guardian in user mode via: .\RAMGuardian.Service.exe --console" -ForegroundColor Yellow
    exit 1
}

# 2. Source Binaries Resolution
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$serviceBin = "$projectRoot\src\RAMGuardian.Service\bin\Release\RAMGuardian.Service.exe"
$dashBin = "$projectRoot\src\RAMGuardian.Dashboard\bin\Release\RAMGuardian.Dashboard.exe"

if (-not (Test-Path $serviceBin)) {
    Write-Error "RAMGuardian.Service.exe not found at: $serviceBin. Please build the solution first."
    exit 1
}

# 3. Create ProgramData and Target directories
$programData = "C:\ProgramData\RAM Guardian"
$dirs = @($InstallDir, $programData, "$programData\logs", "$programData\backups")
foreach ($d in $dirs) {
    if (-not (Test-Path $d)) {
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        Write-Host "Created directory: $d" -ForegroundColor Gray
    }
}

# 4. Stop existing service and running processes before copying binaries
$svc = Get-Service -Name "RAMGuardian" -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "Stopping existing RAMGuardian service..." -ForegroundColor Yellow
    Stop-Service -Name "RAMGuardian" -Force -ErrorAction SilentlyContinue
    $timeout = 10
    while ($timeout -gt 0 -and (Get-Service -Name "RAMGuardian" -ErrorAction SilentlyContinue).Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Start-Sleep -Seconds 1
        $timeout--
    }
}

# Stop any running Dashboard or Service processes that could lock DLLs
Get-Process -Name "RAMGuardian*" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping running process $($_.ProcessName) (PID: $($_.Id))..." -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

# 5. Copy Binaries
Write-Host "Installing application binaries to $InstallDir..." -ForegroundColor Cyan
$releaseDir = "$projectRoot\src\RAMGuardian.Service\bin\Release"
$dashReleaseDir = "$projectRoot\src\RAMGuardian.Dashboard\bin\Release"

Get-ChildItem -Path $releaseDir -Filter "*.dll" | Copy-Item -Destination $InstallDir -Force
Get-ChildItem -Path $releaseDir -Filter "*.exe" | Copy-Item -Destination $InstallDir -Force
Get-ChildItem -Path $dashReleaseDir -Filter "*.exe" | Copy-Item -Destination $InstallDir -Force
Copy-Item -Path "$projectRoot\assets\icons\ramguardian.ico" -Destination "$InstallDir\ramguardian.ico" -Force -ErrorAction SilentlyContinue

# 6. Register Windows Service
$installedServicePath = "$InstallDir\RAMGuardian.Service.exe"
if (-not $svc) {
    Write-Host "Registering Windows Service 'RAMGuardian'..." -ForegroundColor Cyan
    & sc.exe create "RAMGuardian" binPath= "`"$installedServicePath`"" start= auto DisplayName= "RAM Guardian Resource Monitor"
} else {
    Write-Host "Updating existing Windows Service path..." -ForegroundColor Cyan
    & sc.exe config "RAMGuardian" binPath= "`"$installedServicePath`"" start= auto
}

# 7. Configure SCM Failure Recovery (Restart on failure after 60 seconds)
& sc.exe failure "RAMGuardian" reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
& sc.exe description "RAMGuardian" "Intelligent, non-destructive background resource monitor engineered for 4 GB RAM Windows 11 systems." | Out-Null

# 8. Start Service
Write-Host "Starting RAMGuardian background service..." -ForegroundColor Cyan
Start-Service -Name "RAMGuardian"
$status = (Get-Service -Name "RAMGuardian").Status
Write-Host "Service Status: $status" -ForegroundColor Green

# 9. Create Desktop & Start Menu Shortcuts
try {
    $wsh = New-Object -ComObject WScript.Shell

    # Desktop Shortcut
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $shortcutDesktop = $wsh.CreateShortcut("$desktopPath\RAM Guardian.lnk")
    $shortcutDesktop.TargetPath = "$InstallDir\RAMGuardian.Dashboard.exe"
    $shortcutDesktop.IconLocation = "$InstallDir\ramguardian.ico,0"
    $shortcutDesktop.Description = "RAM Guardian Resource Monitor Dashboard"
    $shortcutDesktop.Save()

    # Start Menu Shortcut
    $programsPath = [Environment]::GetFolderPath("CommonPrograms")
    $shortcutStart = $wsh.CreateShortcut("$programsPath\RAM Guardian.lnk")
    $shortcutStart.TargetPath = "$InstallDir\RAMGuardian.Dashboard.exe"
    $shortcutStart.IconLocation = "$InstallDir\ramguardian.ico,0"
    $shortcutStart.Description = "RAM Guardian Resource Monitor Dashboard"
    $shortcutStart.Save()

    Write-Host "Created Desktop and Start Menu shortcuts." -ForegroundColor Green
} catch {
    Write-Warning "Could not create shell shortcuts: $_"
}

Write-Host ""
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "   RAM GUARDIAN INSTALLATION COMPLETE!                         " -ForegroundColor Green
Write-Host "   Default Mode: MonitorOnly (Non-destructive observation)     " -ForegroundColor Green
Write-Host "   Service running silently in background with auto-restart.   " -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Green
