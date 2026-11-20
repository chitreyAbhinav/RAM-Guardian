using System;
using System.Collections.Generic;
using Microsoft.Win32;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Logging;
using RAMGuardian.Optimization.Models;
using RAMGuardian.Security.Policies;

namespace RAMGuardian.Optimization.Actions
{
    public interface IOptimizationAction
    {
        string ActionId { get; }
        string Description { get; }
        string SafetyClassification { get; }
        string ExpectedImpact { get; }
        bool CanExecute(GuardianConfig config, out string reason);
        bool Execute(BackupManager backupManager, FileLogger logger, out string resultMessage);
    }

    public class StartupDisableAction : IOptimizationAction
    {
        public string ActionId { get; private set; }
        public string Description { get; private set; }
        public string SafetyClassification { get { return "SafeReversible"; } }
        public string ExpectedImpact { get; private set; }

        private readonly string _entryName;
        private readonly string _registrySubKey;
        private readonly bool _isHklm;

        public StartupDisableAction(string actionId, string entryName, string description, string expectedImpact, bool isHklm = false)
        {
            ActionId = actionId;
            Description = description;
            ExpectedImpact = expectedImpact;
            _entryName = entryName;
            _registrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            _isHklm = isHklm;
        }

        public bool CanExecute(GuardianConfig config, out string reason)
        {
            if (config == null)
            {
                reason = "Configuration is null.";
                return false;
            }

            // Mode guard: ONLY permitted in SafeAutomatic mode!
            if (config.OperatingMode != "SafeAutomatic")
            {
                reason = string.Format("Action prohibited in mode '{0}'. Requires 'SafeAutomatic'.", config.OperatingMode);
                return false;
            }

            // Verify entry currently exists in registry
            RegistryKey root = _isHklm ? Registry.LocalMachine : Registry.CurrentUser;
            try
            {
                using (RegistryKey key = root.OpenSubKey(_registrySubKey, false))
                {
                    if (key == null || key.GetValue(_entryName) == null)
                    {
                        reason = string.Format("Startup entry '{0}' is not currently active.", _entryName);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                reason = "Error checking registry: " + ex.Message;
                return false;
            }

            reason = "Preconditions satisfied.";
            return true;
        }

        public bool Execute(BackupManager backupManager, FileLogger logger, out string resultMessage)
        {
            RegistryKey root = _isHklm ? Registry.LocalMachine : Registry.CurrentUser;
            string location = _isHklm ? "HKLM_Run" : "HKCU_Run";

            try
            {
                // 1. Read existing value
                string existingCommand = null;
                using (RegistryKey readKey = root.OpenSubKey(_registrySubKey, false))
                {
                    if (readKey != null)
                    {
                        object val = readKey.GetValue(_entryName);
                        if (val != null) existingCommand = val.ToString();
                    }
                }

                if (string.IsNullOrEmpty(existingCommand))
                {
                    resultMessage = string.Format("Entry '{0}' not found; no action required.", _entryName);
                    return true;
                }

                // 2. MANDATORY SAFETY: Create persistent backup BEFORE modifying anything!
                var entry = new StartupEntry
                {
                    Id = string.Format("{0}_{1}", location, _entryName),
                    Name = _entryName,
                    Location = location,
                    RegistryKeyPath = string.Format(@"{0}\{1}", root.Name, _registrySubKey),
                    CommandLine = existingCommand,
                    Status = "Active"
                };

                var backup = backupManager.CreateBackup(entry, ActionId, "Optimization per user preference to reduce background memory consumption.");
                if (backup == null)
                {
                    resultMessage = "Failed to create safety backup. Modification aborted.";
                    if (logger != null) logger.Error("BACKUP_ABORT", resultMessage);
                    return false;
                }

                // 3. Remove registry value
                using (RegistryKey writeKey = root.OpenSubKey(_registrySubKey, true))
                {
                    if (writeKey == null)
                    {
                        resultMessage = "Access denied opening registry for write.";
                        return false;
                    }
                    writeKey.DeleteValue(_entryName, false);
                }

                // 4. Post-Execution Verification: Verify removal succeeded
                using (RegistryKey verifyKey = root.OpenSubKey(_registrySubKey, false))
                {
                    if (verifyKey != null && verifyKey.GetValue(_entryName) != null)
                    {
                        resultMessage = "Post-modification verification failed: registry value still exists!";
                        if (logger != null) logger.Error("VERIFY_FAILED", resultMessage);
                        return false;
                    }
                }

                resultMessage = string.Format("Successfully disabled autostart for '{0}'. Backup saved as {1}.", _entryName, backup.BackupId);
                if (logger != null) logger.Info("STARTUP_OPTIMIZED", resultMessage);
                return true;
            }
            catch (Exception ex)
            {
                resultMessage = "Exception executing action: " + ex.Message;
                if (logger != null) logger.Error("ACTION_EXCEPTION", resultMessage, ex);
                return false;
            }
        }
    }

    public class ActionRegistry
    {
        private readonly List<IOptimizationAction> _actions;

        public ActionRegistry()
        {
            _actions = new List<IOptimizationAction>
            {
                new StartupDisableAction(
                    "OPT_STARTUP_ONEDRIVE_DISABLE",
                    "OneDrive",
                    "Disable Microsoft OneDrive automatic background startup",
                    "Saves ~40-80 MB physical working set and reduces background disk indexing on 4 GB laptop.",
                    false),

                new StartupDisableAction(
                    "OPT_STARTUP_SPOTIFY_DISABLE",
                    "Spotify",
                    "Disable Spotify automatic startup",
                    "Saves ~80-150 MB private memory at login; Spotify remains fully usable on demand.",
                    false),

                new StartupDisableAction(
                    "OPT_STARTUP_UTWEB_DISABLE",
                    "utweb",
                    "Disable uTorrent Web automatic startup",
                    "Saves ~50-90 MB memory and network listening threads at startup.",
                    false),

                new StartupDisableAction(
                    "OPT_STARTUP_WARP_DISABLE",
                    "CloudflareWARP",
                    "Disable Cloudflare WARP background startup",
                    "Saves ~70 MB private bytes; WARP client remains fully intact for manual connection.",
                    true)
            };
        }

        public List<IOptimizationAction> GetAllActions()
        {
            return new List<IOptimizationAction>(_actions);
        }

        public IOptimizationAction GetAction(string actionId)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].ActionId.Equals(actionId, StringComparison.OrdinalIgnoreCase))
                {
                    return _actions[i];
                }
            }
            return null;
        }
    }
}
