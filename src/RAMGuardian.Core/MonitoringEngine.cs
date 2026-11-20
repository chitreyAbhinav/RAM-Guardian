using System;
using System.Collections.Generic;
using System.Threading;
using RAMGuardian.Common.Models;
using RAMGuardian.Configuration;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Core.Alerts;
using RAMGuardian.Core.Analysis;
using RAMGuardian.Logging;
using RAMGuardian.Monitoring;
using RAMGuardian.Optimization;
using RAMGuardian.Optimization.Actions;

namespace RAMGuardian.Service
{
    public class MonitoringEngine : IDisposable
    {
        private readonly SystemMemoryCollector _memCollector;
        private readonly ProcessMemoryCollector _procCollector;
        private readonly EdgeProcessCollector _edgeCollector;
        private readonly FileLogger _logger;
        private readonly SnapshotWriter _snapshotWriter;
        private readonly ConfigManager _configManager;
        private readonly TrendAnalyzer _trendAnalyzer;
        private readonly MemoryLeakDetector _leakDetector;
        private readonly NonpagedPoolTracker _poolTracker;
        private readonly OptimizationCoordinator _optCoordinator;
        private readonly List<MemoryAlert> _recentAlerts;

        private Timer _timer;
        private readonly DateTime _startTime;
        private int _intervalSeconds;
        private bool _isRunning;
        private bool _disposed;
        private long _cycleCount;

        public event Action<StateSnapshot> OnSnapshotGenerated;

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public MonitoringEngine(int intervalSeconds = 15)
        {
            _intervalSeconds = Math.Max(5, intervalSeconds);
            _startTime = DateTime.UtcNow;
            _logger = new FileLogger();
            _snapshotWriter = new SnapshotWriter();
            _configManager = new ConfigManager();
            _memCollector = new SystemMemoryCollector();
            _procCollector = new ProcessMemoryCollector();
            _edgeCollector = new EdgeProcessCollector();
            _trendAnalyzer = new TrendAnalyzer(120);
            _leakDetector = new MemoryLeakDetector();
            _poolTracker = new NonpagedPoolTracker();
            _optCoordinator = new OptimizationCoordinator(new ActionRegistry(), new BackupManager(), _logger);
            _recentAlerts = new List<MemoryAlert>();
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _logger.Info("ENGINE_START", string.Format("RAM Guardian monitoring engine started with interval {0}s.", _intervalSeconds));

            // Execute first cycle immediately
            ExecuteMonitoringCycle(null);

            // Schedule recurring cycles
            _timer = new Timer(ExecuteMonitoringCycle, null, _intervalSeconds * 1000, _intervalSeconds * 1000);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            if (_timer != null)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                _timer.Dispose();
                _timer = null;
            }

            _logger.Info("ENGINE_STOP", "RAM Guardian monitoring engine stopped.");
        }

