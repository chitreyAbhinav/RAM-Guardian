# ==============================================================================
# RAM Guardian - Automated Master Build Script
# ==============================================================================

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$slnPath = "$projectRoot\RAMGuardian.sln"
$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"

Write-Host "Compiling RAM Guardian Solution with MSBuild (Release | x64)..." -ForegroundColor Cyan
& $msbuild $slnPath /p:Configuration=Release /p:Platform=x64 /nologo /verbosity:minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build succeeded! Executables generated:" -ForegroundColor Green
    Write-Host " - Service:   $projectRoot\src\RAMGuardian.Service\bin\Release\RAMGuardian.Service.exe" -ForegroundColor Gray
    Write-Host " - Dashboard: $projectRoot\src\RAMGuardian.Dashboard\bin\Release\RAMGuardian.Dashboard.exe" -ForegroundColor Gray
    Write-Host " - Tests:     $projectRoot\tests\RAMGuardian.Tests\bin\Release\RAMGuardian.Tests.exe" -ForegroundColor Gray
} else {
    Write-Host "Build failed with exit code $LASTEXITCODE." -ForegroundColor Red
}
