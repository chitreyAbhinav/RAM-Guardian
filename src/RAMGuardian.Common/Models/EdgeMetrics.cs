using System;
using System.Collections.Generic;

namespace RAMGuardian.Common.Models
{
    public enum EdgeProcessRole
    {
        Browser,
        Gpu,
        Renderer,
        Extension,
        NetworkService,
        StorageService,
        AudioService,
        Crashpad,
        Utility
    }

    public class EdgeProcessInfo
    {
        public int ProcessId { get; set; }
        public string Role { get; set; }
        public long WorkingSetBytes { get; set; }
        public long PrivateBytes { get; set; }
        public double CpuPercent { get; set; }
        public string CommandLine { get; set; }
        public bool IsExtension { get; set; }
        public bool IsSleepingCandidate { get; set; }

        public double WorkingSetMB
        {
            get { return WorkingSetBytes / (1024.0 * 1024.0); }
        }

        public double PrivateMB
        {
            get { return PrivateBytes / (1024.0 * 1024.0); }
        }

        public EdgeProcessInfo()
        {
            Role = "Utility";
            CommandLine = string.Empty;
        }
    }

    public class EdgeMetrics
    {
        public DateTime TimestampUtc { get; set; }
        public bool IsRunning { get; set; }
        public int TotalProcesses { get; set; }
        public int RendererProcessesCount { get; set; }
        public int ExtensionProcessesCount { get; set; }

        public long TotalWorkingSetBytes { get; set; }
        public long TotalPrivateBytes { get; set; }
        public double TotalCpuPercent { get; set; }

        public double GpuWorkingSetMB { get; set; }
        public double BrowserWorkingSetMB { get; set; }
        public double RenderersWorkingSetMB { get; set; }
        public double ExtensionsWorkingSetMB { get; set; }

        public bool SleepingTabsPolicyEnabled { get; set; }
        public int SleepingTabsTimeoutSeconds { get; set; }
        public bool EfficiencyModePolicyEnabled { get; set; }
        public bool BackgroundModeDisabledPolicy { get; set; }

        public string StatusSummary { get; set; }
        public DateTime? LastOptimizationUtc { get; set; }

        public double TotalWorkingSetMB
        {
            get { return TotalWorkingSetBytes / (1024.0 * 1024.0); }
        }

        public double TotalPrivateMB
        {
            get { return TotalPrivateBytes / (1024.0 * 1024.0); }
        }

        public EdgeMetrics()
        {
            TimestampUtc = DateTime.UtcNow;
            StatusSummary = "Edge Monitoring Active";
        }
    }
}
