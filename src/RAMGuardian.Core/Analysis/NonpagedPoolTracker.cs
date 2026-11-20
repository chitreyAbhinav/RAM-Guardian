using System;
using System.Collections.Generic;
using RAMGuardian.Common.Models;
using RAMGuardian.Core.Alerts;

namespace RAMGuardian.Core.Analysis
{
    public class NonpagedPoolTracker
    {
        private long _baselineBytes;
        private long _peakBytes;
        private DateTime _firstSeenUtc;
        private DateTime _lastSeenUtc;
        private bool _isInitialized;
        private readonly List<Tuple<DateTime, long>> _poolSamples;
        private bool _alertEmitted;

        public double BaselineMB
        {
            get { return _baselineBytes / (1024.0 * 1024.0); }
        }

        public double PeakMB
        {
            get { return _peakBytes / (1024.0 * 1024.0); }
        }

        public double CurrentNetGrowthMB(long currentBytes)
        {
            return (currentBytes - _baselineBytes) / (1024.0 * 1024.0);
        }

        public NonpagedPoolTracker()
        {
            _poolSamples = new List<Tuple<DateTime, long>>();
            _firstSeenUtc = DateTime.UtcNow;
            _lastSeenUtc = DateTime.UtcNow;
        }

        public void IngestPoolSample(SystemMemoryMetrics m, List<MemoryAlert> alertCollector)
        {
            if (m == null || m.NonpagedPoolBytes <= 0) return;

            DateTime now = DateTime.UtcNow;
            long currentBytes = m.NonpagedPoolBytes;

            if (!_isInitialized)
            {
                _baselineBytes = currentBytes;
                _peakBytes = currentBytes;
                _firstSeenUtc = now;
                _lastSeenUtc = now;
                _isInitialized = true;
            }

            if (currentBytes > _peakBytes)
            {
                _peakBytes = currentBytes;
            }

            _lastSeenUtc = now;

            lock (_poolSamples)
            {
                _poolSamples.Add(new Tuple<DateTime, long>(now, currentBytes));
                // Retain up to 100 samples
                if (_poolSamples.Count > 100)
                {
                    _poolSamples.RemoveAt(0);
                }
            }

            // Check if pool is growing abnormally:
            // e.g. grew by > 200 MB and has been observed for > 15 minutes
            double netGrowthMB = CurrentNetGrowthMB(currentBytes);
            double totalHours = (now - _firstSeenUtc).TotalHours;

            if (netGrowthMB > 200.0 && totalHours >= 0.25 && !_alertEmitted)
            {
                _alertEmitted = true;

                if (alertCollector != null)
                {
                    alertCollector.Add(new MemoryAlert(
                        AlertSeverity.Warning,
                        "NonpagedPoolGrowth",
                        "Elevated Kernel Nonpaged Pool Growth Detected",
                        string.Format("Kernel Nonpaged Pool has grown by +{0:F1} MB (from {1:F1} MB baseline to {2:F1} MB). Recommend observing driver behavior or using poolmon.exe. (Diagnostics only - no driver changes made).",
                            netGrowthMB, BaselineMB, m.NonpagedPoolMB),
                        "Kernel / Drivers"
                    ));
                }
            }
        }
    }
}
