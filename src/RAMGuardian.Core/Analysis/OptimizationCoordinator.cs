using System;
using System.Collections.Generic;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Core.Alerts;
using RAMGuardian.Logging;
using RAMGuardian.Optimization;
using RAMGuardian.Optimization.Actions;

namespace RAMGuardian.Core.Analysis
{
    public class OptimizationCoordinator
    {
        private readonly ActionRegistry _actionRegistry;
        private readonly BackupManager _backupManager;
        private readonly FileLogger _logger;
        private readonly List<string> _executedActionsThisSession;

        public OptimizationCoordinator(ActionRegistry actionRegistry = null, BackupManager backupManager = null, FileLogger logger = null)
        {
            _actionRegistry = actionRegistry ?? new ActionRegistry();
            _backupManager = backupManager ?? new BackupManager();
            _logger = logger ?? new FileLogger();
            _executedActionsThisSession = new List<string>();
        }

        public bool TryExecuteSafeOptimization(GuardianConfig config, TrendAnalyzer trend, List<MemoryAlert> alertCollector, out string actionSummary)
        {
            actionSummary = string.Empty;

            if (config == null || trend == null) return false;

            // 1. Check if allowed by operating mode and cooldown
            if (!trend.CanAttemptOptimization(config))
            {
                return false;
            }

            // 2. Iterate through approved actions
            List<IOptimizationAction> actions = _actionRegistry.GetAllActions();
            for (int i = 0; i < actions.Count; i++)
            {
                IOptimizationAction action = actions[i];

                // Skip actions already executed in this session
                if (_executedActionsThisSession.Contains(action.ActionId))
                {
                    continue;
                }

                string preconditionReason;
                if (action.CanExecute(config, out preconditionReason))
                {
                    _logger.Info("OPTIMIZATION_ATTEMPT", string.Format("Attempting approved action '{0}': {1}", action.ActionId, action.Description));

                    string execResult;
                    bool success = action.Execute(_backupManager, _logger, out execResult);

                    trend.RecordOptimizationAttempt();
                    _executedActionsThisSession.Add(action.ActionId);

                    if (success)
                    {
                        actionSummary = execResult;
                        if (alertCollector != null)
                        {
                            alertCollector.Add(new MemoryAlert(
                                AlertSeverity.Info,
                                "Optimization",
                                "Safe Startup Optimization Executed",
                                execResult,
                                action.ActionId
                            ));
                        }
                        return true;
                    }
                    else
                    {
                        _logger.Warn("OPTIMIZATION_SKIPPED", string.Format("Action '{0}' failed or skipped: {1}", action.ActionId, execResult));
                    }
                }
            }

            return false;
        }
    }
}
