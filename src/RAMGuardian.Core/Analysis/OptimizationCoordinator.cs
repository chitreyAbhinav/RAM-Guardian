using System;
using System.Collections.Generic;
using RAMGuardian.Common.Models;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Core.Alerts;
using RAMGuardian.Logging;
using RAMGuardian.Optimization;
using RAMGuardian.Optimization.Actions;
using RAMGuardian.Security.Policies;

namespace RAMGuardian.Core.Analysis
{
    public class OptimizationCoordinator
    {
        private readonly ActionRegistry _actionRegistry;
        private readonly BackupManager _backupManager;
        private readonly FileLogger _logger;
        private readonly EdgeOptimizer _edgeOptimizer;
        private readonly ProcessTerminationEngine _terminationEngine;
        private readonly ProcessIntelligenceEngine _processIntelligence;
        private readonly List<string> _executedActionsThisSession;

        public EdgeOptimizer EdgeOptimizer
        {
            get { return _edgeOptimizer; }
        }

        public ProcessTerminationEngine TerminationEngine
        {
            get { return _terminationEngine; }
        }

        public OptimizationCoordinator(ActionRegistry actionRegistry = null, BackupManager backupManager = null, FileLogger logger = null)
        {
            _actionRegistry = actionRegistry ?? new ActionRegistry();
            _backupManager = backupManager ?? new BackupManager();
            _logger = logger ?? new FileLogger();
            _edgeOptimizer = new EdgeOptimizer(_backupManager, _logger);
            _processIntelligence = new ProcessIntelligenceEngine();
            _terminationEngine = new ProcessTerminationEngine(_processIntelligence, _logger);
            _executedActionsThisSession = new List<string>();
        }

        public bool EvaluateAndOptimize(
            GuardianConfig config,
            TrendAnalyzer trend,
            List<ProcessMemoryInfo> topProcs,
            EdgeMetrics edgeMetrics,
            List<MemoryAlert> alertCollector,
            out string summaryMessage)
        {
            summaryMessage = string.Empty;

            if (config == null || trend == null) return false;

            // 1. Emergency Killswitch Guard
            if (config.IsEmergencyStopped)
            {
                summaryMessage = "Emergency Stop active: All automated optimization suspended.";
                return false;
            }

            // 2. Mode Guard: Paused or MonitorOnly makes ZERO system changes
            if (config.OperatingMode == "Paused")
            {
                summaryMessage = "System is Paused.";
                return false;
            }

            if (config.OperatingMode == "MonitorOnly")
            {
                summaryMessage = "MonitorOnly mode: Telemetry collection and diagnostics only.";
                return false;
            }

            // 3. Built-in Microsoft Edge Optimization (Active across BalancedAuto, AggressiveAuto, GamingFocus)
            if (config.EdgeOptimizationEnabled)
            {
                string edgeMsg;
                if (_edgeOptimizer.OptimizeEdgePolicies(config, edgeMetrics, out edgeMsg))
                {
                    if (edgeMsg.Contains("Successfully"))
                    {
                        if (alertCollector != null)
                        {
                            alertCollector.Add(new MemoryAlert(
                                AlertSeverity.Info,
                                "EdgeOptimization",
                                "Built-in Microsoft Edge Optimization Applied",
                                edgeMsg,
                                "msedge"
                            ));
                        }
                    }
                }
            }

            // 4. Memory Pressure & Cooldown Check for Process/Startup Management
            bool pressureRequiresAction = (trend.CurrentPressureLevel == "High" || trend.CurrentPressureLevel == "Critical");
            if (config.OperatingMode == "GamingFocus" || config.OperatingMode == "AggressiveAuto")
            {
                // In GamingFocus and AggressiveAuto, the goal is 65%-70% RAM: act as soon as usage is Elevated (>70%)
                pressureRequiresAction = (trend.CurrentPressureLevel == "Elevated" || trend.CurrentPressureLevel == "High" || trend.CurrentPressureLevel == "Critical");
            }

            if (!pressureRequiresAction || !trend.CanAttemptOptimization(config))
            {
                return false;
            }

            // 5. Controlled Background Process Management (BalancedAuto, AggressiveAuto, and GamingFocus)
            if ((config.OperatingMode == "BalancedAuto" || config.OperatingMode == "AggressiveAuto" || config.OperatingMode == "GamingFocus") && topProcs != null)
            {
                for (int i = 0; i < topProcs.Count; i++)
                {
                    ProcessMemoryInfo p = topProcs[i];
                    _processIntelligence.ClassifyProcess(p, config);

                    if (p.IsTerminationEligible)
                    {
                        string termAudit;
                        if (_terminationEngine.TryGraduatedClosure(p, config, out termAudit))
                        {
                            trend.RecordOptimizationAttempt();
                            summaryMessage = termAudit;

                            if (alertCollector != null)
                            {
                                alertCollector.Add(new MemoryAlert(
                                    AlertSeverity.Info,
                                    "ControlledClosure",
                                    "Graduated Process Closure Executed",
                                    termAudit,
                                    p.ProcessName
                                ));
                            }
                            return true;
                        }
                    }
                }
            }

            // 6. Approved Startup Optimizations (Reversible)
            List<IOptimizationAction> actions = _actionRegistry.GetAllActions();
            for (int i = 0; i < actions.Count; i++)
            {
                IOptimizationAction action = actions[i];
                if (_executedActionsThisSession.Contains(action.ActionId)) continue;

                string preconditionReason;
                // For startup actions, mode is valid if BalancedAuto or AggressiveAuto
                var proxyConfig = new GuardianConfig { OperatingMode = "SafeAutomatic" };
                if (action.CanExecute(proxyConfig, out preconditionReason))
                {
                    string execResult;
                    bool ok = action.Execute(_backupManager, _logger, out execResult);
                    trend.RecordOptimizationAttempt();
                    _executedActionsThisSession.Add(action.ActionId);

                    if (ok)
                    {
                        summaryMessage = execResult;
                        if (alertCollector != null)
                        {
                            alertCollector.Add(new MemoryAlert(
                                AlertSeverity.Info,
                                "StartupOptimized",
                                "Approved Startup Optimization Executed",
                                execResult,
                                action.ActionId
                            ));
                        }
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
