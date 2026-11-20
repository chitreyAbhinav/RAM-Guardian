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

        // Target Memory Usage Goals (65% to 70% average memory usage at all times)
        public double TargetMemoryUsagePercentageMin { get; set; }
        public double TargetMemoryUsagePercentageMax { get; set; }

        // Interval Settings
        public int MonitoringIntervalSeconds { get; set; }
        public int DashboardRefreshIntervalSeconds { get; set; }

        // Pressure Thresholds (Calibrated for 65%-70% RAM goal on 3.7 GB RAM VivoBook X515EA)
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

        // Explicitly Approved Background Targets for Controlled Termination (AggressiveAuto & GamingFocus extreme cases)
        public List<string> ApprovedTerminationTargets { get; set; }

        public GuardianConfig()
        {
            OperatingMode = "GamingFocus"; // Default mode: proactive 65-70% RAM goal & full-throttle optimization
            IsEmergencyStopped = false;

            // Microsoft Edge Built-in Settings (Aggressive defaults to maintain 65-70% goal)
            EdgeOptimizationEnabled = true; // Always ON by default
            EdgeSleepingTabsTimeoutSeconds = 120; // 2 minutes proactive sleep
            EdgeEfficiencyModeEnabled = true;
            EdgeBackgroundModeDisabled = true; // Stop background apps when Edge closes
            EdgeStartupBoostDisabled = true; // Prevent background pre-loading at boot
            EdgeProtectedWebsites = new List<string>
            {
                "*.google.com", "*.youtube.com", "*.teams.microsoft.com", "*.zoom.us", "*.github.com"
            };

            // Calibrated for 65%-70% Memory Usage Goal on ASUS VivoBook X515EA (~3.7 GB RAM)
            TargetMemoryUsagePercentageMin = 65.0;
            TargetMemoryUsagePercentageMax = 70.0;

            MonitoringIntervalSeconds = 15;
            DashboardRefreshIntervalSeconds = 4;

            ElevatedAvailableRamMB = 1150.0; // Triggers when RAM usage > 70% (~1,150 MB free)
            HighAvailableRamMB = 950.0;      // Triggers when RAM usage > 75% (~950 MB free)
            CriticalAvailableRamMB = 550.0;  // Extreme case: RAM usage > 85% (~550 MB free)

            ElevatedCommitPercentage = 68.0;
            HighCommitPercentage = 75.0;
            CriticalCommitPercentage = 85.0;

            HighPagingPagesPerSec = 1500.0;
            CriticalPagingPagesPerSec = 2500.0;

            CooldownMinutes = 3; // Responsive 3-minute cooldown between automated intervention rounds
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
