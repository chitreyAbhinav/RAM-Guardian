using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Native;

namespace RAMGuardian.Monitoring
{
    public class EdgeProcessCollector
    {
        public Tuple<EdgeMetrics, List<EdgeProcessInfo>> CollectEdgeIntelligence()
        {
            var metrics = new EdgeMetrics();
            var processes = new List<EdgeProcessInfo>();

            Process[] edgeProcs = null;
            try
            {
                edgeProcs = Process.GetProcessesByName("msedge");
            }
            catch
            {
                return new Tuple<EdgeMetrics, List<EdgeProcessInfo>>(metrics, processes);
            }

            if (edgeProcs == null || edgeProcs.Length == 0)
            {
                metrics.IsRunning = false;
                metrics.StatusSummary = "Microsoft Edge is not currently running.";
                return new Tuple<EdgeMetrics, List<EdgeProcessInfo>>(metrics, processes);
            }

            metrics.IsRunning = true;
            metrics.TotalProcesses = edgeProcs.Length;

            // Collect command lines via fast WMI query for msedge.exe
            var cmdLineMap = QueryEdgeCommandLines();

            long totalWorkingSet = 0;
            long totalPrivate = 0;
            long gpuWorkingSet = 0;
            long browserWorkingSet = 0;
            long rendererWorkingSet = 0;
            long extensionWorkingSet = 0;

            int rendererCount = 0;
            int extensionCount = 0;

            for (int i = 0; i < edgeProcs.Length; i++)
            {
                Process p = edgeProcs[i];
                try
                {
                    int pid = p.Id;
                    long ws = 0;
                    long priv = 0;

                    try { ws = p.WorkingSet64; } catch { }
                    try { priv = p.PrivateMemorySize64; } catch { }

                    // Enhance with native K32GetProcessMemoryInfo
                    IntPtr h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                    if (h != IntPtr.Zero)
                    {
                        try
                        {
                            NativeMethods.PROCESS_MEMORY_COUNTERS_EX cnt;
                            int cb = Marshal.SizeOf(typeof(NativeMethods.PROCESS_MEMORY_COUNTERS_EX));
                            if (NativeMethods.K32GetProcessMemoryInfo(h, out cnt, cb))
                            {
                                if (cnt.WorkingSetSize.ToInt64() > 0) ws = cnt.WorkingSetSize.ToInt64();
                                if (cnt.PrivateUsage.ToInt64() > 0) priv = cnt.PrivateUsage.ToInt64();
                            }
                        }
                        finally
                        {
                            NativeMethods.CloseHandle(h);
                        }
                    }

                    string cmdLine;
                    cmdLineMap.TryGetValue(pid, out cmdLine);
                    if (cmdLine == null) cmdLine = string.Empty;

                    bool hasWindow = false;
                    try { hasWindow = (p.MainWindowHandle != IntPtr.Zero); } catch { }

                    string role = DetermineRole(cmdLine, hasWindow);
                    bool isExtension = role == "Extension";

                    totalWorkingSet += ws;
                    totalPrivate += priv;

                    if (role == "Gpu") gpuWorkingSet += ws;
                    else if (role == "Browser") browserWorkingSet += ws;
                    else if (role == "Renderer")
                    {
                        rendererWorkingSet += ws;
                        rendererCount++;
                    }
                    else if (role == "Extension")
                    {
                        extensionWorkingSet += ws;
                        extensionCount++;
                    }

                    processes.Add(new EdgeProcessInfo
                    {
                        ProcessId = pid,
                        Role = role,
                        WorkingSetBytes = ws,
                        PrivateBytes = priv,
                        CommandLine = cmdLine,
                        IsExtension = isExtension,
                        IsSleepingCandidate = (role == "Renderer" && ws > 30 * 1024 * 1024)
                    });
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            metrics.TotalWorkingSetBytes = totalWorkingSet;
            metrics.TotalPrivateBytes = totalPrivate;
            metrics.RendererProcessesCount = rendererCount;
            metrics.ExtensionProcessesCount = extensionCount;
            metrics.GpuWorkingSetMB = gpuWorkingSet / (1024.0 * 1024.0);
            metrics.BrowserWorkingSetMB = browserWorkingSet / (1024.0 * 1024.0);
            metrics.RenderersWorkingSetMB = rendererWorkingSet / (1024.0 * 1024.0);
            metrics.ExtensionsWorkingSetMB = extensionWorkingSet / (1024.0 * 1024.0);

            metrics.StatusSummary = string.Format("Active: {0} procs ({1:F1} MB RAM, {2:F1} MB Commit) | {3} Tabs, {4} Extensions",
                metrics.TotalProcesses, metrics.TotalWorkingSetMB, metrics.TotalPrivateMB, rendererCount, extensionCount);

            return new Tuple<EdgeMetrics, List<EdgeProcessInfo>>(metrics, processes);
        }

        public static string DetermineRole(string cmdLine, bool hasWindow)
        {
            if (string.IsNullOrEmpty(cmdLine))
            {
                return hasWindow ? "Browser" : "Utility";
            }

            if (cmdLine.IndexOf("--type=gpu-process", StringComparison.OrdinalIgnoreCase) >= 0) return "Gpu";
            if (cmdLine.IndexOf("--extension-process", StringComparison.OrdinalIgnoreCase) >= 0) return "Extension";
            if (cmdLine.IndexOf("--type=renderer", StringComparison.OrdinalIgnoreCase) >= 0) return "Renderer";
            if (cmdLine.IndexOf("utility-sub-type=network", StringComparison.OrdinalIgnoreCase) >= 0) return "NetworkService";
            if (cmdLine.IndexOf("utility-sub-type=storage", StringComparison.OrdinalIgnoreCase) >= 0) return "StorageService";
            if (cmdLine.IndexOf("utility-sub-type=audio", StringComparison.OrdinalIgnoreCase) >= 0) return "AudioService";
            if (cmdLine.IndexOf("--type=crashpad-handler", StringComparison.OrdinalIgnoreCase) >= 0) return "Crashpad";
            if (cmdLine.IndexOf("--type=utility", StringComparison.OrdinalIgnoreCase) >= 0) return "Utility";

            return "Browser";
        }

        private static Dictionary<int, string> QueryEdgeCommandLines()
        {
            var map = new Dictionary<int, string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'msedge.exe'"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        try
                        {
                            object pidObj = mo["ProcessId"];
                            object cmdObj = mo["CommandLine"];
                            if (pidObj != null && cmdObj != null)
                            {
                                int pid = Convert.ToInt32(pidObj);
                                map[pid] = cmdObj.ToString();
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return map;
        }
    }
}
