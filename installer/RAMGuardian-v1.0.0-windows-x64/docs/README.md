# RAM Guardian

**Intelligent, Non-Destructive Windows Resource Monitor & Stability Engine**  
*Specifically engineered for fixed-hardware Windows 11 systems (ASUS VivoBook X515EA, 4 GB soldered DDR4 memory).*

---

## Overview

RAM Guardian is not a superficial "RAM cleaner" or task-killing gimmick. It is a carefully engineered systems monitoring application that continuously evaluates physical memory, commit charge, pagefile activity, kernel nonpaged pools, and process memory trends.

### Why RAM Guardian is Different
- **Policy: Default-DENY**: Unknown processes are protected by default.
- **Zero Automatic Process Termination**: RAM Guardian never terminates running processes or dev servers.
- **No Standby List Flushing**: Avoids causing disk thrashing and stutter.
- **No Aggressive Trimming**: Preserves working sets to prevent paging spikes.
- **Ultra-Lightweight Footprint**: Background service uses < 15 MB RAM and < 0.2% CPU.
- **Protected Components Registry**: Microsoft Defender, Windows Security, Audio, Wi-Fi, ASUS hardware utilities, Intel drivers, and **Safe Exam Browser** are strictly protected.
- **100% Reversible**: Every managed change creates a pre-state backup in `C:\ProgramData\RAM Guardian\backups\` before modification and can be rolled back with one click.

---

## Hardware & Environment Baseline
- **Laptop**: ASUS VivoBook X515EA
- **Processor**: Intel Core i5-1135G7 (4 Cores / 8 Logical Processors)
- **Physical Memory**: 4 GB DDR4-3200 soldered (3,790 MB visible, expansion slot unusable)
- **Operating System**: Windows 11 Home Single Language (64-bit), Build 26300+
- **Runtime**: Native .NET Framework 4.8.1 (pre-installed in Windows 11)

---

## Solution Architecture

```
RAM-Guardian/
├── RAMGuardian.sln
├── src/
│   ├── RAMGuardian.Common/         # Win32 P/Invoke, data models, zero-dependency JSON serializer
│   ├── RAMGuardian.Monitoring/     # GlobalMemoryStatusEx, GetPerformanceInfo, PerfCounters, Process collectors
│   ├── RAMGuardian.Security/       # Process & service protection policies, default-DENY engine
│   ├── RAMGuardian.Configuration/  # Persistent JSON configuration manager & validation
│   ├── RAMGuardian.Optimization/   # Startup inventory scanner, safety backup manager, action registry
│   ├── RAMGuardian.Core/           # Trend analyzer, hysteresis evaluator, leak detector, pool tracker
│   ├── RAMGuardian.Service/        # Dual-mode background Windows Service + Console runner
│   └── RAMGuardian.Dashboard/      # Dark Windows Forms UI + System Tray integration
├── tests/
│   └── RAMGuardian.Tests/          # Automated 15-test monitoring, safety, and rollback verification suite
├── scripts/                        # Installation, uninstallation, master build, and dashboard launchers
├── assets/                         # Multi-resolution application and tray icons
└── documentation/                  # Architecture, safety policy, and user manuals
```

---

## Quick Commands

### 1. Build Entire Solution
```powershell
& ".\scripts\Build-Release.ps1"
```

### 2. Run Test Suite
```powershell
& ".\tests\RAMGuardian.Tests\bin\Release\RAMGuardian.Tests.exe"
```

### 3. Run Live Console Monitor
```powershell
& ".\src\RAMGuardian.Service\bin\Release\RAMGuardian.Service.exe" --console
```

### 4. Launch Dark Dashboard
```powershell
& ".\scripts\Start-Dashboard.ps1"
```

### 5. Install as Background Windows Service (Admin)
```powershell
& ".\scripts\Install-RAMGuardian.ps1"
```

---

## License
MIT License. Created for local performance monitoring and system stability.
