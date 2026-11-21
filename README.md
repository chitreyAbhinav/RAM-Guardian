# RAM Guardian

<p align="center">
  <strong>Intelligent, Non-Destructive Windows Resource Monitor & Stability Engine</strong><br>
  <em>Specifically engineered for 4 GB RAM Windows 11 systems (ASUS VivoBook X515EA, Intel Core i5-1135G7).</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-0078D6?style=flat&logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8.1-512BD4?style=flat&logo=dotnet" alt=".NET">
  <img src="https://img.shields.io/badge/Tests-21%20%2F%2021%20Passing%20(100%25)-success?style=flat" alt="Tests">
  <img src="https://img.shields.io/badge/Default%20Mode-GamingFocus%20(65--70%25%20Goal)-00b4d8?style=flat" alt="Default Mode">
  <img src="https://img.shields.io/badge/License-MIT-green?style=flat" alt="License">
</p>

---

## Overview

**RAM Guardian** is an intelligent, low-overhead resource monitoring and memory optimization suite developed specifically for fixed soldered memory laptops (such as the ASUS VivoBook X515EA with 4 GB DDR4-3200, where expanding physical memory is impossible).

Unlike crude "RAM cleaners" that blindly flush standby lists or crash background processes, RAM Guardian uses continuous telemetry and non-destructive Win32 memory APIs to reclaim memory smoothly while protecting operating system stability.

---

## Key Features

### ⚡ 1-Click Dual RAM & Edge Optimization
* **Instant Physical RAM Reclaiming**: Safely flushes idle working sets via Win32 `EmptyWorkingSet` without closing tabs, losing unsaved state, or terminating processes.
* **Microsoft Edge Trimming**: Drops Edge memory usage by **50%+** in a fraction of a second while keeping all active tabs running.
* **Visible Real-Time Feedback**:
  * RAM load needle and megabyte readouts drop immediately on screen.
  * High-visibility emerald notification banner drops down with exact reclaimed metrics (`Reclaimed: 167.6 MB RAM | Edge Reduced: 125.2 MB | New Load: 85%`).
  * Dedicated 1-Click buttons located in the Header (accessible anywhere), Overview tab, and Microsoft Edge tab.

### 🎮 GamingFocus Operating Mode (Default)
* **Goal: 65%–70% RAM Usage**: Actively tunes background pressure to sustain optimal memory headroom for gaming and multitasking.
* **Free-Hand Background Tuning**: Escalates to graduated Level 4 graceful closures on approved background targets (OneDrive, Spotify, uTorrent Web) during extreme memory pressure (>85% load).
* **Calibrated Thresholds for 4 GB**: Elevated (1,150 MB available), High (950 MB available), Critical (550 MB available).

### 🌐 24/7 Microsoft Edge Guardian
* Enforces aggressive **120-second sleeping tabs** timeout.
* Efficiency mode enabled 24/7.
* Background extensions and tasks automatically suspended on browser exit.
* Startup boost disabled to eliminate silent bootup RAM hoarding.

