using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Serialization;

namespace RAMGuardian.Service
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                string cmd = args[0].ToLowerInvariant();

                if (cmd == "--help" || cmd == "-h" || cmd == "/?" || cmd == "-help")
                {
                    PrintUsage();
                    return 0;
                }

                if (cmd == "--once" || cmd == "-1")
                {
                    bool jsonOutput = args.Length > 1 && args[1].ToLowerInvariant() == "--json";
                    return RunOnce(jsonOutput);
                }

                if (cmd == "--mode" || cmd == "-m")
                {
                    if (args.Length > 1)
                    {
                        return SetMode(args[1]);
                    }
                    Console.WriteLine("Error: Please specify mode: MonitorOnly, BalancedAuto, AggressiveAuto, GamingFocus, Paused");
                    return 1;
                }

                if (cmd == "--emergency-stop" || cmd == "-stop")
                {
                    return SetEmergencyStop(true);
                }

                if (cmd == "--resume" || cmd == "-resume")
                {
                    return SetEmergencyStop(false);
                }

                if (cmd == "--edge-status" || cmd == "-edge")
                {
                    return ShowEdgeStatus();
                }

                if (cmd == "--edge-restore")
                {
                    return RestoreEdgePolicies();
                }

                if (cmd == "--backups" || cmd == "-b")
                {
                    return ListBackups();
                }

                if (cmd == "--restore-all" || cmd == "-r")
                {
                    return RestoreAllBackups();
                }

                if (cmd == "--console" || cmd == "-c" || cmd == "--monitor")
                {
                    return RunConsole(args);
                }
            }

            // If running non-interactively (started by Windows Service Control Manager)
            if (!Environment.UserInteractive)
            {
                ServiceBase.Run(new RAMGuardianService());
                return 0;
            }

            // If user clicked executable directly or ran without parameters in terminal:
            PrintBanner();
            Console.WriteLine("Interactive terminal detected. Starting live console monitor...");
            Console.WriteLine("Press Ctrl+C or 'Q' to exit at any time.");
            Console.WriteLine();
            return RunConsole(args);
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================");
            Console.WriteLine("   RAM GUARDIAN - Intelligent Windows Resource Monitor v1.0   ");
            Console.WriteLine("   Engineered for 4 GB RAM Windows 11 Systems (ASUS X515EA)    ");
            Console.WriteLine("   Modes: MonitorOnly | BalancedAuto | AggressiveAuto | Gaming ");
            Console.WriteLine("   Edge 24/7 Optimizer: Active | Default-DENY Safety Invariant ");
            Console.WriteLine("===============================================================");
            Console.ResetColor();
        }

        private static void PrintUsage()
        {
            PrintBanner();
            Console.WriteLine("Usage: RAMGuardian.Service.exe [option]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --console, -c             Run in interactive live console mode");
            Console.WriteLine("  --once, -1                Capture a single read-only snapshot and print to terminal");
            Console.WriteLine("  --once --json             Capture a single snapshot and print raw JSON");
            Console.WriteLine("  --mode <Name>             Set operating mode: MonitorOnly, BalancedAuto,");
            Console.WriteLine("                            AggressiveAuto, GamingFocus, Paused");
            Console.WriteLine("  --emergency-stop          Trip the global emergency stop switch");
            Console.WriteLine("  --resume                  Clear the emergency stop switch");
            Console.WriteLine("  --edge-status             Display Microsoft Edge process roles & memory usage");
            Console.WriteLine("  --edge-restore            Restore Edge group policies to browser defaults");
            Console.WriteLine("  --backups, -b             List all configuration backups");
            Console.WriteLine("  --restore-all, -r         Restore all managed system/startup changes");
            Console.WriteLine("  --help, -h                Show this help text");
            Console.WriteLine();
            Console.WriteLine("When started by Windows Service Control Manager, it automatically runs");
            Console.WriteLine("as the background service 'RAMGuardian' without showing any window.");
        }

        private static int RunOnce(bool jsonOutput)
        {
            using (var engine = new MonitoringEngine(15))
            {
                StateSnapshot snapshot = engine.SampleOnce();

                if (jsonOutput)
                {
                    Console.WriteLine(JsonHelper.Serialize(snapshot, true));
                    return 0;
                }

                PrintBanner();
                PrintSnapshotToConsole(snapshot);
                return 0;
            }
        }

        private static int RunConsole(string[] args)
        {
            int interval = 5;
            for (int i = 0; i < args.Length - 1; i++)
            {
                int customInterval;
                if ((args[i] == "-i" || args[i] == "--interval") && int.TryParse(args[i + 1], out customInterval))
                {
                    interval = Math.Max(2, customInterval);
                }
            }

            using (var engine = new MonitoringEngine(interval))
            {
                engine.OnSnapshotGenerated += (snapshot) =>
                {
                    try
                    {
                        Console.Clear();
                        PrintBanner();
                        PrintSnapshotToConsole(snapshot);
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("Auto-refreshing every {0}s. Press 'Q' to quit.", interval);
                        Console.ResetColor();
                    }
                    catch { }
                };

                engine.Start();

                // Wait for exit key or cancellation
                while (engine.IsRunning)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                        {
                            break;
                        }
                    }
                    Thread.Sleep(200);
                }

                engine.Stop();
            }

            return 0;
        }

        private static void PrintSnapshotToConsole(StateSnapshot snapshot)
        {
            SystemMemoryMetrics m = snapshot.Metrics;

            Console.WriteLine("Timestamp (UTC): {0:yyyy-MM-dd HH:mm:ss} | Operating Mode: {1}",
                snapshot.TimestampUtc, snapshot.OperatingMode);

            if (snapshot.IsEmergencyStopped)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(">>> EMERGENCY STOP SWITCH IS ACTIVE: AUTOMATED ACTIONS HALTED <<<");
                Console.ResetColor();
            }

            // Pressure Level with Alert Color
            Console.Write("Memory Pressure Status: ");
            switch (snapshot.PressureLevel)
            {
                case "Critical":
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case "High":
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case "Elevated":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
            }
            Console.WriteLine("[ {0} ]", snapshot.PressureLevel.ToUpperInvariant());
            Console.ResetColor();

            Console.WriteLine("---------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("PHYSICAL RAM:  {0,7:F1} MB used / {1,7:F1} MB total ({2,5:F1}% load)",
                m.UsedPhysicalMB, m.TotalPhysicalMB, m.MemoryLoadPercentage);
            Console.WriteLine("AVAILABLE RAM: {0,7:F1} MB free physical memory (Slope: {1:F1} MB/m)",
                m.AvailablePhysicalMB, snapshot.AvailableRamSlopeMBPerMin);
            Console.ResetColor();

            Console.WriteLine("COMMIT LIMIT:  {0,7:F1} MB commit / {1,7:F1} MB limit ({2,5:F1}% commit load)",
                m.CommittedMB, m.CommitLimitMB, m.CommitLoadPercentage);

            Console.WriteLine("KERNEL POOLS:  Nonpaged: {0,6:F1} MB | Paged: {1,6:F1} MB | Cache: {2,6:F1} MB",
                m.NonpagedPoolMB, m.PagedPoolMB, m.SystemCacheMB);

            Console.WriteLine("PAGING STATUS: Pages/sec: {0,7:F1} | Faults/sec: {1,7:F1} | Pagefile: {2:F1}%",
                m.PagesPerSec, m.PageFaultsPerSec, m.PagefileUsagePercentage);

            if (m.IsCompressionActive)
            {
                Console.WriteLine("COMPRESSION:   Active ({0:F1} MB working set compressed in RAM)",
                    m.MemoryCompressionWorkingSetMB);
            }
            else
            {
                Console.WriteLine("COMPRESSION:   Inactive or Not Reported");
            }

            if (snapshot.EdgeStatus != null && snapshot.EdgeStatus.IsRunning)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("EDGE 24/7 OPT: Procs: {0} | RAM: {1:F1} MB | Commit: {2:F1} MB | Tabs: {3} | GPU: {4:F1} MB",
                    snapshot.EdgeStatus.TotalProcesses,
                    snapshot.EdgeStatus.TotalWorkingSetMB,
                    snapshot.EdgeStatus.TotalPrivateMB,
                    snapshot.EdgeStatus.RendererProcessesCount,
                    snapshot.EdgeStatus.GpuWorkingSetMB);
                Console.ResetColor();
            }

            Console.WriteLine("SYSTEM:        CPU: {0:F1}% | Handles: {1} | Threads: {2} | Processes: {3}",
                m.CpuUtilizationPercentage, m.HandleCount, m.ThreadCount, m.ProcessCount);

            Console.WriteLine("---------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("TOP 15 MEMORY CONSUMERS (Physical Working Set & Committed Private):");
            Console.ResetColor();
            Console.WriteLine("{0,-7} {1,-20} {2,11} {3,11} {4,-16}",
                "PID", "Process Name", "Working Set", "Private Bytes", "Classification");
            Console.WriteLine(new string('-', 68));

            int displayCount = Math.Min(15, snapshot.TopProcesses.Count);
            for (int i = 0; i < displayCount; i++)
            {
                ProcessMemoryInfo p = snapshot.TopProcesses[i];
                string classLabel = !string.IsNullOrEmpty(p.Classification) ? p.Classification : (p.IsProtected ? "Protected" : "UserApp");
                Console.WriteLine("{0,-7} {1,-20} {2,8:F1} MB {3,8:F1} MB {4,-16}",
                    p.ProcessId,
                    Truncate(p.ProcessName, 20),
                    p.WorkingSetMB,
                    p.PrivateMB,
                    Truncate(classLabel, 16));
            }

            if (snapshot.ActiveAlerts != null && snapshot.ActiveAlerts.Count > 0)
            {
                Console.WriteLine("---------------------------------------------------------------");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("ACTIVE DIAGNOSTIC ALERTS & INSIGHTS ({0}):", snapshot.ActiveAlerts.Count);
                Console.ResetColor();
                for (int i = 0; i < Math.Min(5, snapshot.ActiveAlerts.Count); i++)
                {
                    Console.WriteLine(" * {0}", snapshot.ActiveAlerts[i]);
                }
            }
        }

        private static int SetMode(string mode)
        {
            string clean = mode.Trim();
            string exactMode = null;
            if (clean.Equals("MonitorOnly", StringComparison.OrdinalIgnoreCase)) exactMode = "MonitorOnly";
            else if (clean.Equals("BalancedAuto", StringComparison.OrdinalIgnoreCase)) exactMode = "BalancedAuto";
            else if (clean.Equals("AggressiveAuto", StringComparison.OrdinalIgnoreCase)) exactMode = "AggressiveAuto";
            else if (clean.Equals("GamingFocus", StringComparison.OrdinalIgnoreCase)) exactMode = "GamingFocus";
            else if (clean.Equals("Paused", StringComparison.OrdinalIgnoreCase)) exactMode = "Paused";

            if (exactMode == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid mode '{0}'. Valid options: MonitorOnly, BalancedAuto, AggressiveAuto, GamingFocus, Paused", mode);
                Console.ResetColor();
                return 1;
            }

            var cfgMgr = new RAMGuardian.Configuration.ConfigManager();
            var cfg = cfgMgr.LoadConfig();

            cfg.OperatingMode = exactMode;
            bool ok = cfgMgr.SaveConfig(cfg);

            if (ok)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Operating mode successfully changed to: {0}", exactMode);
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to update config.json.");
                Console.ResetColor();
                return 1;
            }
        }

        private static int SetEmergencyStop(bool stop)
        {
            var cfgMgr = new RAMGuardian.Configuration.ConfigManager();
            var cfg = cfgMgr.LoadConfig();
            cfg.IsEmergencyStopped = stop;
            bool ok = cfgMgr.SaveConfig(cfg);

            if (ok)
            {
                Console.ForegroundColor = stop ? ConsoleColor.Red : ConsoleColor.Green;
                Console.WriteLine(stop
                    ? "EMERGENCY STOP ACTIVATED: All automated optimizations and process closures suspended immediately."
                    : "EMERGENCY STOP CLEARED: Automated optimizations resumed.");
                Console.ResetColor();
                return 0;
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to update config.");
            Console.ResetColor();
            return 1;
        }

        private static int ShowEdgeStatus()
        {
            PrintBanner();
            var collector = new RAMGuardian.Monitoring.EdgeProcessCollector();
            var res = collector.CollectEdgeIntelligence();
            var m = res.Item1;
            var procs = res.Item2;

            Console.WriteLine("MICROSOFT EDGE INTELLIGENCE STATUS");
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine("Running Status:       {0}", m.IsRunning ? "Active" : "Not Running");
            Console.WriteLine("Total Edge Processes: {0}", m.TotalProcesses);
            Console.WriteLine("Total Working Set:    {0:F1} MB", m.TotalWorkingSetMB);
            Console.WriteLine("Total Private Bytes:  {0:F1} MB", m.TotalPrivateMB);
            Console.WriteLine("Browser Process:      {0:F1} MB", m.BrowserWorkingSetMB);
            Console.WriteLine("GPU Process:          {0:F1} MB", m.GpuWorkingSetMB);
            Console.WriteLine("Renderer Tabs:        {0} ({1:F1} MB total)", m.RendererProcessesCount, m.RenderersWorkingSetMB);
            Console.WriteLine("Extension Processes:  {0} ({1:F1} MB total)", m.ExtensionProcessesCount, m.ExtensionsWorkingSetMB);
            Console.WriteLine();
            Console.WriteLine("EDGE PROCESS INVENTORY ({0} detected):", procs.Count);
            Console.WriteLine("{0,-8} {1,-16} {2,12} {3,12} {4,-14}", "PID", "Role", "Working Set", "Private Bytes", "Candidate?");
            Console.WriteLine(new string('-', 66));
            for (int i = 0; i < procs.Count; i++)
            {
                var p = procs[i];
                Console.WriteLine("{0,-8} {1,-16} {2,9:F1} MB {3,9:F1} MB {4,-14}",
                    p.ProcessId, p.Role, p.WorkingSetMB, p.PrivateMB, p.IsSleepingCandidate ? "Sleep Target" : "-");
            }
            return 0;
        }

        private static int RestoreEdgePolicies()
        {
            PrintBanner();
            var optimizer = new RAMGuardian.Optimization.EdgeOptimizer();
            string msg;
            bool ok = optimizer.RestoreEdgePolicies(out msg);
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine(msg);
            Console.ResetColor();
            return ok ? 0 : 1;
        }

        private static int ListBackups()
        {
            var bkpMgr = new RAMGuardian.Optimization.BackupManager();
            var backups = bkpMgr.GetBackups();

            PrintBanner();
            Console.WriteLine("RAM GUARDIAN MANAGED BACKUPS ({0} total):", backups.Count);
            Console.WriteLine("---------------------------------------------------------------");

            if (backups.Count == 0)
            {
                Console.WriteLine("No backups recorded yet. System remains in original state.");
                return 0;
            }

            Console.WriteLine("{0,-32} {1,-14} {2,-12} {3,-10}", "Backup ID", "Created (UTC)", "Entry", "Restored?");
            Console.WriteLine(new string('-', 72));

            for (int i = 0; i < backups.Count; i++)
            {
                var b = backups[i];
                string name = b.OriginalEntry != null ? b.OriginalEntry.Name : "unknown";
                string restored = b.IsRestored ? "YES" : "NO (Active)";
                Console.WriteLine("{0,-32} {1,-14:yyyy-MM-dd} {2,-12} {3,-10}",
                    b.BackupId, b.CreatedUtc, Truncate(name, 12), restored);
            }

            return 0;
        }

        private static int RestoreAllBackups()
        {
            var bkpMgr = new RAMGuardian.Optimization.BackupManager();
            List<string> errors;
            int count = bkpMgr.RestoreAll(out errors);

            PrintBanner();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Restored {0} RAM Guardian managed change(s) back to original state.", count);
            Console.ResetColor();

            if (errors != null && errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Restore Warnings/Errors:");
                for (int i = 0; i < errors.Count; i++)
                {
                    Console.WriteLine(" - {0}", errors[i]);
                }
                Console.ResetColor();
            }

            return 0;
        }

        private static string Truncate(string val, int maxLen)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            return val.Length <= maxLen ? val : val.Substring(0, maxLen - 2) + "..";
        }
    }
}
