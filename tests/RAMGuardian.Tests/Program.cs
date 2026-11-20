using System;
using System.Collections.Generic;
using System.Diagnostics;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Native;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Monitoring;
using RAMGuardian.Service;

namespace RAMGuardian.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("===============================================================");
            Console.WriteLine("          RAM GUARDIAN - AUTOMATED TEST SUITE (M1)            ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();

            int passed = 0;
            int failed = 0;

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

                // Verify sort order
                for (int i = 0; i < procs.Count - 1; i++)
                {
                    Assert(procs[i].WorkingSetBytes >= procs[i + 1].WorkingSetBytes, "Processes sorted by WorkingSet descending");
                }

                // Verify protection logic
                Assert(ProcessMemoryCollector.IsProcessProtected("MsMpEng"), "Defender MsMpEng is protected");
                Assert(ProcessMemoryCollector.IsProcessProtected("dwm"), "Desktop Window Manager is protected");
                Assert(ProcessMemoryCollector.IsProcessProtected("explorer"), "Windows Explorer is protected");
                Assert(ProcessMemoryCollector.IsProcessProtected("SafeExamBrowser.Service"), "SafeExamBrowser is protected");
                Assert(ProcessMemoryCollector.IsProcessProtected("AsusSoftwareManager"), "ASUS services are protected");
                Assert(ProcessMemoryCollector.IsProcessProtected(null), "Unknown/null process denied (protected by default)");
            }, ref passed, ref failed);

            RunTest("Test 6: Inaccessible / Terminated Process Error Handling", () =>
            {
                // Verify that trying to inspect a non-existent PID never throws
                int nonExistentPid = 9999999;
                IntPtr h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, nonExistentPid);
                Assert(h == IntPtr.Zero, "OpenProcess gracefully returned IntPtr.Zero for invalid PID");

                // Verify ProcessMemoryCollector handles System idle and privileged kernel processes (PID 0, 4)
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
                    Assert(json.Contains("TotalPhysicalBytes"), "JSON contains TotalPhysicalBytes");
                    Assert(json.Contains("PressureLevel"), "JSON contains PressureLevel");

                    // Verify deserialization
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

                var mElevated = new SystemMemoryMetrics
                {
                    TotalPhysicalBytes = 4000L * 1024 * 1024,
                    AvailablePhysicalBytes = 400L * 1024 * 1024,
                    CommitLimitBytes = 16000L * 1024 * 1024,
                    CommittedBytes = 12000L * 1024 * 1024,
                    CommitLoadPercentage = 75.0,
                    PagesPerSec = 500.0
                };
                Assert(MonitoringEngine.EvaluatePressure(mElevated) == "Elevated", "Available RAM < 500 MB evaluates to Elevated");

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
            Console.Write("{0,-55} ... ", name);
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
