using System;
using System.Collections.Generic;
using System.IO;
using RAMGuardian.Common.Serialization;

namespace RAMGuardian.Security.Policies
{
    public class ProcessProtectionPolicy
    {
        // Core OS and system components that must never be touched
        private static readonly HashSet<string> ImmutableSystemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass",
            "winlogon", "fontdrvhost", "dwm", "explorer", "sihost", "taskhostw", "RuntimeBroker",
            "svchost", "SystemEventsBroker", "TimeBrokerServer", "conhost", "dllhost",
            "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost"
        };

        // Security components
        private static readonly HashSet<string> ImmutableSecurityProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MsMpEng", "NisSrv", "SecurityHealthService", "SecurityHealthSystray",
            "smartscreen", "SenseCncProxy", "MsSense"
        };

        // Windows Memory Manager
        private static readonly HashSet<string> ImmutableMemoryProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Memory Compression"
        };

        // Hardware, Driver, Audio, Power, and ASUS components
        private static readonly HashSet<string> ImmutableHardwareProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AsusAppService", "AsusLinkNear", "AsusLinkRemote", "AsusSoftwareManager",
            "AsusSystemAnalysis", "AsusSystemDiagnosis", "asus_framework",
            "igfxCUIService", "igfxEM", "OneApp.IGCC.WinService", "IntelCpHDCPSvc", "cphs",
            "RtkAudioService64", "RAVBg64", "ETDCtrl", "ETDService"
        };

        // Academic and Protected Testing Environments
        private static readonly HashSet<string> ImmutableAcademicProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SafeExamBrowser", "SafeExamBrowser.Client", "SafeExamBrowser.Service"
        };

        private readonly HashSet<string> _userProtectedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _customConfigPath;

        public ProcessProtectionPolicy(string customConfigPath = @"C:\ProgramData\RAM Guardian\protected-processes.json")
        {
            _customConfigPath = customConfigPath;
            LoadUserProtectedList();
        }

        private void LoadUserProtectedList()
        {
            try
            {
                if (File.Exists(_customConfigPath))
                {
                    string json = File.ReadAllText(_customConfigPath);
                    var list = JsonHelper.Deserialize<List<string>>(json);
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(list[i]))
                            {
                                _userProtectedProcesses.Add(list[i].Trim());
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Non-negotiable safety check: RAM Guardian NEVER automatically terminates running processes.
        /// Policy: DENY.
        /// </summary>
        public bool CanTerminateProcess(string processName)
        {
            return false;
        }

        public bool IsProcessProtected(string processName, out string reason)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                reason = "Unknown process (Default-DENY: Protected)";
                return true;
            }

            string cleanName = Path.GetFileNameWithoutExtension(processName).Trim();

            if (ImmutableSystemProcesses.Contains(cleanName))
            {
                reason = "Core Windows Operating System Component";
                return true;
            }

            if (ImmutableSecurityProcesses.Contains(cleanName))
            {
                reason = "Microsoft Defender / Windows Security Component";
                return true;
            }

            if (ImmutableMemoryProcesses.Contains(cleanName))
            {
                reason = "Windows Memory Compression System";
                return true;
            }

            if (ImmutableHardwareProcesses.Contains(cleanName))
            {
                reason = "ASUS / Intel / Hardware Driver Component";
                return true;
            }

            if (ImmutableAcademicProcesses.Contains(cleanName))
            {
                reason = "Safe Exam Browser Testing Component";
                return true;
            }

            if (_userProtectedProcesses.Contains(cleanName))
            {
                reason = "User-defined Protected Application";
                return true;
            }

            // By default policy: treat unknown processes as protected from disruption
            reason = "Standard Application (Default Protection Active)";
            return true;
        }
    }
}
