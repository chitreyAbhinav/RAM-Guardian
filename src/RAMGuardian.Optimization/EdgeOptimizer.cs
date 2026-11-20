using System;
using System.Collections.Generic;
using Microsoft.Win32;
using RAMGuardian.Common.Models;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Logging;
using RAMGuardian.Optimization.Models;

namespace RAMGuardian.Optimization
{
    public class EdgeOptimizer
    {
        public const string DefaultEdgePolicySubKey = @"Software\Policies\Microsoft\Edge";
        private readonly string _edgePolicySubKey;
        private readonly BackupManager _backupManager;
        private readonly FileLogger _logger;

        public string PolicySubKey
        {
            get { return _edgePolicySubKey; }
        }

        public EdgeOptimizer(BackupManager backupManager = null, FileLogger logger = null, string edgePolicySubKey = DefaultEdgePolicySubKey)
        {
            _backupManager = backupManager ?? new BackupManager();
            _logger = logger ?? new FileLogger();
            _edgePolicySubKey = !string.IsNullOrEmpty(edgePolicySubKey) ? edgePolicySubKey : DefaultEdgePolicySubKey;
        }

        public bool OptimizeEdgePolicies(GuardianConfig config, EdgeMetrics metrics, out string resultSummary)
        {
            resultSummary = string.Empty;

            if (config == null)
            {
                resultSummary = "Configuration is null.";
                return false;
            }

            // Mode Verification
            if (config.OperatingMode == "MonitorOnly")
            {
                resultSummary = "MonitorOnly mode: Edge performance observed without modifications.";
                return true;
            }

            if (config.OperatingMode == "Paused")
            {
                resultSummary = "Edge optimization suspended in Paused mode.";
                return false;
            }

            if (config.IsEmergencyStopped)
            {
                resultSummary = "Edge optimization halted: Emergency Stop active.";
                return false;
            }

            if (!config.EdgeOptimizationEnabled)
            {
                resultSummary = "Edge optimization is disabled in user settings.";
                return false;
            }

            try
            {
                int timeoutSeconds = config.EdgeSleepingTabsTimeoutSeconds > 0 ? config.EdgeSleepingTabsTimeoutSeconds : 300;
                bool disableBackground = config.EdgeBackgroundModeDisabled;
                bool disableStartupBoost = config.EdgeStartupBoostDisabled;

                if (config.OperatingMode == "AggressiveAuto" || config.OperatingMode == "GamingFocus")
                {
                    if (config.EdgeSleepingTabsTimeoutSeconds <= 0) timeoutSeconds = 120;
                    disableBackground = true;
                    disableStartupBoost = true;
                }

                // 1. Read existing policy keys to create backup if not already backed up
                EnsurePolicyBackup();

                // 2. Apply documented Edge group policies in HKCU
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(_edgePolicySubKey))
                {
                    if (key != null)
                    {
                        key.SetValue("SleepingTabsEnabled", 1, RegistryValueKind.DWord);
                        key.SetValue("SleepingTabsTimeout", timeoutSeconds, RegistryValueKind.DWord);
                        key.SetValue("EfficiencyModeEnabled", 1, RegistryValueKind.DWord);

                        if (disableBackground)
                        {
                            key.SetValue("BackgroundModeEnabled", 0, RegistryValueKind.DWord);
                        }

                        if (disableStartupBoost)
                        {
                            key.SetValue("StartupBoostEnabled", 0, RegistryValueKind.DWord);
                        }

                        // Set protected URLs
                        if (config.EdgeProtectedWebsites != null && config.EdgeProtectedWebsites.Count > 0)
                        {
                            key.SetValue("SleepingTabsBlockedForUrls", config.EdgeProtectedWebsites.ToArray(), RegistryValueKind.MultiString);
                        }
                    }
                }

                if (metrics != null)
                {
                    metrics.SleepingTabsPolicyEnabled = true;
                    metrics.SleepingTabsTimeoutSeconds = timeoutSeconds;
                    metrics.EfficiencyModePolicyEnabled = true;
                    metrics.BackgroundModeDisabledPolicy = disableBackground;
                    metrics.LastOptimizationUtc = DateTime.UtcNow;
                    metrics.StatusSummary = string.Format("Edge Optimized ({0}): Sleeping Tabs {1}s, Efficiency Mode ON",
                        config.OperatingMode, timeoutSeconds);
                }

                resultSummary = string.Format("Successfully applied Edge performance policy ({0}): Sleeping Tabs {1}s.",
                    config.OperatingMode, timeoutSeconds);

                _logger.Info("EDGE_OPTIMIZED", resultSummary);
                return true;
            }
            catch (Exception ex)
            {
                resultSummary = "Failed to apply Edge policy: " + ex.Message;
                _logger.Error("EDGE_OPT_FAILED", resultSummary, ex);
                return false;
            }
        }

        private void EnsurePolicyBackup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_edgePolicySubKey, false))
                {
                    if (key != null)
                    {
                        var entry = new StartupEntry
                        {
                            Id = "EDGE_POLICY_BACKUP",
                            Name = "MicrosoftEdgePolicy",
                            Location = "HKCU_Policy",
                            RegistryKeyPath = string.Format(@"{0}\{1}", Registry.CurrentUser.Name, _edgePolicySubKey),
                            CommandLine = "Native Edge Enterprise Policies Backup",
                            Status = "Active"
                        };
                        _backupManager.CreateBackup(entry, "OPT_EDGE_POLICIES", "Pre-optimization backup of Edge policies.");
                    }
                }
            }
            catch { }
        }

        public bool RestoreEdgePolicies(out string message)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_edgePolicySubKey, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue("SleepingTabsEnabled", false);
                        key.DeleteValue("SleepingTabsTimeout", false);
                        key.DeleteValue("EfficiencyModeEnabled", false);
                        key.DeleteValue("BackgroundModeEnabled", false);
                        key.DeleteValue("StartupBoostEnabled", false);
                        key.DeleteValue("SleepingTabsBlockedForUrls", false);
                    }
                }
                message = "Microsoft Edge policy optimizations successfully removed. Edge restored to browser defaults.";
                _logger.Info("EDGE_RESTORED", message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Failed to restore Edge policies: " + ex.Message;
                _logger.Error("EDGE_RESTORE_FAILED", message, ex);
                return false;
            }
        }
    }
}
