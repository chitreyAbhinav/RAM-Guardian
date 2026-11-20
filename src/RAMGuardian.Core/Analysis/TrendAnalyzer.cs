using System;
using System.Collections.Generic;
using RAMGuardian.Common.Models;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Core.Alerts;

namespace RAMGuardian.Core.Analysis
{
    public class TrendAnalyzer
    {
        private readonly int _maxSamples;
        private readonly List<SystemMemoryMetrics> _history;
        private string _currentPressureLevel;
        private DateTime _pressureLevelStartTime;
        private DateTime _lastOptimizationAttemptTime;

        public string CurrentPressureLevel
        {
            get { return _currentPressureLevel; }
        }

        public TimeSpan CurrentPressureDuration
        {
            get { return DateTime.UtcNow - _pressureLevelStartTime; }
        }

        public TrendAnalyzer(int maxSamples = 120)
        {
            _maxSamples = Math.Max(30, maxSamples);
            _history = new List<SystemMemoryMetrics>();
            _currentPressureLevel = "Normal";
            _pressureLevelStartTime = DateTime.UtcNow;
            _lastOptimizationAttemptTime = DateTime.MinValue;
        }

        public string IngestMetrics(SystemMemoryMetrics m, GuardianConfig config, List<MemoryAlert> alertCollector)
        {
            if (m == null) return _currentPressureLevel;

            lock (_history)
            {
                _history.Add(m);
                if (_history.Count > _maxSamples)
                {
                    _history.RemoveAt(0);
                }
            }

            // Raw instant pressure evaluation
            string instantPressure = EvaluateInstantPressure(m, config);

            // Apply hysteresis and sustained duration filtering
            string filteredPressure = ApplyHysteresis(instantPressure, m, config, alertCollector);

            return filteredPressure;
        }

        private static string EvaluateInstantPressure(SystemMemoryMetrics m, GuardianConfig cfg)
        {
            double elevRam = cfg != null ? cfg.ElevatedAvailableRamMB : 1150.0;
            double highRam = cfg != null ? cfg.HighAvailableRamMB : 950.0;
            double critRam = cfg != null ? cfg.CriticalAvailableRamMB : 550.0;

            double elevCommit = cfg != null ? cfg.ElevatedCommitPercentage : 68.0;
            double highCommit = cfg != null ? cfg.HighCommitPercentage : 75.0;
            double critCommit = cfg != null ? cfg.CriticalCommitPercentage : 85.0;

            double highPages = cfg != null ? cfg.HighPagingPagesPerSec : 1500.0;
            double critPages = cfg != null ? cfg.CriticalPagingPagesPerSec : 2500.0;

            // Extreme Case: RAM > 85%, or critical low RAM with high commit/paging
            if (m.MemoryLoadPercentage > 85.0 || (m.AvailablePhysicalMB < critRam && (m.CommitLoadPercentage > critCommit || m.PagesPerSec > critPages)))
            {
                return "Critical";
            }

            // High Pressure: RAM > 75%, or high paging/commit
            if (m.MemoryLoadPercentage > 75.0 || (m.AvailablePhysicalMB < highRam && (m.CommitLoadPercentage > highCommit || m.PagesPerSec > highPages)))
            {
                return "High";
            }

            // Elevated: RAM exceeds target 70% threshold (Target Goal: 65% - 70% usage)
            if (m.MemoryLoadPercentage > 70.0 || m.AvailablePhysicalMB < elevRam || m.CommitLoadPercentage > elevCommit)
            {
                return "Elevated";
            }

            return "Normal";
        }