        public StateSnapshot SampleOnce()
        {
            GuardianConfig config = _configManager.LoadConfig();
            var metrics = _memCollector.CollectMetrics();
            var topProcs = _procCollector.GetTopProcesses(20);

            // Collect Microsoft Edge intelligence
            var edgeResult = _edgeCollector.CollectEdgeIntelligence();
            var edgeMetrics = edgeResult.Item1;
            var edgeProcs = edgeResult.Item2;

            // Classify each process using ProcessIntelligenceEngine
            if (topProcs != null)
            {
                for (int i = 0; i < topProcs.Count; i++)
                {
                    _optCoordinator.TerminationEngine.IntelligenceEngine.ClassifyProcess(topProcs[i], config);
                }
            }

            var newAlerts = new List<MemoryAlert>();

            // 1. Ingest into Trend Analyzer (Composite Pressure Matrix + Hysteresis)
            string pressure = _trendAnalyzer.IngestMetrics(metrics, config, newAlerts);

            // 2. Ingest into Memory Leak Detector
            _leakDetector.IngestProcessSnapshot(topProcs, newAlerts);

            // 3. Ingest into Nonpaged Pool Tracker
            _poolTracker.IngestPoolSample(metrics, newAlerts);

            // 4. Safe & Controlled Optimization Coordinator
            string optSummary;
            if (_optCoordinator.EvaluateAndOptimize(config, _trendAnalyzer, topProcs, edgeMetrics, newAlerts, out optSummary))
            {
                if (!string.IsNullOrEmpty(optSummary))
                {
                    _logger.Info("OPTIMIZATION_EXECUTED", optSummary);
                }
            }

            // Append new alerts to recent alerts list
            lock (_recentAlerts)
            {
                for (int i = 0; i < newAlerts.Count; i++)
                {
                    _recentAlerts.Add(newAlerts[i]);
                    // Keep up to 20 alerts
                    if (_recentAlerts.Count > 20) _recentAlerts.RemoveAt(0);
                }
            }

            var alertStrings = new List<string>();
            lock (_recentAlerts)
            {
                for (int i = 0; i < _recentAlerts.Count; i++)
                {
                    alertStrings.Add(string.Format("[{0}] {1}: {2}",
                        _recentAlerts[i].Severity, _recentAlerts[i].Title, _recentAlerts[i].Message));
                }
            }

            var snapshot = new StateSnapshot
            {
                TimestampUtc = DateTime.UtcNow,
                OperatingMode = config.OperatingMode,
                PressureLevel = pressure,
                StatusMessage = string.Format("System {0}. Available: {1:F1} MB, Commit: {2:F1}%",
                    pressure, metrics.AvailablePhysicalMB, metrics.CommitLoadPercentage),
                ServiceUptimeSeconds = Math.Round((DateTime.UtcNow - _startTime).TotalSeconds, 1),
                AvailableRamSlopeMBPerMin = _trendAnalyzer.CalculateAvailableRamRateOfChangeMBPerMin(),
                NonpagedPoolBaselineMB = _poolTracker.BaselineMB,
                IsEmergencyStopped = config.IsEmergencyStopped,
                ActiveAlerts = alertStrings,
                Metrics = metrics,
                EdgeStatus = edgeMetrics,
                TopProcesses = topProcs,
                EdgeProcesses = edgeProcs
            };

            try
            {
                _snapshotWriter.WriteSnapshot(snapshot);
            }
            catch { }

            return snapshot;
        }

        private void ExecuteMonitoringCycle(object state)
        {
            if (!_isRunning && _timer != null) return;

            try
            {
                _cycleCount++;
                StateSnapshot snapshot = SampleOnce();

                // Periodic structured logging (every 4 cycles = ~1 minute, or immediately if pressure elevated/high/critical)
                bool isSignificant = snapshot.PressureLevel != "Normal" || (_cycleCount % 4 == 0);
                if (isSignificant)
                {
                    LogLevel level = LogLevel.Info;
                    if (snapshot.PressureLevel == "Elevated") level = LogLevel.Warning;
                    else if (snapshot.PressureLevel == "High" || snapshot.PressureLevel == "Critical") level = LogLevel.Error;

                    string logMsg = string.Format("Mode: {0} | RAM: {1:F1}/{2:F1} MB ({3:F1}%) | Avail: {4:F1} MB (Slope: {5:F1} MB/m) | Commit: {6:F1}/{7:F1} MB ({8:F1}%) | Nonpaged: {9:F1} MB | Pages/s: {10:F1} | Faults/s: {11:F1} | Status: {12}",
                        snapshot.OperatingMode,
                        snapshot.Metrics.UsedPhysicalMB,
                        snapshot.Metrics.TotalPhysicalMB,
                        snapshot.Metrics.MemoryLoadPercentage,
                        snapshot.Metrics.AvailablePhysicalMB,
                        snapshot.AvailableRamSlopeMBPerMin,
                        snapshot.Metrics.CommittedMB,
                        snapshot.Metrics.CommitLimitMB,
                        snapshot.Metrics.CommitLoadPercentage,
                        snapshot.Metrics.NonpagedPoolMB,
                        snapshot.Metrics.PagesPerSec,
                        snapshot.Metrics.PageFaultsPerSec,
                        snapshot.PressureLevel);

                    _logger.Log(level, "MEMORY_SAMPLE", logMsg);
                }

                // Raise event for listeners
                if (OnSnapshotGenerated != null)
                {
                    try { OnSnapshotGenerated(snapshot); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("CYCLE_ERROR", "Error executing monitoring cycle", ex);
            }
        }

        public static string EvaluatePressure(SystemMemoryMetrics m)
        {
            var analyzer = new TrendAnalyzer();
            return analyzer.IngestMetrics(m, null, null);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();

            if (_memCollector != null)
            {
                _memCollector.Dispose();
            }
        }
    }
}
