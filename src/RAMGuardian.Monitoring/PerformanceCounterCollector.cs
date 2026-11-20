using System;
using System.Diagnostics;

namespace RAMGuardian.Monitoring
{
    public class PerformanceCounterCollector : IDisposable
    {
        private PerformanceCounter _pagesPerSecCounter;
        private PerformanceCounter _pageFaultsPerSecCounter;
        private PerformanceCounter _pagefileUsageCounter;
        private PerformanceCounter _pagefilePeakCounter;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _diskBytesCounter;

        private bool _isInitialized;
        private bool _disposed;

        public bool IsInitialized
        {
            get { return _isInitialized; }
        }

        public PerformanceCounterCollector()
        {
            InitializeCounters();
        }

        private void InitializeCounters()
        {
            try
            {
                _pagesPerSecCounter = new PerformanceCounter("Memory", "Pages/sec", true);
                _pageFaultsPerSecCounter = new PerformanceCounter("Memory", "Page Faults/sec", true);
                _pagefileUsageCounter = new PerformanceCounter("Paging File", "% Usage", "_Total", true);
                _pagefilePeakCounter = new PerformanceCounter("Paging File", "% Usage Peak", "_Total", true);
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _diskBytesCounter = new PerformanceCounter("LogicalDisk", "Disk Bytes/sec", "_Total", true);

                // Prime initial counter values
                SafeNextValue(_pagesPerSecCounter);
                SafeNextValue(_pageFaultsPerSecCounter);
                SafeNextValue(_pagefileUsageCounter);
                SafeNextValue(_pagefilePeakCounter);
                SafeNextValue(_cpuCounter);
                SafeNextValue(_diskBytesCounter);

                _isInitialized = true;
            }
            catch (Exception)
            {
                // Fallback gracefully if performance counter subsystem is restricted or corrupted
                _isInitialized = false;
            }
        }

        public double GetPagesPerSec()
        {
            return SafeNextValue(_pagesPerSecCounter);
        }

        public double GetPageFaultsPerSec()
        {
            return SafeNextValue(_pageFaultsPerSecCounter);
        }

        public double GetPagefileUsagePercentage()
        {
            return SafeNextValue(_pagefileUsageCounter);
        }

        public double GetPagefilePeakUsagePercentage()
        {
            return SafeNextValue(_pagefilePeakCounter);
        }

        public double GetCpuUtilization()
        {
            return SafeNextValue(_cpuCounter);
        }

        public double GetDiskBytesPerSec()
        {
            return SafeNextValue(_diskBytesCounter);
        }

        private static double SafeNextValue(PerformanceCounter counter)
        {
            if (counter == null) return 0.0;
            try
            {
                return counter.NextValue();
            }
            catch
            {
                return 0.0;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SafeDispose(ref _pagesPerSecCounter);
            SafeDispose(ref _pageFaultsPerSecCounter);
            SafeDispose(ref _pagefileUsageCounter);
            SafeDispose(ref _pagefilePeakCounter);
            SafeDispose(ref _cpuCounter);
            SafeDispose(ref _diskBytesCounter);
        }

        private static void SafeDispose(ref PerformanceCounter counter)
        {
            if (counter != null)
            {
                try { counter.Dispose(); } catch { }
                counter = null;
            }
        }
    }
}
