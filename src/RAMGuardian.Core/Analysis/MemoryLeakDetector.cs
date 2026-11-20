using System;
using System.Collections.Generic;
using RAMGuardian.Common.Models;
using RAMGuardian.Core.Alerts;

namespace RAMGuardian.Core.Analysis
{
    public class ProcessMemorySample
    {
        public DateTime TimestampUtc { get; set; }
        public long PrivateBytes { get; set; }
        public long WorkingSetBytes { get; set; }
    }

    public class MemoryLeakDetector
    {
        private readonly Dictionary<int, List<ProcessMemorySample>> _processHistory;
        private readonly HashSet<int> _alertedPids;
        private readonly int _minSamplesForAlert;
        private readonly double _minGrowthRateMBPerHour;

        public MemoryLeakDetector(int minSamplesForAlert = 5, double minGrowthRateMBPerHour = 50.0)
        {
            _processHistory = new Dictionary<int, List<ProcessMemorySample>>();
            _alertedPids = new HashSet<int>();
            _minSamplesForAlert = Math.Max(3, minSamplesForAlert);
            _minGrowthRateMBPerHour = minGrowthRateMBPerHour;
        }

        public void IngestProcessSnapshot(List<ProcessMemoryInfo> procs, List<MemoryAlert> alertCollector)
        {
            if (procs == null) return;
            DateTime now = DateTime.UtcNow;

            var activePids = new HashSet<int>();

            for (int i = 0; i < procs.Count; i++)
            {
                ProcessMemoryInfo p = procs[i];
                activePids.Add(p.ProcessId);

                // Skip system/idle processes
                if (p.ProcessId <= 4) continue;

                List<ProcessMemorySample> samples;
                if (!_processHistory.TryGetValue(p.ProcessId, out samples))
                {
                    samples = new List<ProcessMemorySample>();
                    _processHistory[p.ProcessId] = samples;
                }

                samples.Add(new ProcessMemorySample
                {
                    TimestampUtc = now,
                    PrivateBytes = p.PrivateBytes,
                    WorkingSetBytes = p.WorkingSetBytes
                });

                // Keep up to 20 samples
                if (samples.Count > 20)
                {
                    samples.RemoveAt(0);
                }

                // Check for leak pattern if we have enough samples
                if (samples.Count >= _minSamplesForAlert && !_alertedPids.Contains(p.ProcessId))
                {
                    double growthRateMBPerHour;
                    double totalGrowthMB;
                    if (IsMonotonicallyGrowing(samples, out growthRateMBPerHour, out totalGrowthMB))
                    {
                        if (growthRateMBPerHour >= _minGrowthRateMBPerHour && totalGrowthMB > 40.0)
                        {
                            _alertedPids.Add(p.ProcessId);

                            if (alertCollector != null)
                            {
                                alertCollector.Add(new MemoryAlert(
                                    AlertSeverity.Warning,
                                    "LeakSuspicion",
                                    string.Format("Potential Memory Leak: {0} (PID {1})", p.ProcessName, p.ProcessId),
                                    string.Format("Process private memory has grown continuously at ~{0:F1} MB/hr (+{1:F1} MB total) over {2} samples without releasing. (Diagnostic Alert - Process untouched).",
                                        growthRateMBPerHour, totalGrowthMB, samples.Count),
                                    p.ProcessName
                                ));
                            }
                        }
                    }
                }
            }

            // Cleanup terminated processes from tracking
            var deadPids = new List<int>();
            foreach (var pid in _processHistory.Keys)
            {
                if (!activePids.Contains(pid))
                {
                    deadPids.Add(pid);
                }
            }

            for (int i = 0; i < deadPids.Count; i++)
            {
                _processHistory.Remove(deadPids[i]);
                _alertedPids.Remove(deadPids[i]);
            }
        }

        private static bool IsMonotonicallyGrowing(List<ProcessMemorySample> samples, out double growthRateMBPerHour, out double totalGrowthMB)
        {
            growthRateMBPerHour = 0.0;
            totalGrowthMB = 0.0;

            int increases = 0;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].PrivateBytes >= samples[i - 1].PrivateBytes)
                {
                    increases++;
                }
            }

            // At least 80% of intervals show growth
            bool strongGrowth = (double)increases / (samples.Count - 1) >= 0.8;
            if (!strongGrowth) return false;

            var first = samples[0];
            var last = samples[samples.Count - 1];

            double hours = (last.TimestampUtc - first.TimestampUtc).TotalHours;
            if (hours <= 0.001) return false;

            long byteDiff = last.PrivateBytes - first.PrivateBytes;
            if (byteDiff <= 0) return false;

            totalGrowthMB = byteDiff / (1024.0 * 1024.0);
            growthRateMBPerHour = Math.Round(totalGrowthMB / hours, 1);

            return true;
        }
    }
}