### 🛡️ Ironclad Safety Policy (Default-DENY)
* **Immutable Protection**: Core Windows processes (`csrss`, `explorer`, `services`, `lsass`), Microsoft Defender, ASUS hardware utilities, Intel graphics drivers, and **Safe Exam Browser (SEB)** are permanently immune.
* **User Custom Script Protection**: AutoHotkey scripts (`ShortcutSwap.ahk`, `WindowButtonDisable.ahk`), Internet Download Manager (`IDMan`), and `Filee` are strictly preserved.
* **100% Reversible**: Every system configuration change creates an atomic pre-state backup in `C:\ProgramData\RAM Guardian\backups\` and can be restored with a single click.
* **Global Emergency Stop**: One-click killswitch immediately halts all automated background management.

### 🔍 Windows 11 Native Integration
* Searchable directly from the Windows Start screen (`Win + S` -> **RAM Guardian**).
* Quick launch via Windows Run dialog (`Win + R` -> **`ramguardian`**).
* Runs as an Automatic Windows Service (`AUTO_START`, Code 2) starting at boot before user login.
* Runs in system tray with live tooltip stats and one-click quick actions.

---

## Hardware Baseline (Tested & Verified)

| Component | Specification |
| :--- | :--- |
| **Model** | ASUS VivoBook X515EA |
| **Processor** | Intel Core i5-1135G7 @ 2.40 GHz (4 Cores / 8 Threads) |
| **Installed RAM** | 4.00 GB DDR4-3200 (3,790 MB usable, soldered) |
| **Operating System** | Windows 11 Home Single Language (64-bit), Build 26300+ |
| **Runtime** | .NET Framework 4.8.1 (Native Windows 11 component) |
| **Service Footprint** | `< 12 MB` Physical RAM, `< 0.2%` CPU |

---

## Solution Architecture

```
RAM-Guardian/
├── RAMGuardian.sln
├── src/
│   ├── RAMGuardian.Common/         # Win32 P/Invoke (EmptyWorkingSet, GlobalMemoryStatusEx), Models
│   ├── RAMGuardian.Monitoring/     # SystemMemoryCollector, ProcessMemoryCollector, PerfCounters
│   ├── RAMGuardian.Security/       # 9-Tier Process Intelligence, Immutable Registries, Default-DENY
│   ├── RAMGuardian.Configuration/  # Persistent JSON ConfigManager with corrupted file auto-recovery
│   ├── RAMGuardian.Optimization/   # OneClickOptimizer, EdgeOptimizer, ProcessTerminationEngine
│   ├── RAMGuardian.Core/           # TrendAnalyzer, MemoryPressureMatrix, OptimizationCoordinator
│   ├── RAMGuardian.Service/        # Dual-mode background Windows Service + Console runner
│   └── RAMGuardian.Dashboard/      # Dark WinForms Dashboard, Custom Controls, System Tray integration
├── tests/
│   └── RAMGuardian.Tests/          # Automated 21-test verification suite (100% pass rate)
├── scripts/                        # Installation, Build, Dashboard launch, and Packaging scripts
├── installer/                      # Standalone ZIP distribution package
└── assets/                         # Multi-resolution icons (256x256 to 16x16)
```

---

## Quick Start & Installation

### Option 1: Standalone Dashboard (No Admin Required)
1. Download **`RAMGuardian-v1.0.0-windows-x64.zip`** from [Releases](https://github.com/chitreyAbhinav/RAM-Guardian/releases).
2. Extract the archive to any folder.
3. Double-click **`Launch-Dashboard.cmd`** or run:
   ```cmd
   RAMGuardian.Dashboard.exe
   ```
*If the Windows Service is not installed, the Dashboard automatically runs an embedded background monitoring engine!*

### Option 2: Automatic Windows Service (24/7 Protection)
1. Right-click **`Install-Service.cmd`** and select **Run as Administrator**.
2. The installer will:
   * Install binaries to `C:\Program Files\RAM Guardian\`.
   * Register and start the `RAMGuardian` Windows Service with auto-restart on boot.
   * Place shortcuts in your Start Menu, Desktop, and Startup folder.

---

## Automated Verification Suite

Run all 21 automated tests:
```powershell
& ".\tests\RAMGuardian.Tests\bin\Release\RAMGuardian.Tests.exe"
```

```
===============================================================
        RAM GUARDIAN - COMPREHENSIVE AUTOMATED TEST SUITE      
   ASUS VivoBook X515EA 4 GB RAM Profile & Redesign Verification
===============================================================
Test 1: Native GlobalMemoryStatusEx & Memory Readings          ... [PASS]
Test 2: Native GetPerformanceInfo & Kernel Pools               ... [PASS]
Test 3: PerformanceCounter Collector Robustness                ... [PASS]
Test 4: SystemMemoryCollector Full Metric Extraction           ... [PASS]
Test 5: ProcessMemoryCollector & Protection Verification       ... [PASS]
Test 6: Inaccessible / Terminated Process Error Handling       ... [PASS]
Test 7: JSON Serialization & Snapshot Integrity                ... [PASS]
Test 8: Memory Pressure Composite Matrix Evaluation            ... [PASS]
Test 9: Process Protection Policy & Default-DENY               ... [PASS]
Test 10: Service Protection Policy & Tamper Prevention         ... [PASS]
Test 11: Startup Protection Policy (AHK & Approved Targets)    ... [PASS]
Test 12: Startup Scanner Real-World Discovery on Laptop        ... [PASS]
Test 13: Configuration Persistence & Recovery                  ... [PASS]
Test 14: Safety Backup & Atomic Rollback Mechanism             ... [PASS]
Test 15: Action Precondition Guard & Mode Enforcement          ... [PASS]
Test 16: 5 Operating Modes & Global Emergency Killswitch       ... [PASS]
Test 17: Process Intelligence Engine (9-Tier Immunity)         ... [PASS]
Test 18: Microsoft Edge Role Classifier & 24/7 Policies        ... [PASS]
Test 19: Graduated 4-Level Process Termination Safety          ... [PASS]
Test 20: Optimization Coordinator End-to-End Across All Modes  ... [PASS]
Test 21: OneClickOptimizer Dual RAM and Edge Optimization      ... [PASS]
===============================================================
  TEST SUMMARY: 21 PASSED, 0 FAILED (TOTAL: 21)
===============================================================
```

---

## Building from Source

Requirements: Windows 10/11 with .NET Framework 4.8.1 and MSBuild.

```powershell
git clone https://github.com/chitreyAbhinav/RAM-Guardian.git
cd RAM-Guardian
.\scripts\Build-Release.ps1
```

---

## License

MIT License. Engineered for maximum Windows 11 stability and lightweight performance.
