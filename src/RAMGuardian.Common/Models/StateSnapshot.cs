using System;
using System.Collections.Generic;

namespace RAMGuardian.Common.Models
{
    public class StateSnapshot
    {
        public DateTime TimestampUtc { get; set; }
        public string OperatingMode { get; set; }
        public string PressureLevel { get; set; }
        public string StatusMessage { get; set; }
        public double ServiceUptimeSeconds { get; set; }
        public double AvailableRamSlopeMBPerMin { get; set; }
        public double NonpagedPoolBaselineMB { get; set; }
        public List<string> ActiveAlerts { get; set; }

        public SystemMemoryMetrics Metrics { get; set; }
        public List<ProcessMemoryInfo> TopProcesses { get; set; }

        public StateSnapshot()
        {
            TimestampUtc = DateTime.UtcNow;
            OperatingMode = "MonitorOnly";
            PressureLevel = "Normal";
            StatusMessage = "Service operating normally.";
            ActiveAlerts = new List<string>();
            Metrics = new SystemMemoryMetrics();
            TopProcesses = new List<ProcessMemoryInfo>();
        }
    }
}
