# Changelog

## [1.0.0] - 2026-11-20
### Initial Release
- **Milestone 1**: Read-only monitoring engine using native Win32 `GlobalMemoryStatusEx` and `GetPerformanceInfo`. Added performance counter sampler and safe process enumerator. Built structured rolling file logger and atomic snapshot writer.
- **Milestone 2**: Implemented security framework with Default-DENY policy. Added immutable protection registries for Windows core components, Microsoft Defender, ASUS hardware utilities, and Safe Exam Browser. Implemented startup inventory scanner and backup/rollback subsystem.
- **Milestone 3**: Built multi-metric memory pressure evaluator with hysteresis, memory leak detector for continuous private byte growth, and kernel nonpaged pool trend tracker.
- **Milestone 4**: Safe optimization coordinator executing approved reversible startup optimizations under sustained pressure with mandatory pre-modification backups and one-click rollback.
- **Milestone 5**: Full desktop user interface with polished dark theme, anti-aliased memory meter, live trend history graph, top process list, alerts list, and system tray integration with background minimization.
- **Milestone 6**: Windows Service registration with auto-recovery, installer/uninstaller PowerShell scripts, and complete documentation.
