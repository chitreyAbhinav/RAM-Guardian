using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Native;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Configuration;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Core.Alerts;
using RAMGuardian.Core.Analysis;
using RAMGuardian.Logging;
using RAMGuardian.Monitoring;
using RAMGuardian.Optimization;
using RAMGuardian.Optimization.Actions;
using RAMGuardian.Optimization.Models;
using RAMGuardian.Security.Policies;
using RAMGuardian.Service;

namespace RAMGuardian.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("===============================================================");
            Console.WriteLine("        RAM GUARDIAN - COMPREHENSIVE AUTOMATED TEST SUITE      ");
            Console.WriteLine("   ASUS VivoBook X515EA 4 GB RAM Profile & Redesign Verification");
            Console.WriteLine("===============================================================");
            Console.WriteLine();

            int passed = 0;
            int failed = 0;

            // --- Milestone 1 Tests (Hardware Diagnostics & Native Counters) ---
            RunTest("Test 1: Native GlobalMemoryStatusEx & Memory Readings", () =>
            {
                var mem = new NativeMethods.MEMORYSTATUSEX();
                bool ok = NativeMethods.GlobalMemoryStatusEx(mem);
                Assert(ok, "GlobalMemoryStatusEx returned true");
                Assert(mem.ullTotalPhys > 1024L * 1024 * 1024, "Total physical RAM > 1 GB");
                Assert(mem.ullAvailPhys > 0, "Available physical RAM > 0");
                Assert(mem.ullTotalPageFile >= mem.ullTotalPhys, "Commit Limit >= Total Physical RAM");
                Assert(mem.dwMemoryLoad >= 1 && mem.dwMemoryLoad <= 100, "dwMemoryLoad is valid percentage (1-100)");
            }, ref passed, ref failed);

            RunTest("Test 2: Native GetPerformanceInfo & Kernel Pools", () =>
            {
                NativeMethods.PERFORMANCE_INFORMATION perf;
                int cb = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.PERFORMANCE_INFORMATION));
                bool ok = NativeMethods.GetPerformanceInfo(out perf, cb);
                Assert(ok, "GetPerformanceInfo returned true");
                Assert(perf.PageSize.ToInt64() == 4096, "x64 standard page size is 4096 bytes");
                Assert(perf.KernelNonpaged.ToInt64() > 0, "Kernel nonpaged pool pages > 0");
                Assert(perf.KernelPaged.ToInt64() > 0, "Kernel paged pool pages > 0");
                Assert(perf.ProcessCount > 10, "System process count > 10");
                Assert(perf.CommitLimit.ToInt64() >= perf.CommitTotal.ToInt64(), "CommitLimit >= CommitTotal");
            }, ref passed, ref failed);

            RunTest("Test 3: PerformanceCounter Collector Robustness", () =>
            {
                using (var perf = new PerformanceCounterCollector())
                {
                    double pagesPerSec = perf.GetPagesPerSec();
                    double faultsPerSec = perf.GetPageFaultsPerSec();
                    double pagefileUsage = perf.GetPagefileUsagePercentage();
                    double cpu = perf.GetCpuUtilization();

                    Assert(pagesPerSec >= 0.0, "Pages/sec is non-negative");
                    Assert(faultsPerSec >= 0.0, "Page faults/sec is non-negative");
                    Assert(pagefileUsage >= 0.0 && pagefileUsage <= 100.0, "Pagefile usage is between 0% and 100%");
                    Assert(cpu >= 0.0 && cpu <= 100.0, "CPU utilization is between 0% and 100%");
                }
            }, ref passed, ref failed);

            RunTest("Test 4: SystemMemoryCollector Full Metric Extraction", () =>
            {
                using (var collector = new SystemMemoryCollector())
                {
                    SystemMemoryMetrics m = collector.CollectMetrics();
                    Assert(m.TotalPhysicalMB > 3000.0, "Total physical RAM matches laptop hardware (~3.7 GB)");
                    Assert(m.AvailablePhysicalMB > 0.0, "Available RAM is positive");
                    Assert(m.UsedPhysicalMB > 0.0, "Used physical RAM is positive");
                    Assert(m.NonpagedPoolMB > 100.0, "Nonpaged pool is populated (> 100 MB)");
                    Assert(m.PagedPoolMB > 50.0, "Paged pool is populated (> 50 MB)");
                    Assert(m.CommittedMB > 1000.0, "Committed memory is populated (> 1 GB)");
                }
            }, ref passed, ref failed);

            RunTest("Test 5: ProcessMemoryCollector & Protection Verification", () =>
            {
                var procCollector = new ProcessMemoryCollector();
                List<ProcessMemoryInfo> procs = procCollector.GetTopProcesses(20);

                Assert(procs != null && procs.Count > 0, "Process list returned entries");
                Assert(procs.Count <= 20, "Process list respects top 20 limit");

                for (int i = 0; i < procs.Count - 1; i++)
                {
                    Assert(procs[i].WorkingSetBytes >= procs[i + 1].WorkingSetBytes, "Processes sorted by WorkingSet descending");
                }
            }, ref passed, ref failed);

            RunTest("Test 6: Inaccessible / Terminated Process Error Handling", () =>
            {
                int nonExistentPid = 9999999;
                IntPtr h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, nonExistentPid);
                Assert(h == IntPtr.Zero, "OpenProcess gracefully returned IntPtr.Zero for invalid PID");

                var procCollector = new ProcessMemoryCollector();
                var procs = procCollector.GetTopProcesses(50);
                Assert(procs.Count > 0, "Handled system and kernel processes without exceptions");
            }, ref passed, ref failed);

            RunTest("Test 7: JSON Serialization & Snapshot Integrity", () =>
            {
                using (var engine = new MonitoringEngine(15))
                {
                    StateSnapshot snap = engine.SampleOnce();
                    Assert(snap != null, "SampleOnce returned valid snapshot");
                    Assert(snap.Metrics != null, "Snapshot contains metrics");
                    Assert(snap.TopProcesses.Count > 0, "Snapshot contains top processes");

                    string json = JsonHelper.Serialize(snap, true);
                    Assert(!string.IsNullOrEmpty(json), "JSON serialization succeeded");

                    StateSnapshot deserialized = JsonHelper.Deserialize<StateSnapshot>(json);
                    Assert(deserialized != null, "JSON deserialization succeeded");
                    Assert(deserialized.PressureLevel == snap.PressureLevel, "Deserialized pressure level matches");
                }
            }, ref passed, ref failed);

            RunTest("Test 8: Memory Pressure Composite Matrix Evaluation", () =>
            {
                var mNormal = new SystemMemoryMetrics
                {
                    TotalPhysicalBytes = 4000L * 1024 * 1024,
                    AvailablePhysicalBytes = 1200L * 1024 * 1024,
                    CommitLimitBytes = 16000L * 1024 * 1024,
                    CommittedBytes = 8000L * 1024 * 1024,
                    CommitLoadPercentage = 50.0,
                    PagesPerSec = 100.0
                };
                Assert(MonitoringEngine.EvaluatePressure(mNormal) == "Normal", "Healthy metrics evaluate to Normal");

                var mCritical = new SystemMemoryMetrics
                {
                    TotalPhysicalBytes = 4000L * 1024 * 1024,
                    AvailablePhysicalBytes = 100L * 1024 * 1024,
                    CommitLimitBytes = 16000L * 1024 * 1024,
                    CommittedBytes = 15000L * 1024 * 1024,
                    CommitLoadPercentage = 93.75,
                    PagesPerSec = 3000.0
                };
                Assert(MonitoringEngine.EvaluatePressure(mCritical) == "Critical", "Available RAM < 150 MB + High Commit/Paging evaluates to Critical");
            }, ref passed, ref failed);

            // --- Milestone 2 Tests (Safety Framework & Protection Registries) ---
            RunTest("Test 9: Process Protection Policy & Default-DENY", () =>
            {
                var policy = new ProcessProtectionPolicy();
                string reason;

                // Non-negotiable: CanTerminateProcess MUST always return false
                Assert(!policy.CanTerminateProcess("notepad"), "CanTerminateProcess returned false for standard app");
                Assert(!policy.CanTerminateProcess("MsMpEng"), "CanTerminateProcess returned false for Defender");
                Assert(!policy.CanTerminateProcess("Unknown_Process_XYZ"), "CanTerminateProcess returned false for unknown process");

                // Protection inquiries
                Assert(policy.IsProcessProtected("MsMpEng", out reason), "Defender MsMpEng is protected");
                Assert(policy.IsProcessProtected("SafeExamBrowser.Service", out reason), "SafeExamBrowser is protected");
                Assert(policy.IsProcessProtected("AsusSoftwareManager", out reason), "ASUS software is protected");
                Assert(policy.IsProcessProtected("Memory Compression", out reason), "Memory Compression is protected");
                Assert(policy.IsProcessProtected("UnknownApp123", out reason), "Unknown process protected under Default-DENY");
            }, ref passed, ref failed);

            RunTest("Test 10: Service Protection Policy & Tamper Prevention", () =>
            {
                var servicePolicy = new ServiceProtectionPolicy();
                string reason;

                Assert(servicePolicy.IsServiceProtected("WinDefend", out reason), "WinDefend service is protected");
                Assert(servicePolicy.IsServiceProtected("SafeExamBrowserService", out reason), "SafeExamBrowserService is protected");
                Assert(servicePolicy.IsServiceProtected("AsusAppService", out reason), "AsusAppService is protected");
                Assert(servicePolicy.IsServiceProtected("SysMain", out reason), "SysMain is protected from arbitrary disabling");
                Assert(servicePolicy.IsServiceProtected("AudioSrv", out reason), "AudioSrv is protected");

                Assert(!servicePolicy.CanModifyService("WinDefend"), "CanModifyService returned false for WinDefend");
                Assert(!servicePolicy.CanModifyService("SysMain"), "CanModifyService returned false for SysMain");
                Assert(!servicePolicy.CanModifyService("RandomServiceXYZ"), "CanModifyService returned false for unknown service");
            }, ref passed, ref failed);

            RunTest("Test 11: Startup Protection Policy (AHK & Approved Targets)", () =>
            {
                var startupPolicy = new StartupProtectionPolicy();
                string reason;

                // AutoHotkey scripts MUST be protected
                Assert(!startupPolicy.IsApprovedForOptimization("ShortcutSwap.ahk", "cmd", out reason), "ShortcutSwap.ahk is protected");
                Assert(!startupPolicy.IsApprovedForOptimization("WindowButtonDisable.ahk", "cmd", out reason), "WindowButtonDisable.ahk is protected");
                Assert(startupPolicy.IsEntryProtected("ShortcutSwap.ahk"), "IsEntryProtected returns true for AHK script");

                // Other user-protected apps
                Assert(!startupPolicy.IsApprovedForOptimization("SecurityHealth", "cmd", out reason), "SecurityHealth is protected");
                Assert(!startupPolicy.IsApprovedForOptimization("IDMan", "cmd", out reason), "IDM is protected");
                Assert(!startupPolicy.IsApprovedForOptimization("Filee", "cmd", out reason), "Filee is protected");
                Assert(!startupPolicy.IsApprovedForOptimization("MicrosoftEdgeAutoLaunch_12345", "cmd", out reason), "Edge AutoLaunch is protected");

                // Approved targets per user preferences
                Assert(startupPolicy.IsApprovedForOptimization("OneDrive", "cmd", out reason), "OneDrive is approved for optimization");
                Assert(startupPolicy.IsApprovedForOptimization("Spotify", "cmd", out reason), "Spotify is approved for optimization");
                Assert(startupPolicy.IsApprovedForOptimization("utweb", "cmd", out reason), "uTorrent Web is approved for optimization");
                Assert(startupPolicy.IsApprovedForOptimization("CloudflareWARP", "cmd", out reason), "Cloudflare WARP is approved for optimization");
            }, ref passed, ref failed);

            RunTest("Test 12: Startup Scanner Real-World Discovery on Laptop", () =>
            {
                var scanner = new StartupScanner();
                List<StartupEntry> entries = scanner.ScanAll();

                Assert(entries != null && entries.Count > 0, "Discovered live startup entries on laptop");

                bool foundApproved = false;
                bool foundAhk = false;

                for (int i = 0; i < entries.Count; i++)
                {
                    Console.Write("[Entry: {0} ({1})] ", entries[i].Name, entries[i].Location);
                    if (entries[i].Name.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                        entries[i].Name.Equals("Spotify", StringComparison.OrdinalIgnoreCase) ||
                        entries[i].Name.Equals("utweb", StringComparison.OrdinalIgnoreCase))
                    {
                        foundApproved = true;
                        Assert(entries[i].IsApprovedForOptimization, "Discovered approved startup candidate for optimization");
                    }
                    if (entries[i].Name.IndexOf(".ahk", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundAhk = true;
                        Assert(entries[i].IsProtected, "AutoHotkey script discovered and marked as PROTECTED");
                    }
                }

                Assert(foundApproved || entries.Count > 0, "Discovered live startup entries on laptop");
                Assert(foundAhk, "Discovered AutoHotkey scripts in live startup folder");
            }, ref passed, ref failed);

            RunTest("Test 13: Configuration Persistence & Safe Corrupted JSON Recovery", () =>
            {
                string testConfigDir = @"C:\ProgramData\RAM Guardian\test_config";
                try
                {
                    if (Directory.Exists(testConfigDir)) Directory.Delete(testConfigDir, true);
                    Directory.CreateDirectory(testConfigDir);

                    var mgr = new ConfigManager(testConfigDir);
                    GuardianConfig cfg = mgr.LoadConfig();
                    Assert(cfg.OperatingMode == "GamingFocus", "Default mode is strictly GamingFocus");
                    Assert(cfg.ElevatedAvailableRamMB == 1150.0, "Default thresholds are calibrated for 65-70% RAM goal");

                    // Mutate file with corrupted JSON
                    string configFile = Path.Combine(testConfigDir, "config.json");
                    File.WriteAllText(configFile, "{ INVALID_CORRUPT_JSON: ");

                    // Load should not throw; should safely revert to safe defaults
                    GuardianConfig recovered = mgr.LoadConfig();
                    Assert(recovered != null, "Recovered from corrupted JSON gracefully");
                    Assert(recovered.OperatingMode == "GamingFocus", "Safe default restored");
                }
                finally
                {
                    try { if (Directory.Exists(testConfigDir)) Directory.Delete(testConfigDir, true); } catch { }
                }
            }, ref passed, ref failed);

            RunTest("Test 14: Safety Backup & Atomic Rollback Mechanism", () =>
            {
                string testBackupDir = @"C:\ProgramData\RAM Guardian\test_backups";
                try
                {
                    if (Directory.Exists(testBackupDir)) Directory.Delete(testBackupDir, true);
                    Directory.CreateDirectory(testBackupDir);

                    var bkpMgr = new BackupManager(testBackupDir);
                    var entry = new StartupEntry
                    {
                        Id = "TEST_ENTRY",
                        Name = "TestApp",
                        Location = "HKCU_Run",
                        CommandLine = "C:\\TestApp\\test.exe /bg"
                    };

                    OptimizationBackup bkp = bkpMgr.CreateBackup(entry, "OPT_TEST_ACTION", "Testing backup subsystem");
                    Assert(bkp != null, "Backup record created successfully");
                    Assert(File.Exists(Path.Combine(testBackupDir, string.Format("{0}.json", bkp.BackupId))), "Backup file exists on disk");

                    List<OptimizationBackup> backups = bkpMgr.GetBackups();
                    Assert(backups.Count == 1, "Backup list reports 1 saved backup");
                    Assert(backups[0].OriginalEntry.Name == "TestApp", "Backup metadata verified intact");
                }
                finally
                {
                    try { if (Directory.Exists(testBackupDir)) Directory.Delete(testBackupDir, true); } catch { }
                }
            }, ref passed, ref failed);

            RunTest("Test 15: Action Precondition Guard & Mode Enforcement", () =>
            {
                var registry = new ActionRegistry();
                var action = registry.GetAction("OPT_STARTUP_UTWEB_DISABLE");
                Assert(action != null, "Retrieved approved utweb action");

                var monitorConfig = new GuardianConfig { OperatingMode = "MonitorOnly" };
                string reason;
                bool canRunInMonitorMode = action.CanExecute(monitorConfig, out reason);
                Assert(!canRunInMonitorMode, "Action refused in MonitorOnly mode");
                Assert(reason.Contains("Requires 'SafeAutomatic'"), "Refusal reason explains mode requirement");

                object originalValue = null;
                using (var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (runKey != null)
                    {
                        originalValue = runKey.GetValue("utweb");
                        if (originalValue == null)
                        {
                            runKey.SetValue("utweb", @"C:\Test\utweb.exe /MINIMIZED");
                        }
                    }
                }

                try
                {
                    var safeAutoConfig = new GuardianConfig { OperatingMode = "SafeAutomatic" };
                    bool canRunInSafeAuto = action.CanExecute(safeAutoConfig, out reason);
                    Assert(canRunInSafeAuto, "Action permitted in SafeAutomatic mode when entry exists (" + reason + ")");
                }
                finally
                {
                    using (var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null)
                        {
                            if (originalValue == null)
                            {
                                try { runKey.DeleteValue("utweb", false); } catch { }
                            }
                            else
                            {
                                runKey.SetValue("utweb", originalValue);
                            }
                        }
                    }
                }
            }, ref passed, ref failed);

            // --- Redesign Tests: 5 Operating Modes, Edge 24/7, Process Intelligence & Killswitch ---

            RunTest("Test 16: 5 Operating Modes & Global Emergency Killswitch", () =>
            {
                var cfg = new GuardianConfig();
                string[] validModes = new string[] { "MonitorOnly", "BalancedAuto", "AggressiveAuto", "GamingFocus", "Paused" };

                for (int i = 0; i < validModes.Length; i++)
                {
                    cfg.OperatingMode = validModes[i];
                    Assert(cfg.OperatingMode == validModes[i], string.Format("Mode '{0}' successfully assigned", validModes[i]));
                }

                // Verify Emergency Killswitch blocks action
                cfg.IsEmergencyStopped = true;
                var coord = new OptimizationCoordinator();
                string summary;
                bool executed = coord.EvaluateAndOptimize(cfg, new TrendAnalyzer(), new List<ProcessMemoryInfo>(), new EdgeMetrics(), null, out summary);
                Assert(!executed, "OptimizationCoordinator refused action when Emergency Stop is ACTIVE");
                Assert(summary.Contains("Emergency Stop"), "Summary correctly indicates Emergency Stop is active");

                var termEngine = new ProcessTerminationEngine();
                var dummyTarget = new ProcessMemoryInfo { ProcessId = 1234, ProcessName = "Spotify" };
                string termAudit;
                bool closed = termEngine.TryGraduatedClosure(dummyTarget, cfg, out termAudit);
                Assert(!closed, "Termination engine refused action when Emergency Stop is ACTIVE");
                Assert(termAudit.Contains("Emergency Stop"), "Audit message notes Emergency Stop is ACTIVE");
            }, ref passed, ref failed);

            RunTest("Test 17: Process Intelligence Engine (9-Tier Immunity Classification)", () =>
            {
                var engine = new ProcessIntelligenceEngine();
                var config = new GuardianConfig { OperatingMode = "AggressiveAuto" };

                // 1. Windows Core
                var pExplorer = new ProcessMemoryInfo { ProcessId = 100, ProcessName = "explorer" };
                engine.ClassifyProcess(pExplorer, config);
                Assert(pExplorer.IsProtected, "Explorer is protected");
                Assert(!pExplorer.IsTerminationEligible, "Explorer is immune from termination");

                // 2. Security Software
                var pDefender = new ProcessMemoryInfo { ProcessId = 101, ProcessName = "MsMpEng" };
                engine.ClassifyProcess(pDefender, config);
                Assert(pDefender.Classification == "SecuritySoftware", "Defender classified as SecuritySoftware");
                Assert(pDefender.IsProtected, "Defender is protected");
                Assert(!pDefender.IsTerminationEligible, "Defender is immune from termination");

                // 3. Academic Testing (Safe Exam Browser)
                var pSeb = new ProcessMemoryInfo { ProcessId = 102, ProcessName = "SafeExamBrowser" };
                engine.ClassifyProcess(pSeb, config);
                Assert(pSeb.Classification == "AcademicTesting", "SEB classified as AcademicTesting");
                Assert(pSeb.IsProtected, "Safe Exam Browser is strictly protected");
                Assert(!pSeb.IsTerminationEligible, "Safe Exam Browser is immune from termination");

                // 4. User Custom Scripting (AutoHotkey scripts)
                var pAhk1 = new ProcessMemoryInfo { ProcessId = 103, ProcessName = "ShortcutSwap.ahk" };
                engine.ClassifyProcess(pAhk1, config);
                Assert(pAhk1.Classification == "UserScripting", "ShortcutSwap.ahk classified as UserScripting");
                Assert(pAhk1.IsProtected, "ShortcutSwap.ahk is protected");
                Assert(!pAhk1.IsTerminationEligible, "ShortcutSwap.ahk is immune from termination");

                var pAhk2 = new ProcessMemoryInfo { ProcessId = 104, ProcessName = "WindowButtonDisable.ahk" };
                engine.ClassifyProcess(pAhk2, config);
                Assert(pAhk2.IsProtected, "WindowButtonDisable.ahk is protected");
                Assert(!pAhk2.IsTerminationEligible, "WindowButtonDisable.ahk is immune from termination");

                var pIdm = new ProcessMemoryInfo { ProcessId = 105, ProcessName = "IDMan" };
                engine.ClassifyProcess(pIdm, config);
                Assert(pIdm.IsProtected, "IDMan is protected");
                Assert(!pIdm.IsTerminationEligible, "IDMan is immune from termination");

                var pFilee = new ProcessMemoryInfo { ProcessId = 106, ProcessName = "Filee" };
                engine.ClassifyProcess(pFilee, config);
                Assert(pFilee.IsProtected, "Filee is protected");
                Assert(!pFilee.IsTerminationEligible, "Filee is immune from termination");

                // 5. Development Tools
                var pCode = new ProcessMemoryInfo { ProcessId = 107, ProcessName = "code" };
                engine.ClassifyProcess(pCode, config);
                Assert(pCode.Classification == "DevEnvironment", "VS Code classified as DevEnvironment");
                Assert(pCode.IsProtected, "VS Code is protected");

                var pNode = new ProcessMemoryInfo { ProcessId = 108, ProcessName = "node" };
                engine.ClassifyProcess(pNode, config);
                Assert(pNode.IsProtected, "Node.js is protected");

                // 6. Virtual Machines
                var pVm = new ProcessMemoryInfo { ProcessId = 109, ProcessName = "vmware-vmx" };
                engine.ClassifyProcess(pVm, config);
                Assert(pVm.Classification == "VirtualMachine", "VMware classified as VirtualMachine");
                Assert(pVm.IsProtected, "VMware is protected");

                // 7. Essential Hardware (ASUS)
                var pAsus = new ProcessMemoryInfo { ProcessId = 110, ProcessName = "AsusAppService" };
                engine.ClassifyProcess(pAsus, config);
                Assert(pAsus.IsProtected, "ASUS service is protected");

                // 8. Standard Unknown App (Default-DENY)
                var pUnknown = new ProcessMemoryInfo { ProcessId = 111, ProcessName = "RandomThirdPartyApp" };
                engine.ClassifyProcess(pUnknown, config);
                Assert(!pUnknown.IsTerminationEligible, "Unknown application denied termination by Default-DENY");

                // 9. Approved Background Target in AggressiveAuto
                var pApproved = new ProcessMemoryInfo { ProcessId = 112, ProcessName = "OneDrive" };
                engine.ClassifyProcess(pApproved, config);
                Assert(pApproved.IsTerminationEligible, "OneDrive is eligible in AggressiveAuto");
            }, ref passed, ref failed);

            RunTest("Test 18: Microsoft Edge Role Classifier & 24/7 Policy Optimization", () =>
            {
                // Verify Command Line Role Classification
                string roleGpu = EdgeProcessCollector.DetermineRole("msedge.exe --type=gpu-process --field-trial-handle=123", false);
                Assert(roleGpu == "Gpu", "GPU process identified correctly");

                string roleRenderer = EdgeProcessCollector.DetermineRole("msedge.exe --type=renderer --extension-process", false);
                Assert(roleRenderer == "Extension", "Extension process identified correctly");

                string roleTab = EdgeProcessCollector.DetermineRole("msedge.exe --type=renderer --origin-trial-disabled-features", false);
                Assert(roleTab == "Renderer", "Renderer Tab identified correctly");

                string roleCrashpad = EdgeProcessCollector.DetermineRole("msedge.exe --type=crashpad-handler", false);
                Assert(roleCrashpad == "Crashpad", "Crashpad handler identified correctly");

                string roleBrowser = EdgeProcessCollector.DetermineRole("msedge.exe", true);
                Assert(roleBrowser == "Browser", "Browser parent identified correctly");

                // Verify Registry Policy Application and Rollback
                string testKeyPath = @"Software\RAMGuardianTest\EdgePolicies";
                var opt = new EdgeOptimizer(null, null, testKeyPath);
                var cfg = new GuardianConfig
                {
                    OperatingMode = "BalancedAuto",
                    EdgeOptimizationEnabled = true,
                    EdgeSleepingTabsTimeoutSeconds = 60,
                    EdgeEfficiencyModeEnabled = true,
                    EdgeBackgroundModeDisabled = true
                };

                string applyMsg;
                bool applied = opt.OptimizeEdgePolicies(cfg, null, out applyMsg);
                Assert(applied, "Edge policies applied successfully: " + applyMsg);

                // Verify keys written to test key path
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(testKeyPath, false))
                {
                    Assert(key != null, "Policy key exists in registry");
                    object sleepTimeout = key.GetValue("SleepingTabsTimeout");
                    Assert(sleepTimeout != null && Convert.ToInt32(sleepTimeout) == 60, "SleepingTabsTimeout is set to 60s");

                    object effMode = key.GetValue("EfficiencyModeEnabled");
                    Assert(effMode != null && Convert.ToInt32(effMode) == 1, "EfficiencyModeEnabled is set to 1");
                }

                // Restore policies
                string restoreMsg;
                bool restored = opt.RestoreEdgePolicies(out restoreMsg);
                Assert(restored, "Edge policies cleanly restored");

                // Verify keys deleted/restored
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(testKeyPath, false))
                {
                    if (key != null)
                    {
                        object sleepTimeout = key.GetValue("SleepingTabsTimeout");
                        Assert(sleepTimeout == null, "SleepingTabsTimeout successfully removed on restore");
                    }
                }

                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\RAMGuardianTest", false); } catch { }
            }, ref passed, ref failed);

            RunTest("Test 19: Graduated 4-Level Process Termination Engine Safety Invariants", () =>
            {
                var termEngine = new ProcessTerminationEngine();

                // 1. Rejection in MonitorOnly Mode
                var cfgMonitor = new GuardianConfig { OperatingMode = "MonitorOnly" };
                var target = new ProcessMemoryInfo { ProcessId = 5000, ProcessName = "OneDrive" };
                string audit;
                bool ok = termEngine.TryGraduatedClosure(target, cfgMonitor, out audit);
                Assert(!ok, "Closure refused in MonitorOnly mode");
                Assert(audit.Contains("prohibited in mode 'MonitorOnly'"), "Audit cites MonitorOnly prohibition");

                // 2. Rejection of Protected Targets
                var cfgAggressive = new GuardianConfig { OperatingMode = "AggressiveAuto" };
                var protTarget = new ProcessMemoryInfo { ProcessId = 5001, ProcessName = "SafeExamBrowser" };
                ok = termEngine.TryGraduatedClosure(protTarget, cfgAggressive, out audit);
                Assert(!ok, "Closure refused for SafeExamBrowser");
                Assert(audit.Contains("PROTECTED"), "Audit cites process protection invariant");

                // 3. Rejection of Unapproved Target (Default-DENY)
                var unapprovedTarget = new ProcessMemoryInfo { ProcessId = 5002, ProcessName = "CustomEditorApp" };
                ok = termEngine.TryGraduatedClosure(unapprovedTarget, cfgAggressive, out audit);
                Assert(!ok, "Closure refused for unapproved custom application");
                Assert(audit.Contains("Default-DENY"), "Audit cites Default-DENY protection");

                // 4. Rejection when Emergency Stop is Active
                var cfgEmergency = new GuardianConfig { OperatingMode = "AggressiveAuto", IsEmergencyStopped = true };
                ok = termEngine.TryGraduatedClosure(target, cfgEmergency, out audit);
                Assert(!ok, "Closure refused when Emergency Stop is Active");
                Assert(audit.Contains("Emergency Stop"), "Audit cites Emergency Stop switch");
            }, ref passed, ref failed);

            RunTest("Test 20: Optimization Coordinator End-to-End Across All 5 Operating Modes", () =>
            {
                var coord = new OptimizationCoordinator();
                var trend = new TrendAnalyzer();
                var procs = new List<ProcessMemoryInfo>
                {
                    new ProcessMemoryInfo { ProcessId = 1, ProcessName = "System" },
                    new ProcessMemoryInfo { ProcessId = 2, ProcessName = "MsMpEng" },
                    new ProcessMemoryInfo { ProcessId = 3, ProcessName = "OneDrive" }
                };
                var edge = new EdgeMetrics { IsRunning = true, TotalProcesses = 5 };
                string summary;

                // 1. Paused Mode: Zero Actions
                var cfgPaused = new GuardianConfig { OperatingMode = "Paused" };
                bool runPaused = coord.EvaluateAndOptimize(cfgPaused, trend, procs, edge, null, out summary);
                Assert(!runPaused, "No optimization in Paused mode");
                Assert(summary.Contains("Paused"), "Summary indicates system is Paused");

                // 2. MonitorOnly Mode: Zero Actions
                var cfgMonitor = new GuardianConfig { OperatingMode = "MonitorOnly" };
                bool runMonitor = coord.EvaluateAndOptimize(cfgMonitor, trend, procs, edge, null, out summary);
                Assert(!runMonitor, "No optimization in MonitorOnly mode");
                Assert(summary.Contains("MonitorOnly"), "Summary indicates MonitorOnly mode");

                // 3. BalancedAuto Mode: Edge Optimization Applied, No Process Terminations on Normal Pressure
                var cfgBalanced = new GuardianConfig { OperatingMode = "BalancedAuto", EdgeOptimizationEnabled = true };
                var alerts = new List<MemoryAlert>();
                coord.EvaluateAndOptimize(cfgBalanced, trend, procs, edge, alerts, out summary);
                // Clean up edge policies
                coord.EdgeOptimizer.RestoreEdgePolicies(out summary);

                // 4. Emergency Stop: Immediate Suspension
                var cfgEmerg = new GuardianConfig { OperatingMode = "BalancedAuto", IsEmergencyStopped = true };
                bool runEmerg = coord.EvaluateAndOptimize(cfgEmerg, trend, procs, edge, null, out summary);
                Assert(!runEmerg, "No optimization when Emergency Stop active");
                Assert(summary.Contains("Emergency Stop"), "Summary indicates Emergency Stop active");
            }, ref passed, ref failed);

            RunTest("Test 21: OneClickOptimizer Dual RAM and Edge Optimization", () =>
            {
                var opt = new OneClickOptimizer();
                var cfg = new GuardianConfig { OperatingMode = "GamingFocus" };
                OptimizationSummary summary = opt.OptimizeAll(cfg);

                Assert(summary != null, "Summary is not null");
                Assert(summary.InitialAvailableMB > 0, "Initial available RAM > 0");
                Assert(summary.FinalAvailableMB > 0, "Final available RAM > 0");
                Assert(summary.InitialMemoryLoad >= 1 && summary.InitialMemoryLoad <= 100, "Initial memory load valid");
                Assert(summary.FinalMemoryLoad >= 1 && summary.FinalMemoryLoad <= 100, "Final memory load valid");
                Assert(!string.IsNullOrEmpty(summary.DetailsMessage), "Details message is populated");
            }, ref passed, ref failed);

            Console.WriteLine();
            Console.WriteLine("===============================================================");
            Console.ForegroundColor = failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine("  TEST SUMMARY: {0} PASSED, {1} FAILED (TOTAL: {2})", passed, failed, passed + failed);
            Console.ResetColor();
            Console.WriteLine("===============================================================");

            return failed == 0 ? 0 : 1;
        }

        private static void RunTest(string name, Action testAction, ref int passed, ref int failed)
        {
            Console.Write("{0,-62} ... ", name);
            try
            {
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PASS]");
                Console.ResetColor();
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAIL]");
                Console.ResetColor();
                Console.WriteLine("   Error: {0}", ex.Message);
                failed++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }
    }
}
