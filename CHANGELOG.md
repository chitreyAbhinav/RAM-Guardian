# Changelog

All notable changes to the RAM Guardian project are documented in this file.

## [v1.0.0] - 2026-11-21
### Highlights & Core Features
- **1-Click Dual RAM & Edge Optimization Engine**:
  - Instant one-click memory reclaiming via native Win32 `EmptyWorkingSet`.
  - Safely flushes inactive physical memory pages from Microsoft Edge (tabs, isolates, GPU cache) and idle background helpers without closing tabs or terminating processes.
  - Measured benchmark: Reclaims **160+ MB physical RAM** in ~300ms, drops Edge footprint from **237 MB down to 112 MB** (-52%), and trims 48+ idle background processes.
  - Interactive UI buttons in Header, Overview meter card, and Edge panel.
  - Real-time visual feedback: animated gauge dial drops instantly, emerald toast notification banner displays exact reclaimed metrics, and audio confirmation plays.

- **GamingFocus Operating Mode (Default)**:
  - System-wide default mode tuned specifically for fixed 4 GB RAM laptops (ASUS VivoBook X515EA).
  - Actively maintains a target goal of **65%–70% average memory usage at all times**.
  - Calibrated available RAM pressure thresholds: Elevated (1,150 MB), High (950 MB), Critical (550 MB).
  - Escalates to graduated Level 4 termination only on approved background targets (OneDrive, Spotify, uTorrent Web) in extreme conditions (>85% RAM load).

- **24/7 Microsoft Edge Guardian**:
  - Always-on browser optimization enforcing aggressive 120-second sleeping tabs timeout.
  - Efficiency mode enabled by default.
  - Background processes disabled on browser exit; startup boost disabled to prevent silent RAM hoarding.

- **Windows 11 Start Menu & Search Integration**:
  - Registered as a native desktop application indexed by Windows Search (`Win + S` -> `RAM Guardian`).
  - Registered in Windows `App Paths` (`Win + R` -> `ramguardian`).
  - Added to Windows 11 Installed Applications registry with high-res icon and version metadata.
  - Configured for auto-start on boot via Windows Service (`LocalSystem`) and user login via Startup folder.

- **Automated Windows Service & Standalone Mode**:
  - Automatic Windows Service (`AUTO_START`, Code 2) running under `LocalSystem` with 60-second SCM auto-recovery.
  - Intelligent embedded engine fallback in Dashboard if Windows Service is stopped or not installed.
  - 1-click batch scripts: `Install-Service.cmd`, `Launch-Dashboard.cmd`, `Reinstall-Latest-Service.cmd`, `Uninstall-Service.cmd`.

- **Rock-Solid Safety & Immunity (Default-DENY)**:
  - Strict immutable protection for Windows OS kernel, Microsoft Defender, audio/network/ASUS drivers, and Safe Exam Browser (SEB).
  - User custom scripts protected: AutoHotkey (`ShortcutSwap.ahk`, `WindowButtonDisable.ahk`), IDM, and Filee.
  - Pre-state backups for all registry and startup changes in `C:\ProgramData\RAM Guardian\backups\` with 1-click rollback.

- **Automated Test Suite**:
  - 21 comprehensive automated unit and integration tests covering hardware diagnostics, classification, Edge optimization, and dual 1-click optimization (100% pass rate).
