using System;
using System.Collections.Generic;

namespace RAMGuardian.Configuration.Models
{
    public class GuardianConfig
    {
        // Operating Modes:
        // 1. "MonitorOnly"   - Read-only diagnostics, zero modifications, Edge read-only analysis
        // 2. "BalancedAuto"  - Low-risk optimization, graceful closures only, low-risk Edge tuning
        // 3. "AggressiveAuto"- Maximum practical memory optimization, graduated process management, proactive Edge tuning
        // 4. "GamingFocus"   - Low-interference, foreground protected, non-disruptive Edge tuning
        // 5. "Paused"        - Standby, all monitoring and optimizations suspended
        public string OperatingMode { get; set; }

        // Global Emergency Switch: Immediately halts all automated process closures and optimizations
        public bool IsEmergencyStopped { get; set; }

        // Microsoft Edge Optimization (Built-in, enabled by default across all active modes)
        public bool EdgeOptimizationEnabled { get; set; }
        public int EdgeSleepingTabsTimeoutSeconds { get; set; }
        public bool EdgeEfficiencyModeEnabled { get; set; }
        public bool EdgeBackgroundModeDisabled { get; set; }
        public bool EdgeStartupBoostDisabled { get; set; }
        public List<string> EdgeProtectedWebsites { get; set; }

        // Interval Settings
        public int MonitoringIntervalSeconds { get; set; }
        public int DashboardRefreshIntervalSeconds { get; set; }

        // Pressure Thresholds (Calibrated for 4 GB RAM VivoBook X515EA)
        public double ElevatedAvailableRamMB { get; set; }
        public double HighAvailableRamMB { get; set; }
        public double CriticalAvailableRamMB { get; set; }

        public double ElevatedCommitPercentage { get; set; }
        public double HighCommitPercentage { get; set; }
        public double CriticalCommitPercentage { get; set; }

        public double HighPagingPagesPerSec { get; set; }
        public double CriticalPagingPagesPerSec { get; set; }

        // Cooldown and Safety Controls
        public int CooldownMinutes { get; set; }
        public bool EnableNotifications { get; set; }
        public bool LaunchDashboardOnLogin { get; set; }
        public bool MinimizeDashboardToTray { get; set; }
        public int LogRetentionDays { get; set; }
        public long MaxLogFileSizeBytes { get; set; }

        // Explicitly Approved Background Targets for Controlled Termination (AggressiveAuto only)
        public List<string> ApprovedTerminationTargets { get; set; }

        public GuardianConfig()
        {
            OperatingMode = "MonitorOnly"; // Installation default
            IsEmergencyStopped = false;

            // Microsoft Edge Built-in Settings
            EdgeOptimizationEnabled = true; // Always ON by default
            EdgeSleepingTabsTimeoutSeconds = 300; // 5 minutes
            EdgeEfficiencyModeEnabled = true;
            EdgeBackgroundModeDisabled = false;
            EdgeStartupBoostDisabled = false;
            EdgeProtectedWebsites = new List<string>
            {
                "*.google.com", "*.youtube.com", "*.teams.microsoft.com", "*.zoom.us", "*.github.com"
            };

            MonitoringIntervalSeconds = 15;
            DashboardRefreshIntervalSeconds = 4;

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

            // Default safe list of eligible background targets (non-critical helper processes)
            ApprovedTerminationTargets = new List<string>
            {
                "OneDrive", "utweb", "Spotify"
            };
        }
    }
}
