using System;
using System.Collections.Generic;
using System.IO;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Native;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Configuration;
using RAMGuardian.Configuration.Models;
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
            Console.WriteLine("        RAM GUARDIAN - AUTOMATED TEST SUITE (M1 + M2)         ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();

            int passed = 0;
            int failed = 0;

            // --- Milestone 1 Tests ---
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

                bool foundOneDrive = false;
                bool foundAhk = false;

                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Name.Equals("OneDrive", StringComparison.OrdinalIgnoreCase))
                    {
                        foundOneDrive = true;
                        Assert(entries[i].IsApprovedForOptimization, "OneDrive discovered as approved for optimization");
                    }
                    if (entries[i].Name.IndexOf(".ahk", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundAhk = true;
                        Assert(entries[i].IsProtected, "AutoHotkey script discovered and marked as PROTECTED");
                    }
                }

                Assert(foundOneDrive, "Discovered OneDrive in live registry");
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
                    Assert(cfg.OperatingMode == "MonitorOnly", "Default mode is strictly MonitorOnly");
                    Assert(cfg.ElevatedAvailableRamMB == 500.0, "Default thresholds are calibrated");

                    // Mutate file with corrupted JSON
                    string configFile = Path.Combine(testConfigDir, "config.json");
                    File.WriteAllText(configFile, "{ INVALID_CORRUPT_JSON: ");

                    // Load should not throw; should safely revert to safe defaults
                    GuardianConfig recovered = mgr.LoadConfig();
                    Assert(recovered != null, "Recovered from corrupted JSON gracefully");
                    Assert(recovered.OperatingMode == "MonitorOnly", "Safe default restored");
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
                var action = registry.GetAction("OPT_STARTUP_ONEDRIVE_DISABLE");
                Assert(action != null, "Retrieved approved OneDrive action");

                var monitorConfig = new GuardianConfig { OperatingMode = "MonitorOnly" };
                string reason;
                bool canRunInMonitorMode = action.CanExecute(monitorConfig, out reason);
                Assert(!canRunInMonitorMode, "Action refused in MonitorOnly mode");
                Assert(reason.Contains("Requires 'SafeAutomatic'"), "Refusal reason explains mode requirement");

                var safeAutoConfig = new GuardianConfig { OperatingMode = "SafeAutomatic" };
                bool canRunInSafeAuto = action.CanExecute(safeAutoConfig, out reason);
                // Can execute should be true since OneDrive exists in live HKCU Run
                Assert(canRunInSafeAuto, "Action permitted in SafeAutomatic mode when entry exists");
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
            Console.Write("{0,-60} ... ", name);
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
