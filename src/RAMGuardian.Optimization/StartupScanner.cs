using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using RAMGuardian.Optimization.Models;
using RAMGuardian.Security.Policies;

namespace RAMGuardian.Optimization
{
    public class StartupScanner
    {
        private readonly StartupProtectionPolicy _policy;
        private readonly BackupManager _backupManager;

        public StartupScanner(StartupProtectionPolicy policy = null, BackupManager backupManager = null)
        {
            _policy = policy ?? new StartupProtectionPolicy();
            _backupManager = backupManager ?? new BackupManager();
        }

        public List<StartupEntry> ScanAll()
        {
            var results = new List<StartupEntry>();

            // 1. Scan HKCU Run
            ScanRegistryRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU_Run", results);

            // 2. Scan HKLM Run
            ScanRegistryRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM_Run", results);

            // 3. Scan User Startup Folder
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            ScanStartupFolder(userStartup, "UserStartupFolder", results);

            // 4. Scan Common Startup Folder
            string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            ScanStartupFolder(commonStartup, "CommonStartupFolder", results);

            return results;
        }

        private void ScanRegistryRunKey(RegistryKey rootKey, string subKeyPath, string locationName, List<StartupEntry> list)
        {
            try
            {
                using (RegistryKey key = rootKey.OpenSubKey(subKeyPath, false))
                {
                    if (key == null) return;

                    string[] valueNames = key.GetValueNames();
                    for (int i = 0; i < valueNames.Length; i++)
                    {
                        string name = valueNames[i];
                        object val = key.GetValue(name);
                        string cmd = val != null ? val.ToString() : string.Empty;

                        string reason;
                        bool approved = _policy.IsApprovedForOptimization(name, cmd, out reason);
                        bool isProtected = _policy.IsEntryProtected(name);

                        var entry = new StartupEntry
                        {
                            Id = string.Format("{0}_{1}", locationName, name),
                            Name = name,
                            Location = locationName,
                            RegistryKeyPath = string.Format(@"{0}\{1}", rootKey.Name, subKeyPath),
                            CommandLine = cmd,
                            TargetExecutable = ExtractExecutable(cmd),
                            IsApprovedForOptimization = approved,
                            IsProtected = isProtected,
                            ProtectionReason = reason,
                            Status = "Active"
                        };

                        list.Add(entry);
                    }
                }
            }
            catch { }
        }

        private void ScanStartupFolder(string folderPath, string locationName, List<StartupEntry> list)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

                string[] files = Directory.GetFiles(folderPath);
                for (int i = 0; i < files.Length; i++)
                {
                    string filePath = files[i];
                    string name = Path.GetFileName(filePath);

                    string reason;
                    bool approved = _policy.IsApprovedForOptimization(name, filePath, out reason);
                    bool isProtected = _policy.IsEntryProtected(name);

                    var entry = new StartupEntry
                    {
                        Id = string.Format("{0}_{1}", locationName, name),
                        Name = name,
                        Location = locationName,
                        FilePath = filePath,
                        CommandLine = filePath,
                        TargetExecutable = filePath,
                        IsApprovedForOptimization = approved,
                        IsProtected = isProtected,
                        ProtectionReason = reason,
                        Status = "Active"
                    };

                    list.Add(entry);
                }
            }
            catch { }
        }

        private static string ExtractExecutable(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return string.Empty;
            string trimmed = cmd.Trim();

            if (trimmed.StartsWith("\""))
            {
                int endQuote = trimmed.IndexOf('\"', 1);
                if (endQuote > 1)
                {
                    return trimmed.Substring(1, endQuote - 1);
                }
            }

            int firstSpace = trimmed.IndexOf(' ');
            return firstSpace > 0 ? trimmed.Substring(0, firstSpace) : trimmed;
        }
    }
}
