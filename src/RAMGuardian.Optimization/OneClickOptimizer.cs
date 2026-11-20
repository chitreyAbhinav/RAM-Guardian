using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Native;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Logging;
using RAMGuardian.Security.Policies;

namespace RAMGuardian.Optimization
{
    public class OptimizationSummary
    {
        public double InitialAvailableMB { get; set; }
        public double FinalAvailableMB { get; set; }
        public double ReclaimedRamMB { get; set; }
        public double InitialMemoryLoad { get; set; }
        public double FinalMemoryLoad { get; set; }
        public double EdgeInitialWorkingSetMB { get; set; }
        public double EdgeFinalWorkingSetMB { get; set; }
        public double EdgeReclaimedMB { get; set; }
        public int EdgeProcessesTrimmed { get; set; }
        public int BackgroundProcessesTrimmed { get; set; }
        public int ApprovedProcessesClosed { get; set; }
        public string DetailsMessage { get; set; }
    }

    public class OneClickOptimizer
    {
        private readonly EdgeOptimizer _edgeOptimizer;
        private readonly ProcessProtectionPolicy _protectionPolicy;
        private readonly FileLogger _logger;

        public OneClickOptimizer(EdgeOptimizer edgeOptimizer = null, ProcessProtectionPolicy protectionPolicy = null, FileLogger logger = null)
        {
            _edgeOptimizer = edgeOptimizer ?? new EdgeOptimizer();
            _protectionPolicy = protectionPolicy ?? new ProcessProtectionPolicy();
            _logger = logger ?? new FileLogger();
        }

        public OptimizationSummary OptimizeAll(GuardianConfig config)
        {
            var summary = new OptimizationSummary();

            // 1. Snapshot Initial Metrics
            var initialStatus = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(initialStatus))
            {
                summary.InitialAvailableMB = initialStatus.ullAvailPhys / (1024.0 * 1024.0);
                summary.InitialMemoryLoad = initialStatus.dwMemoryLoad;
            }

            // 2. Measure initial Edge Working Set
            Process[] edgeProcs = Process.GetProcessesByName("msedge");
            long edgeWsBefore = 0;
            for (int i = 0; i < edgeProcs.Length; i++)
            {
                try { edgeWsBefore += edgeProcs[i].WorkingSet64; } catch { }
            }
            summary.EdgeInitialWorkingSetMB = edgeWsBefore / (1024.0 * 1024.0);

            // 3. Apply Microsoft Edge Performance Policies
            if (config != null)
            {
                string edgePolicyResult;
                _edgeOptimizer.OptimizeEdgePolicies(config, null, out edgePolicyResult);
            }

            // 4. Safely Trim Working Set of Microsoft Edge Processes
            int edgeTrimmed = 0;
            for (int i = 0; i < edgeProcs.Length; i++)
            {
                try
                {
                    IntPtr h = edgeProcs[i].Handle;
                    if (h != IntPtr.Zero)
                    {
                        if (NativeMethods.EmptyWorkingSet(h))
                        {
                            edgeTrimmed++;
                        }
                    }
                }
                catch { }
                finally
                {
                    try { edgeProcs[i].Dispose(); } catch { }
                }
            }
            summary.EdgeProcessesTrimmed = edgeTrimmed;

            // 5. Manage Approved Background Applications (L2 Graceful Closure)
            int closedCount = 0;
            if (config != null && config.ApprovedTerminationTargets != null)
            {
                for (int i = 0; i < config.ApprovedTerminationTargets.Count; i++)
                {
                    string targetName = config.ApprovedTerminationTargets[i];
                    Process[] targets = Process.GetProcessesByName(targetName);
                    for (int j = 0; j < targets.Length; j++)
                    {
                        try
                        {
                            if (!_protectionPolicy.IsSystemOrHardwareProtected(targets[j].ProcessName))
                            {
                                targets[j].CloseMainWindow();
                                if (targets[j].WaitForExit(1500))
                                {
                                    closedCount++;
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            try { targets[j].Dispose(); } catch { }
                        }
                    }
                }
            }
            summary.ApprovedProcessesClosed = closedCount;

            // 6. Safely Trim Inactive Non-Protected Background Processes
            int bgTrimmed = 0;
            Process[] allProcs = Process.GetProcesses();
            for (int i = 0; i < allProcs.Length; i++)
            {
                Process p = allProcs[i];
                try
                {
                    string name = p.ProcessName;
                    // Strict protection check: Never touch system, explorer, defender, ahk, seb, dev tools
                    if (!_protectionPolicy.IsSystemOrHardwareProtected(name) && !name.Equals("msedge", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it has no visible window or is minimized/idle
                        if (p.MainWindowHandle == IntPtr.Zero || string.IsNullOrEmpty(p.MainWindowTitle))
                        {
                            IntPtr h = p.Handle;
                            if (h != IntPtr.Zero)
                            {
                                if (NativeMethods.EmptyWorkingSet(h))
                                {
                                    bgTrimmed++;
                                }
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
            summary.BackgroundProcessesTrimmed = bgTrimmed;

            // 7. Compact .NET Heap and Trim Local Process
            try
            {
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);
                NativeMethods.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }

            // Allow operating system brief moment to update page tables
            Thread.Sleep(300);

            // 8. Snapshot Final Metrics
            var finalStatus = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(finalStatus))
            {
                summary.FinalAvailableMB = finalStatus.ullAvailPhys / (1024.0 * 1024.0);
                summary.FinalMemoryLoad = finalStatus.dwMemoryLoad;
            }

            // 9. Measure final Edge Working Set
            Process[] edgeAfter = Process.GetProcessesByName("msedge");
            long edgeWsAfter = 0;
            for (int i = 0; i < edgeAfter.Length; i++)
            {
                try { edgeWsAfter += edgeAfter[i].WorkingSet64; } catch { }
                try { edgeAfter[i].Dispose(); } catch { }
            }
            summary.EdgeFinalWorkingSetMB = edgeWsAfter / (1024.0 * 1024.0);
            summary.EdgeReclaimedMB = Math.Max(0.0, summary.EdgeInitialWorkingSetMB - summary.EdgeFinalWorkingSetMB);

            summary.ReclaimedRamMB = Math.Max(0.0, summary.FinalAvailableMB - summary.InitialAvailableMB);
            if (summary.ReclaimedRamMB <= 0.0 && summary.EdgeReclaimedMB > 0.0)
            {
                summary.ReclaimedRamMB = summary.EdgeReclaimedMB;
            }

            summary.DetailsMessage = string.Format(
                "Optimization complete! Reclaimed {0:F1} MB RAM. Edge reduced by {1:F1} MB across {2} processes. Trimmed {3} background processes. New RAM Load: {4:F0}%.",
                summary.ReclaimedRamMB, summary.EdgeReclaimedMB, summary.EdgeProcessesTrimmed, summary.BackgroundProcessesTrimmed, summary.FinalMemoryLoad);

            _logger.Info("ONE_CLICK_OPTIMIZE", summary.DetailsMessage);

            return summary;
        }
    }
}
