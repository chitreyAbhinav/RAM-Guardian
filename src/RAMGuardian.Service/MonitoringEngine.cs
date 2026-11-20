using System;
using System.Collections.Generic;
using System.Threading;
using RAMGuardian.Common.Models;
using RAMGuardian.Logging;
using RAMGuardian.Monitoring;

namespace RAMGuardian.Service
{
    public class MonitoringEngine : IDisposable
    {
        private readonly SystemMemoryCollector _memCollector;
        private readonly ProcessMemoryCollector _procCollector;
        private readonly FileLogger _logger;
        private readonly SnapshotWriter _snapshotWriter;
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
            _memCollector = new SystemMemoryCollector();
            _procCollector = new ProcessMemoryCollector();
            _logger = new FileLogger();
            _snapshotWriter = new SnapshotWriter();
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
            var metrics = _memCollector.CollectMetrics();
            var topProcs = _procCollector.GetTopProcesses(20);
            string pressure = EvaluatePressure(metrics);

            var snapshot = new StateSnapshot
            {
                TimestampUtc = DateTime.UtcNow,
                OperatingMode = "MonitorOnly",
                PressureLevel = pressure,
                StatusMessage = string.Format("System {0}. Available: {1:F1} MB, Commit: {2:F1}%",
                    pressure, metrics.AvailablePhysicalMB, metrics.CommitLoadPercentage),
                ServiceUptimeSeconds = Math.Round((DateTime.UtcNow - _startTime).TotalSeconds, 1),
                Metrics = metrics,
                TopProcesses = topProcs
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

                // Write atomic snapshot
                _snapshotWriter.WriteSnapshot(snapshot);

                // Periodic structured logging (every 4 cycles = ~1 minute, or immediately if pressure elevated/high/critical)
                bool isSignificant = snapshot.PressureLevel != "Normal" || (_cycleCount % 4 == 0);
                if (isSignificant)
                {
                    LogLevel level = LogLevel.Info;
                    if (snapshot.PressureLevel == "Elevated") level = LogLevel.Warning;
                    else if (snapshot.PressureLevel == "High" || snapshot.PressureLevel == "Critical") level = LogLevel.Error;

                    string logMsg = string.Format("RAM: {0:F1}/{1:F1} MB ({2:F1}% used) | Avail: {3:F1} MB | Commit: {4:F1}/{5:F1} MB ({6:F1}%) | Nonpaged: {7:F1} MB | Pages/s: {8:F1} | Faults/s: {9:F1} | Pressure: {10}",
                        snapshot.Metrics.UsedPhysicalMB,
                        snapshot.Metrics.TotalPhysicalMB,
                        snapshot.Metrics.MemoryLoadPercentage,
                        snapshot.Metrics.AvailablePhysicalMB,
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
            if (m == null) return "Normal";

            // Multi-criteria composite pressure matrix:
            // 4 GB Machine Thresholds:
            // Critical: Available < 150 MB AND (Commit > 90% OR Pages/sec > 2500)
            // High: Available < 250 MB AND (Commit > 82% OR Pages/sec > 1500)
            // Elevated: Available < 500 MB OR Commit > 78%
            // Normal: All other states

            if (m.AvailablePhysicalMB < 150 && (m.CommitLoadPercentage > 90.0 || m.PagesPerSec > 2500))
            {
                return "Critical";
            }

            if (m.AvailablePhysicalMB < 250 && (m.CommitLoadPercentage > 82.0 || m.PagesPerSec > 1500))
            {
                return "High";
            }

            if (m.AvailablePhysicalMB < 500 || m.CommitLoadPercentage > 78.0)
            {
                return "Elevated";
            }

            return "Normal";
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
