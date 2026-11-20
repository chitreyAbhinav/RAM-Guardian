using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using RAMGuardian.Common.Models;
using RAMGuardian.Configuration.Models;

namespace RAMGuardian.Security.Policies
{
    public class ProcessIntelligenceEngine
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private readonly ProcessProtectionPolicy _baseProtectionPolicy;

        // Development & Compiler environments that must never be disrupted
        private static readonly HashSet<string> DevProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "code", "devenv", "rider64", "idea64", "clion64", "pycharm64",
            "node", "dotnet", "python", "pythonw", "java", "javac", "git",
            "msbuild", "csc", "cargo", "rustc", "go", "wsl", "wslhost",
            "docker", "dockerd", "mongod", "postgres", "mysqld", "redis-server"
        };

        // Virtual Machine Hosts
        private static readonly HashSet<string> VmProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vmware", "vmware-vmx", "vmware-authd", "vmware-tray", "vmnat", "vmnetdhcp", "VirtualBox"
        };

        // User Custom Scripting & Utilities
        private static readonly HashSet<string> UserCustomProtected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AutoHotkey", "AutoHotkeyUX", "IDMan", "Filee"
        };

        public ProcessIntelligenceEngine(ProcessProtectionPolicy baseProtectionPolicy = null)
        {
            _baseProtectionPolicy = baseProtectionPolicy ?? new ProcessProtectionPolicy();
        }

        public void ClassifyProcess(ProcessMemoryInfo info, GuardianConfig config)
        {
            if (info == null) return;

            string name = Path.GetFileNameWithoutExtension(info.ProcessName ?? string.Empty).Trim();
            int foregroundPid = GetForegroundProcessId();
            bool isForeground = (info.ProcessId == foregroundPid);

            // 1. Critical Windows OS
            string reason;
            if (_baseProtectionPolicy.IsProcessProtected(name, out reason))
            {
                if (reason.Contains("Operating System"))
                {
                    info.Classification = "CriticalWindows";
                    info.ClassificationConfidence = 1.0;
                    info.IsProtected = true;
                    info.ProtectionReason = reason;
                    info.IsTerminationEligible = false;
                    info.EligibilityReason = "Immune: Core Operating System component";
                    return;
                }
                else if (reason.Contains("Security") || reason.Contains("Defender"))
                {
                    info.Classification = "SecuritySoftware";
                    info.ClassificationConfidence = 1.0;
                    info.IsProtected = true;
                    info.ProtectionReason = reason;
                    info.IsTerminationEligible = false;
                    info.EligibilityReason = "Immune: Active security and antivirus system";
                    return;
                }
                else if (reason.Contains("Hardware") || reason.Contains("ASUS") || reason.Contains("Intel"))
                {
                    info.Classification = "EssentialHardware";
                    info.ClassificationConfidence = 1.0;
                    info.IsProtected = true;
                    info.ProtectionReason = reason;
                    info.IsTerminationEligible = false;
                    info.EligibilityReason = "Immune: Hardware driver / ASUS power management";
                    return;
                }
                else if (reason.Contains("Safe Exam Browser"))
                {
                    info.Classification = "AcademicTesting";
                    info.ClassificationConfidence = 1.0;
                    info.IsProtected = true;
                    info.ProtectionReason = reason;
                    info.IsTerminationEligible = false;
                    info.EligibilityReason = "Immune: Safe Exam Browser component";
                    return;
                }
            }

            // 2. Safe Exam Browser Extra Hardening
            if (name.StartsWith("SafeExamBrowser", StringComparison.OrdinalIgnoreCase))
            {
                info.Classification = "AcademicTesting";
                info.ClassificationConfidence = 1.0;
                info.IsProtected = true;
                info.ProtectionReason = "Safe Exam Browser Academic Testing Environment";
                info.IsTerminationEligible = false;
                info.EligibilityReason = "Immune: Testing environment locked against termination";
                return;
            }

            // 3. User Custom Tools (AutoHotkey, IDM, Filee)
            bool isAhk = (info.ProcessName != null && info.ProcessName.IndexOf(".ahk", StringComparison.OrdinalIgnoreCase) >= 0)
                         || name.IndexOf("ShortcutSwap", StringComparison.OrdinalIgnoreCase) >= 0
                         || name.IndexOf("WindowButtonDisable", StringComparison.OrdinalIgnoreCase) >= 0;

            if (UserCustomProtected.Contains(name) || isAhk)
            {
                info.Classification = "UserScripting";
                info.ClassificationConfidence = 1.0;
                info.IsProtected = true;
                info.ProtectionReason = "User custom tool or active AutoHotkey script (ShortcutSwap/WindowButtonDisable)";
                info.IsTerminationEligible = false;
                info.EligibilityReason = "Immune: User customized Windows behavior script";
                return;
            }

            // 4. Developer Tools & Development Servers
            if (DevProcesses.Contains(name))
            {
                info.Classification = "DevEnvironment";
                info.ClassificationConfidence = 0.95;
                info.IsProtected = true;
                info.ProtectionReason = "Active developer tool, server, or compiler process";
                info.IsTerminationEligible = false;
                info.EligibilityReason = "Immune: Developer environment protected from data loss";
                return;
            }

            // 5. Virtual Machines (VMware)
            if (VmProcesses.Contains(name))
            {
                info.Classification = "VirtualMachine";
                info.ClassificationConfidence = 0.95;
                info.IsProtected = true;
                info.ProtectionReason = "VMware Virtual Machine engine";
                info.IsTerminationEligible = false;
                info.EligibilityReason = "Immune: VMware preserved for active/on-demand VM operations";
                return;
            }

            // 6. Microsoft Edge Sessions
            if (name.Equals("msedge", StringComparison.OrdinalIgnoreCase))
            {
                info.Classification = "EdgeBrowser";
                info.ClassificationConfidence = 0.95;
                info.IsProtected = true;
                info.ProtectionReason = "Microsoft Edge session (Optimized via built-in Edge Engine, never blindly terminated)";
                info.IsTerminationEligible = false;
                info.EligibilityReason = "Managed by built-in 24/7 Edge optimizer";
                return;
            }

            // 7. Active User-Facing Window
            if (info.HasWindow || isForeground)
            {
                info.Classification = "ActiveUserApp";
                info.ClassificationConfidence = 0.9;
                info.IsProtected = true;
                info.ProtectionReason = isForeground ? "Active Foreground Application" : "Application has open user-facing window";
                info.IsTerminationEligible = false;
                info.EligibilityReason = "Preserved: User interface visible, risk of unsaved work";
                return;
            }

            // 8. Explicitly Approved Optimization Targets (e.g. OneDrive, Spotify, utweb, WARP when idle in background)
            bool isApprovedTarget = false;
            if (config != null && config.ApprovedTerminationTargets != null)
            {
                for (int i = 0; i < config.ApprovedTerminationTargets.Count; i++)
                {
                    if (name.Equals(config.ApprovedTerminationTargets[i], StringComparison.OrdinalIgnoreCase))
                    {
                        isApprovedTarget = true;
                        break;
                    }
                }
            }

            if (isApprovedTarget)
            {
                info.Classification = "ApprovedOptimizationTarget";
                info.ClassificationConfidence = 0.95;
                info.IsProtected = false;
                info.ProtectionReason = "Approved target for automated background management";

                // Evaluate termination eligibility based on mode and killswitch
                if (config != null && !config.IsEmergencyStopped &&
                    (config.OperatingMode == "BalancedAuto" || config.OperatingMode == "AggressiveAuto"))
                {
                    info.IsTerminationEligible = true;
                    info.EligibilityReason = string.Format("Eligible for graduated closure in mode '{0}'", config.OperatingMode);
                }
                else
                {
                    info.IsTerminationEligible = false;
                    info.EligibilityReason = "Target approved, but mode restricts closure";
                }
                return;
            }

            // 9. Unknown Process (Default-DENY Invariant)
            info.Classification = "Unknown";
            info.ClassificationConfidence = 0.5;
            info.IsProtected = true;
            info.ProtectionReason = "Unrecognized application (Default-DENY: Protected)";
            info.IsTerminationEligible = false;
            info.EligibilityReason = "Default-DENY: Unknown processes are immune to termination";
        }

        private static int GetForegroundProcessId()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd != IntPtr.Zero)
                {
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);
                    return (int)pid;
                }
            }
            catch { }
            return -1;
        }
    }
}
