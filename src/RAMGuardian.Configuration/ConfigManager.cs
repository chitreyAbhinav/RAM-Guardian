using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Configuration.Models;

namespace RAMGuardian.Configuration
{
    public class ConfigManager
    {
        private static readonly object FileLock = new object();
        private readonly string _baseDirectory;
        private readonly string _configFilePath;
        private readonly string _protectedProcessesPath;
        private readonly string _protectedServicesPath;

        public ConfigManager(string baseDirectory = @"C:\ProgramData\RAM Guardian")
        {
            _baseDirectory = baseDirectory;
            _configFilePath = Path.Combine(_baseDirectory, "config.json");
            _protectedProcessesPath = Path.Combine(_baseDirectory, "protected-processes.json");
            _protectedServicesPath = Path.Combine(_baseDirectory, "protected-services.json");

            EnsureDirectoriesAndDefaults();
        }

        private void EnsureDirectoriesAndDefaults()
        {
            try
            {
                EnsureDirectories();

                if (!File.Exists(_configFilePath))
                {
                    WriteConfigInternal(new GuardianConfig());
                }

                if (!File.Exists(_protectedProcessesPath))
                {
                    var defaultUserProcesses = new List<string>
                    {
                        "AutoHotkey", "AutoHotkeyUX", "code", "devenv", "git", "node", "python"
                    };
                    File.WriteAllText(_protectedProcessesPath, JsonHelper.Serialize(defaultUserProcesses, true), Encoding.UTF8);
                }

                if (!File.Exists(_protectedServicesPath))
                {
                    var defaultUserServices = new List<string>
                    {
                        "VMware", "VMAuthdService", "VMUSBArbService"
                    };
                    File.WriteAllText(_protectedServicesPath, JsonHelper.Serialize(defaultUserServices, true), Encoding.UTF8);
                }
            }
            catch { }
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }

            string backupsDir = Path.Combine(_baseDirectory, "backups");
            if (!Directory.Exists(backupsDir))
            {
                Directory.CreateDirectory(backupsDir);
            }

            string logsDir = Path.Combine(_baseDirectory, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
        }

        public GuardianConfig LoadConfig()
        {
            lock (FileLock)
            {
                try
                {
                    if (File.Exists(_configFilePath))
                    {
                        string json = File.ReadAllText(_configFilePath, Encoding.UTF8);
                        var config = JsonHelper.Deserialize<GuardianConfig>(json);
                        if (config != null)
                        {
                            ValidateConfig(config);
                            return config;
                        }
                    }
                }
                catch (Exception)
                {
                    // If JSON was corrupted by user or disk error, archive corrupted file and revert to safe defaults
                    try
                    {
                        string corruptBackup = Path.Combine(_baseDirectory, string.Format("config_corrupted_{0:yyyyMMddHHmmss}.json", DateTime.UtcNow));
                        File.Copy(_configFilePath, corruptBackup, true);
                    }
                    catch { }
                }

                // Safe fallback default
                var fallback = new GuardianConfig();
                WriteConfigInternal(fallback);
                return fallback;
            }
        }

        public bool SaveConfig(GuardianConfig config)
        {
            if (config == null) return false;

            lock (FileLock)
            {
                try
                {
                    EnsureDirectories();
                    ValidateConfig(config);
                    return WriteConfigInternal(config);
                }
                catch
                {
                    return false;
                }
            }
        }

        private bool WriteConfigInternal(GuardianConfig config)
        {
            try
            {
                string json = JsonHelper.Serialize(config, true);
                string tempFile = _configFilePath + ".tmp";

                File.WriteAllText(tempFile, json, Encoding.UTF8);
                if (File.Exists(_configFilePath))
                {
                    File.Delete(_configFilePath);
                }
                File.Move(tempFile, _configFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateConfig(GuardianConfig c)
        {
            // Enforce safe bounds
            if (c.MonitoringIntervalSeconds < 5) c.MonitoringIntervalSeconds = 5;
            if (c.DashboardRefreshIntervalSeconds < 2) c.DashboardRefreshIntervalSeconds = 2;
            if (c.CooldownMinutes < 1) c.CooldownMinutes = 1;
            if (c.EdgeSleepingTabsTimeoutSeconds < 30) c.EdgeSleepingTabsTimeoutSeconds = 30;

            if (c.OperatingMode != "MonitorOnly" &&
                c.OperatingMode != "BalancedAuto" &&
                c.OperatingMode != "AggressiveAuto" &&
                c.OperatingMode != "GamingFocus" &&
                c.OperatingMode != "Paused")
            {
                c.OperatingMode = "MonitorOnly";
            }

            if (c.ApprovedTerminationTargets == null)
            {
                c.ApprovedTerminationTargets = new List<string> { "OneDrive", "utweb", "Spotify" };
            }

            if (c.EdgeProtectedWebsites == null)
            {
                c.EdgeProtectedWebsites = new List<string> { "*.google.com", "*.youtube.com", "*.teams.microsoft.com" };
            }
        }
    }
}