        private string ApplyHysteresis(string instantPressure, SystemMemoryMetrics m, GuardianConfig cfg, List<MemoryAlert> alerts)
        {
            DateTime now = DateTime.UtcNow;

            if (instantPressure == _currentPressureLevel)
            {
                // Level unchanged
                return _currentPressureLevel;
            }

            int rankInstant = PressureRank(instantPressure);
            int rankCurrent = PressureRank(_currentPressureLevel);

            if (rankInstant > rankCurrent)
            {
                // Escalation: Require sustained duration to avoid flapping on transient spikes
                // Elevated: immediate (10s)
                // High: requires 20s
                // Critical: requires 30s
                double requiredDurationSeconds = 15.0;
                if (instantPressure == "High") requiredDurationSeconds = 25.0;
                if (instantPressure == "Critical") requiredDurationSeconds = 35.0;

                // Check if recent history has been consistently at or above this rank
                if (IsSustainedAboveRank(rankInstant, requiredDurationSeconds, cfg))
                {
                    _currentPressureLevel = instantPressure;
                    _pressureLevelStartTime = now;

                    if (alerts != null && (_currentPressureLevel == "High" || _currentPressureLevel == "Critical"))
                    {
                        AlertSeverity sev = _currentPressureLevel == "Critical" ? AlertSeverity.Critical : AlertSeverity.High;
                        alerts.Add(new MemoryAlert(
                            sev,
                            "Pressure",
                            string.Format("Sustained {0} Resource Pressure Detected", _currentPressureLevel),
                            string.Format("Available RAM is {0:F1} MB, Commit load is {1:F1}%, Paging is {2:F1} pages/sec.",
                                m.AvailablePhysicalMB, m.CommitLoadPercentage, m.PagesPerSec)
                        ));
                    }
                }
            }
            else
            {
                // De-escalation: Require 45s of recovery before lowering pressure state
                if (IsSustainedBelowRank(rankCurrent, 45.0, cfg))
                {
                    _currentPressureLevel = instantPressure;
                    _pressureLevelStartTime = now;
                }
            }

            return _currentPressureLevel;
        }

        private bool IsSustainedAboveRank(int targetRank, double durationSeconds, GuardianConfig cfg)
        {
            lock (_history)
            {
                if (_history.Count < 2) return true;
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-durationSeconds);

                for (int i = _history.Count - 1; i >= 0; i--)
                {
                    if (_history[i].TimestampUtc < cutoff) break;
                    string p = EvaluateInstantPressure(_history[i], cfg);
                    if (PressureRank(p) < targetRank)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private bool IsSustainedBelowRank(int currentRank, double durationSeconds, GuardianConfig cfg)
        {
            lock (_history)
            {
                if (_history.Count < 2) return true;
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-durationSeconds);

                for (int i = _history.Count - 1; i >= 0; i--)
                {
                    if (_history[i].TimestampUtc < cutoff) break;
                    string p = EvaluateInstantPressure(_history[i], cfg);
                    if (PressureRank(p) >= currentRank)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private static int PressureRank(string p)
        {
            switch (p)
            {
                case "Critical": return 3;
                case "High": return 2;
                case "Elevated": return 1;
                default: return 0;
            }
        }

        public bool CanAttemptOptimization(GuardianConfig config)
        {
            if (config == null || config.IsEmergencyStopped)
            {
                return false;
            }

            if (config.OperatingMode == "MonitorOnly" || config.OperatingMode == "Paused")
            {
                return false;
            }

            int cooldownMinutes = Math.Max(1, config.CooldownMinutes);
            if ((DateTime.UtcNow - _lastOptimizationAttemptTime).TotalMinutes < cooldownMinutes)
            {
                return false;
            }

            // In GamingFocus and AggressiveAuto, target 65%-70% RAM: optimize as soon as usage exceeds 70% (Elevated, High, or Critical)
            if (config.OperatingMode == "GamingFocus" || config.OperatingMode == "AggressiveAuto")
            {
                return _currentPressureLevel == "Elevated" || _currentPressureLevel == "High" || _currentPressureLevel == "Critical";
            }

            // BalancedAuto requires High or Critical
            return _currentPressureLevel == "High" || _currentPressureLevel == "Critical";
        }

        public void RecordOptimizationAttempt()
        {
            _lastOptimizationAttemptTime = DateTime.UtcNow;
        }

        public double CalculateAvailableRamRateOfChangeMBPerMin()
        {
            lock (_history)
            {
                if (_history.Count < 4) return 0.0;
                var newest = _history[_history.Count - 1];
                var oldest = _history[0];

                double minutes = (newest.TimestampUtc - oldest.TimestampUtc).TotalMinutes;
                if (minutes <= 0.1) return 0.0;

                return Math.Round((newest.AvailablePhysicalMB - oldest.AvailablePhysicalMB) / minutes, 2);
            }
        }

        public List<SystemMemoryMetrics> GetHistorySnapshot()
        {
            lock (_history)
            {
                return new List<SystemMemoryMetrics>(_history);
            }
        }
    }
}
