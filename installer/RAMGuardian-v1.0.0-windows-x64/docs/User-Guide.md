# RAM Guardian: User & Operational Guide

## 1. Quick Start

### Running the Live Console Monitor
To inspect memory diagnostics in real-time inside your PowerShell terminal without installing anything:
```powershell
& "C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\src\RAMGuardian.Service\bin\Release\RAMGuardian.Service.exe" --console
```
*(Press `Q` or `Ctrl+C` to quit)*

### Capturing a Single Diagnostic Snapshot
```powershell
& "C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\src\RAMGuardian.Service\bin\Release\RAMGuardian.Service.exe" --once
```

### Launching the Dark Dashboard
```powershell
& "C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\scripts\Start-Dashboard.ps1"
```

---

## 2. Operating Modes

RAM Guardian supports three operating modes:

1. **MonitorOnly (Default Mode)**
   - Continuous background telemetry collection.
   - Evaluates memory pressure, kernel nonpaged pools, and process memory trends.
   - Saves atomic state to `C:\ProgramData\RAM Guardian\latest.json`.
   - **Zero system modifications are made.**

2. **SafeAutomatic**
   - Evaluates sustained memory pressure over a 30–45 second window.
   - If sustained High or Critical pressure is detected and the 5-minute cooldown has passed, safe approved optimizations (e.g. disabling approved startup items) may be executed.
   - Always creates a timestamped rollback backup prior to modification.

3. **Paused**
   - Telemetry collection is temporarily suspended.
   - No optimizations are executed.

To switch mode via command line:
```powershell
RAMGuardian.Service.exe --mode SafeAutomatic
RAMGuardian.Service.exe --mode MonitorOnly
```

---

## 3. Reviewing & Restoring Changes

### Viewing Managed Backups
```powershell
RAMGuardian.Service.exe --backups
```

### Restoring All Changes
```powershell
RAMGuardian.Service.exe --restore-all
```
*(Or click "Restore Managed Changes" in the Dashboard or System Tray menu)*

---

## 4. Installing as a Background Windows Service

To have RAM Guardian start automatically on boot and monitor silently without any terminal window open:

1. Open PowerShell **as Administrator**.
2. Run:
   ```powershell
   & "C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\scripts\Install-RAMGuardian.ps1"
   ```
3. The service `RAMGuardian` will be registered with automatic startup and crash-recovery configured.
4. Desktop and Start Menu shortcuts will be created.

To uninstall:
```powershell
& "C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\scripts\Uninstall-RAMGuardian.ps1"
```
*(User backups and diagnostic logs are safely preserved by default)*
