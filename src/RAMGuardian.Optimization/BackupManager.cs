using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Optimization.Models;

namespace RAMGuardian.Optimization
{
    public class BackupManager
    {
        private static readonly object SyncLock = new object();
        private readonly string _backupsDirectory;

        public BackupManager(string backupsDirectory = @"C:\ProgramData\RAM Guardian\backups")
        {
            _backupsDirectory = backupsDirectory;
            EnsureDirectory();
        }

        private void EnsureDirectory()
        {
            try
            {
                if (!Directory.Exists(_backupsDirectory))
                {
                    Directory.CreateDirectory(_backupsDirectory);
                }
            }
            catch { }
        }

        public OptimizationBackup CreateBackup(StartupEntry entry, string actionId, string reason)
        {
            if (entry == null) return null;

            lock (SyncLock)
            {
                EnsureDirectory();

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string safeName = SanitizeFileName(entry.Name);
                string backupId = string.Format("BKP_{0}_{1}", timestamp, safeName);

                var backup = new OptimizationBackup
                {
                    BackupId = backupId,
                    CreatedUtc = DateTime.UtcNow,
                    ActionId = actionId,
                    Reason = reason,
                    OriginalEntry = entry,
                    IsRestored = false
                };

                string filePath = Path.Combine(_backupsDirectory, string.Format("{0}.json", backupId));
                string json = JsonHelper.Serialize(backup, true);
                File.WriteAllText(filePath, json, Encoding.UTF8);

                return backup;
            }
        }

        public List<OptimizationBackup> GetBackups()
        {
            var list = new List<OptimizationBackup>();

            lock (SyncLock)
            {
                try
                {
                    if (!Directory.Exists(_backupsDirectory)) return list;

                    string[] files = Directory.GetFiles(_backupsDirectory, "BKP_*.json");
                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            string json = File.ReadAllText(files[i], Encoding.UTF8);
                            var bkp = JsonHelper.Deserialize<OptimizationBackup>(json);
                            if (bkp != null)
                            {
                                list.Add(bkp);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Sort newest first
            list.Sort((a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
            return list;
        }

        public bool RestoreBackup(string backupId, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrEmpty(backupId))
            {
                errorMessage = "Invalid backup ID.";
                return false;
            }

            lock (SyncLock)
            {
                string filePath = Path.Combine(_backupsDirectory, string.Format("{0}.json", backupId));
                if (!File.Exists(filePath))
                {
                    errorMessage = "Backup file not found on disk.";
                    return false;
                }

                try
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var backup = JsonHelper.Deserialize<OptimizationBackup>(json);
                    if (backup == null || backup.OriginalEntry == null)
                    {
                        errorMessage = "Corrupted backup record.";
                        return false;
                    }

                    StartupEntry orig = backup.OriginalEntry;

                    if (orig.Location == "HKCU_Run")
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            if (key == null)
                            {
                                errorMessage = "Unable to open HKCU Run registry key.";
                                return false;
                            }
                            key.SetValue(orig.Name, orig.CommandLine);
                        }
                    }
                    else if (orig.Location == "HKLM_Run")
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            if (key == null)
                            {
                                errorMessage = "Unable to open HKLM Run registry key (requires administrator rights).";
                                return false;
                            }
                            key.SetValue(orig.Name, orig.CommandLine);
                        }
                    }
                    else
                    {
                        errorMessage = "Unsupported restore location: " + orig.Location;
                        return false;
                    }

                    // Mark as restored in backup file
                    backup.IsRestored = true;
                    backup.RestoredUtc = DateTime.UtcNow;
                    File.WriteAllText(filePath, JsonHelper.Serialize(backup, true), Encoding.UTF8);

                    return true;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }
            }
        }

        public int RestoreAll(out List<string> errors)
        {
            errors = new List<string>();
            var backups = GetBackups();
            int restoredCount = 0;

            for (int i = 0; i < backups.Count; i++)
            {
                if (!backups[i].IsRestored)
                {
                    string err;
                    if (RestoreBackup(backups[i].BackupId, out err))
                    {
                        restoredCount++;
                    }
                    else
                    {
                        errors.Add(string.Format("Failed to restore {0}: {1}", backups[i].BackupId, err));
                    }
                }
            }

            return restoredCount;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            char[] invalids = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalids.Length; i++)
            {
                name = name.Replace(invalids[i], '_');
            }
            return name.Trim();
        }
    }
}
