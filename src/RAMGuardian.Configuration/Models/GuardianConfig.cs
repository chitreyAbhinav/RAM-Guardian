using System;

namespace RAMGuardian.Configuration.Models
{
    public class GuardianConfig
    {
        public string OperatingMode { get; set; }
        public int MonitoringIntervalSeconds { get; set; }
        public int DashboardRefreshIntervalSeconds { get; set; }

        // Pressure Thresholds
        public double ElevatedAvailableRamMB { get; set; }
        public double HighAvailableRamMB { get; set; }
        public double CriticalAvailableRamMB { get; set; }

        public double ElevatedCommitPercentage { get; set; }
        public double HighCommitPercentage { get; set; }
        public double CriticalCommitPercentage { get; set; }

        public double HighPagingPagesPerSec { get; set; }
        public double CriticalPagingPagesPerSec { get; set; }

        public int CooldownMinutes { get; set; }
        public bool EnableNotifications { get; set; }
        public bool LaunchDashboardOnLogin { get; set; }
        public bool MinimizeDashboardToTray { get; set; }
        public int LogRetentionDays { get; set; }
        public long MaxLogFileSizeBytes { get; set; }

        public GuardianConfig()
        {
            // Default Mode must be MonitorOnly per Section 11 & Non-negotiable safety rules
            OperatingMode = "MonitorOnly";
            MonitoringIntervalSeconds = 15;
            DashboardRefreshIntervalSeconds = 5;

            // Thresholds calibrated for 4 GB RAM VivoBook
            ElevatedAvailableRamMB = 500.0;
            HighAvailableRamMB = 250.0;
            CriticalAvailableRamMB = 150.0;

            ElevatedCommitPercentage = 78.0;
            HighCommitPercentage = 82.0;
            CriticalCommitPercentage = 90.0;

            HighPagingPagesPerSec = 1500.0;
            CriticalPagingPagesPerSec = 2500.0;

            CooldownMinutes = 5;
            EnableNotifications = true;
            LaunchDashboardOnLogin = false;
            MinimizeDashboardToTray = true;
            LogRetentionDays = 14;
            MaxLogFileSizeBytes = 10485760; // 10 MB
        }
    }
}
