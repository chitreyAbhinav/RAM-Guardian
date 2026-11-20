using System;
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

                if (cmd == "--console" || cmd == "-c" || cmd == "--monitor" || cmd == "-m")
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
            Console.WriteLine("   Safety Policy: Default-DENY | Non-Destructive Monitoring   ");
            Console.WriteLine("===============================================================");
            Console.ResetColor();
        }

        private static void PrintUsage()
        {
            PrintBanner();
            Console.WriteLine("Usage: RAMGuardian.Service.exe [option]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --console, -c       Run in interactive live console mode (updates continuously)");
            Console.WriteLine("  --once, -1          Capture a single read-only snapshot and print to terminal");
            Console.WriteLine("  --once --json       Capture a single snapshot and print raw JSON");
            Console.WriteLine("  --help, -h          Show this help text");
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
            Console.WriteLine("AVAILABLE RAM: {0,7:F1} MB free physical memory", m.AvailablePhysicalMB);
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

            Console.WriteLine("SYSTEM:        CPU: {0:F1}% | Handles: {1} | Threads: {2} | Processes: {3}",
                m.CpuUtilizationPercentage, m.HandleCount, m.ThreadCount, m.ProcessCount);

            Console.WriteLine("---------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("TOP 15 MEMORY CONSUMERS (Physical Working Set & Committed Private):");
            Console.ResetColor();
            Console.WriteLine("{0,-7} {1,-22} {2,12} {3,12} {4,-10}",
                "PID", "Process Name", "Working Set", "Private Bytes", "Protected?");
            Console.WriteLine(new string('-', 68));

            int displayCount = Math.Min(15, snapshot.TopProcesses.Count);
            for (int i = 0; i < displayCount; i++)
            {
                ProcessMemoryInfo p = snapshot.TopProcesses[i];
                string protLabel = p.IsProtected ? "YES (Safe)" : "User/Other";
                Console.WriteLine("{0,-7} {1,-22} {2,9:F1} MB {3,9:F1} MB {4,-10}",
                    p.ProcessId,
                    Truncate(p.ProcessName, 22),
                    p.WorkingSetMB,
                    p.PrivateMB,
                    protLabel);
            }
        }

        private static string Truncate(string val, int maxLen)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            return val.Length <= maxLen ? val : val.Substring(0, maxLen - 2) + "..";
        }
    }
}
