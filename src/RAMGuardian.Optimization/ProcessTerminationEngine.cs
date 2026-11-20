using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using RAMGuardian.Common.Models;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Logging;
using RAMGuardian.Security.Policies;

namespace RAMGuardian.Optimization
{
    public class ProcessTerminationEngine
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        private const uint WM_CLOSE = 0x0010;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        private readonly ProcessIntelligenceEngine _intelligence;
        private readonly FileLogger _logger;

        public ProcessIntelligenceEngine IntelligenceEngine
        {
            get { return _intelligence; }
        }

        public ProcessTerminationEngine(ProcessIntelligenceEngine intelligence = null, FileLogger logger = null)
        {
            _intelligence = intelligence ?? new ProcessIntelligenceEngine();
            _logger = logger ?? new FileLogger();
        }

        public bool TryGraduatedClosure(ProcessMemoryInfo target, GuardianConfig config, out string auditMessage)
        {
            auditMessage = string.Empty;

            if (target == null || config == null)
            {
                auditMessage = "Invalid parameter (target or config is null).";
                return false;
            }

            // Global Emergency Stop Check
            if (config.IsEmergencyStopped)
            {
                auditMessage = "Action aborted: Global Emergency Stop switch is ACTIVE.";
                _logger.Warn("TERMINATION_ABORTED", auditMessage);
                return false;
            }

            // Mode Check: Only BalancedAuto, AggressiveAuto, and GamingFocus permit process management
            if (config.OperatingMode != "BalancedAuto" && config.OperatingMode != "AggressiveAuto" && config.OperatingMode != "GamingFocus")
            {
                auditMessage = string.Format("Action prohibited in mode '{0}'. Only BalancedAuto, AggressiveAuto, and GamingFocus permit automated closure.", config.OperatingMode);
                return false;
            }

            // LEVEL 1: Observe & Invariant Validation
            _intelligence.ClassifyProcess(target, config);

            if (target.IsProtected)
            {
                auditMessage = string.Format("Safety Invariant: Process '{0}' (PID {1}) is PROTECTED ({2}). Action denied.",
                    target.ProcessName, target.ProcessId, target.ProtectionReason);
                _logger.Warn("TERMINATION_DENIED", auditMessage);
                return false;
            }

            if (!target.IsTerminationEligible)
            {
                auditMessage = string.Format("Policy Deny: Process '{0}' (PID {1}) is not eligible ({2}). Action denied.",
                    target.ProcessName, target.ProcessId, target.EligibilityReason);
                _logger.Warn("TERMINATION_DENIED", auditMessage);
                return false;
            }

            Process proc = null;
            try
            {
                proc = Process.GetProcessById(target.ProcessId);
            }
            catch (ArgumentException)
            {
                auditMessage = string.Format("Process '{0}' (PID {1}) already exited.", target.ProcessName, target.ProcessId);
                return true;
            }

            using (proc)
            {
                // LEVEL 2: Request Graceful Closure
                _logger.Info("GRADUATED_L2", string.Format("Requesting graceful closure of '{0}' (PID {1}, WorkingSet: {2:F1} MB)...",
                    target.ProcessName, target.ProcessId, target.WorkingSetMB));

                IntPtr hMain = proc.MainWindowHandle;

                if (hMain != IntPtr.Zero)
                {
                    IntPtr result;
                    SendMessageTimeout(hMain, WM_CLOSE, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out result);
                }
                else
                {
                    try
                    {
                        proc.CloseMainWindow();
                    }
                    catch { }
                }

                // Wait up to 3 seconds for graceful exit
                bool exited = proc.WaitForExit(3000);
                if (exited)
                {
                    auditMessage = string.Format("Success (Level 2): Process '{0}' (PID {1}) exited gracefully. Reclaimed {2:F1} MB RAM.",
                        target.ProcessName, target.ProcessId, target.WorkingSetMB);
                    _logger.Info("PROCESS_CLOSED", auditMessage);
                    return true;
                }

                // LEVEL 3: Reassess
                proc.Refresh();
                if (proc.HasExited)
                {
                    auditMessage = string.Format("Success (Level 3): Process '{0}' (PID {1}) exited.", target.ProcessName, target.ProcessId);
                    _logger.Info("PROCESS_CLOSED", auditMessage);
                    return true;
                }

                // Check if process opened a modal window or became active
                if (proc.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(proc.MainWindowTitle))
                {
                    auditMessage = string.Format("Abort (Level 3): Process '{0}' (PID {1}) opened a user window ('{2}'). May contain unsaved work.",
                        target.ProcessName, target.ProcessId, proc.MainWindowTitle);
                    _logger.Warn("CLOSURE_ABORTED", auditMessage);
                    return false;
                }

                // In BalancedAuto mode: We NEVER escalate to Level 4 forced termination
                if (config.OperatingMode == "BalancedAuto")
                {
                    auditMessage = string.Format("BalancedAuto Policy: Process '{0}' did not respond to graceful closure. Forced termination prohibited in BalancedAuto.",
                        target.ProcessName);
                    _logger.Info("CLOSURE_COMPLETE", auditMessage);
                    return false;
                }

                // LEVEL 4: Controlled Termination (AggressiveAuto and GamingFocus extreme cases)
                if (config.OperatingMode == "AggressiveAuto" || config.OperatingMode == "GamingFocus")
                {
                    _logger.Warn("GRADUATED_L4", string.Format("Executing controlled termination on approved target '{0}' (PID {1})...",
                        target.ProcessName, target.ProcessId));

                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(1500);

                        auditMessage = string.Format("Success (Level 4): Process '{0}' (PID {1}) terminated under {2} policy. Reclaimed {3:F1} MB RAM.",
                            target.ProcessName, target.ProcessId, config.OperatingMode, target.WorkingSetMB);
                        _logger.Info("PROCESS_TERMINATED", auditMessage);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        auditMessage = string.Format("Level 4 Error: Failed to terminate process '{0}': {1}", target.ProcessName, ex.Message);
                        _logger.Error("TERMINATION_FAILED", auditMessage, ex);
                        return false;
                    }
                }
            }

            return false;
        }
    }
}
