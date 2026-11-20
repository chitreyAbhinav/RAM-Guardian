using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Native;

namespace RAMGuardian.Monitoring
{
    public class ProcessMemoryCollector
    {
        private static readonly HashSet<string> KnownProtectedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass",
            "svchost", "dwm", "explorer", "sihost", "taskhostw", "RuntimeBroker",
            "MsMpEng", "NisSrv", "SecurityHealthService", "SecurityHealthSystray",
            "Memory Compression", "SafeExamBrowser", "SafeExamBrowser.Client", "SafeExamBrowser.Service",
            "AsusAppService", "AsusLinkNear", "AsusLinkRemote", "AsusSoftwareManager", "AsusSystemAnalysis", "AsusSystemDiagnosis"
        };

        public List<ProcessMemoryInfo> GetTopProcesses(int count = 20, bool sortByPrivateBytes = false)
        {
            var results = new List<ProcessMemoryInfo>();
            Process[] allProcesses = null;

            try
            {
                allProcesses = Process.GetProcesses();
            }
            catch
            {
                return results;
            }

            for (int i = 0; i < allProcesses.Length; i++)
            {
                Process p = allProcesses[i];
                try
                {
                    int pid = p.Id;
                    string name = p.ProcessName;

                    long workingSet = 0;
                    long privateBytes = 0;
                    string title = string.Empty;
                    bool hasWindow = false;
                    DateTime? startTime = null;

                    // Standard .NET process queries
                    try { workingSet = p.WorkingSet64; } catch { }
                    try { privateBytes = p.PrivateMemorySize64; } catch { }
                    try
                    {
                        title = p.MainWindowTitle;
                        hasWindow = !string.IsNullOrEmpty(title) || p.MainWindowHandle != IntPtr.Zero;
                    }
                    catch { }

                    try { startTime = p.StartTime; } catch { }

                    // Enhance with native K32GetProcessMemoryInfo for exact PrivateUsage
                    if (pid > 4)
                    {
                        IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                        if (hProcess != IntPtr.Zero)
                        {
                            try
                            {
                                NativeMethods.PROCESS_MEMORY_COUNTERS_EX counters;
                                int cb = Marshal.SizeOf(typeof(NativeMethods.PROCESS_MEMORY_COUNTERS_EX));
                                if (NativeMethods.K32GetProcessMemoryInfo(hProcess, out counters, cb))
                                {
                                    if (counters.WorkingSetSize.ToInt64() > 0)
                                    {
                                        workingSet = counters.WorkingSetSize.ToInt64();
                                    }
                                    if (counters.PrivateUsage.ToInt64() > 0)
                                    {
                                        privateBytes = counters.PrivateUsage.ToInt64();
                                    }
                                }
                            }
                            finally
                            {
                                NativeMethods.CloseHandle(hProcess);
                            }
                        }
                    }

                    var info = new ProcessMemoryInfo
                    {
                        ProcessId = pid,
                        ProcessName = name,
                        WorkingSetBytes = workingSet,
                        PrivateBytes = privateBytes,
                        MainWindowTitle = title ?? string.Empty,
                        HasWindow = hasWindow,
                        StartTime = startTime,
                        IsProtected = IsProcessProtected(name)
                    };

                    if (info.IsProtected)
                    {
                        info.ProtectionReason = "Essential Windows, Security, or Hardware Component";
                    }

                    results.Add(info);
                }
                catch
                {
                    // Catch process exit or permission denied safely
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            // Sort
            if (sortByPrivateBytes)
            {
                results.Sort((a, b) => b.PrivateBytes.CompareTo(a.PrivateBytes));
            }
            else
            {
                results.Sort((a, b) => b.WorkingSetBytes.CompareTo(a.WorkingSetBytes));
            }

            if (results.Count > count)
            {
                return results.GetRange(0, count);
            }

            return results;
        }

        public static bool IsProcessProtected(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return true; // Deny unknown by default
            return KnownProtectedProcesses.Contains(processName);
        }
    }
}
