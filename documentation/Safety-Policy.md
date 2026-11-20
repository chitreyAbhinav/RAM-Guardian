# RAM Guardian: Non-Negotiable Safety Policy & Invariant Rules

## 1. Core Operating Philosophy: Default-DENY

RAM Guardian is specifically engineered for a fixed-hardware laptop (ASUS VivoBook X515EA, 4 GB soldered DDR4 memory, broken expansion slot). Because physical memory cannot be expanded and resources are constrained, amateur utilities often employ destructive tactics such as process killing, forced working set trimming, or standby list flushing.

**RAM Guardian strictly rejects all of these destructive gimmicks.**

### The Default-DENY Rule
- **Every process that cannot be positively identified as approved for an action is treated as PROTECTED.**
- **Automatic process termination policy: DENY.** Under no circumstance will RAM Guardian kill or terminate a user application or system process.
- **Standby list trimming is forbidden**: Windows standby memory is a read cache. Emptying it forces disk thrashing and makes the system slower, not faster.
- **EmptyWorkingSet is forbidden**: Forcing working set reduction merely forces pages out to the pagefile, causing immediate page-fault spikes and system stutter when the user switches back to the application.

---

## 2. Protected System & Hardware Components

The following components are immutable and protected at all times:

### 1. Microsoft Security
- Microsoft Defender Antivirus (`MsMpEng.exe`, `NisSrv.exe`)
- Windows Security Center (`SecurityHealthService.exe`, `SecurityHealthSystray.exe`)
- SmartScreen (`smartscreen.exe`)
- Windows Firewall (`mpssvc`)

### 2. Core Windows Operating System
- `System` (PID 4), `Idle` (PID 0), `Registry`
- `smss.exe`, `csrss.exe`, `wininit.exe`, `services.exe`, `lsass.exe`, `winlogon.exe`
- Desktop Window Manager (`dwm.exe`)
- Windows Explorer (`explorer.exe`)
- Windows Memory Compression (`Memory Compression`, PID 3364)
- Windows Service Control Manager (`services.exe`)
- Windows Update & Orchestration (`wuauserv`, `UsoSvc`)
- SysMain (`SysMain`) — monitored only; never disabled automatically

### 3. ASUS Hardware & Drivers
- ASUS System Control Interface & ASUS Services (`AsusAppService`, `AsusSoftwareManager`, `AsusSystemAnalysis`, `AsusSystemDiagnosis`, `asus_framework`)
- Intel Graphics & Display Services (`igfxCUIService`, `OneApp.IGCC.WinService`, `IntelCpHDCPSvc`)
- Realtek Audio Services (`RtkAudioService64`, `AudioSrv`, `AudioEndpointBuilder`)
- ELAN / Precision Touchpad drivers (`ETDCtrl`, `ETDService`)

### 4. User-Specific Protected Components
- **AutoHotkey Scripts**: `ShortcutSwap.ahk`, `WindowButtonDisable.ahk` (Explicitly preserved in startup and runtime)
- **Safe Exam Browser**: `SafeExamBrowser.exe`, `SafeExamBrowser.Client.exe`, `SafeExamBrowser.Service.exe`
- **Filee**: `Filee.exe` (Preserved pending user investigation)
- **Internet Download Manager**: `IDMan.exe` (Preserved for download sessions)
- **Microsoft Edge**: Active browser sessions protected

---

## 3. Approved Optimization Targets & User Preferences

Only startup entries explicitly approved by the user may be optimized, and only when the user chooses **SafeAutomatic** mode:

| Target | Location | Expected Impact | Rollback Capability |
|---|---|---|---|
| **OneDrive** | `HKCU\...\Run` | Saves ~40–80 MB physical working set & reduces background disk indexing | Complete instantaneous registry restoration from backup |
| **Spotify** | `HKCU\...\Run` | Saves ~80–150 MB private memory at startup | Complete registry restoration from backup |
| **uTorrent Web** | `HKCU\...\Run` | Saves ~50–90 MB memory and listening threads | Complete registry restoration from backup |
| **Cloudflare WARP** | `HKLM\...\Run` | Saves ~70 MB private bytes; client remains available on demand | Complete registry restoration from backup |

---

## 4. Mandatory Reversibility Guarantee

Before ANY system setting or registry value is modified:
1. **Full Pre-State Serialization**: The complete command line, registry key, and metadata are saved to `C:\ProgramData\RAM Guardian\backups\BKP_YYYYMMDD_HHmmss_<entry>.json`.
2. **Atomic Verification**: If backup creation fails, the modification is immediately aborted.
3. **Rollback Mechanism**: Users can restore any or all changes via the Dashboard button ("Restore Managed Changes") or via CLI:
   ```powershell
   RAMGuardian.Service.exe --restore-all
   ```
4. **Permanent Backup Retention**: Uninstallation of RAM Guardian will never silently delete user backup records.
