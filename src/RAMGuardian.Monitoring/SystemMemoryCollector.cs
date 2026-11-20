using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Native;

namespace RAMGuardian.Monitoring
{
    public class SystemMemoryCollector : IDisposable
    {
        private readonly PerformanceCounterCollector _perfCounters;
        private bool _disposed;

        public SystemMemoryCollector()
        {
            _perfCounters = new PerformanceCounterCollector();
        }

        public SystemMemoryMetrics CollectMetrics()
        {
            var metrics = new SystemMemoryMetrics();

            // 1. Native GlobalMemoryStatusEx
            var memStatus = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(memStatus))
            {
                metrics.TotalPhysicalBytes = (long)memStatus.ullTotalPhys;
                metrics.AvailablePhysicalBytes = (long)memStatus.ullAvailPhys;
                metrics.UsedPhysicalBytes = metrics.TotalPhysicalBytes - metrics.AvailablePhysicalBytes;
                metrics.MemoryLoadPercentage = memStatus.dwMemoryLoad;

                metrics.CommitLimitBytes = (long)memStatus.ullTotalPageFile;
                long availCommit = (long)memStatus.ullAvailPageFile;
                metrics.CommittedBytes = metrics.CommitLimitBytes - availCommit;
                if (metrics.CommitLimitBytes > 0)
                {
                    metrics.CommitLoadPercentage = Math.Round((double)metrics.CommittedBytes / metrics.CommitLimitBytes * 100.0, 2);
                }
            }

            // 2. Native GetPerformanceInfo
            NativeMethods.PERFORMANCE_INFORMATION perfInfo;
            int cb = Marshal.SizeOf(typeof(NativeMethods.PERFORMANCE_INFORMATION));
            if (NativeMethods.GetPerformanceInfo(out perfInfo, cb))
            {
                long pageSize = perfInfo.PageSize.ToInt64();
                if (pageSize > 0)
                {
                    metrics.NonpagedPoolBytes = perfInfo.KernelNonpaged.ToInt64() * pageSize;
                    metrics.PagedPoolBytes = perfInfo.KernelPaged.ToInt64() * pageSize;
                    metrics.SystemCacheBytes = perfInfo.SystemCache.ToInt64() * pageSize;
                    metrics.CommitPeakBytes = perfInfo.CommitPeak.ToInt64() * pageSize;

                    // Fallback or cross-validation for commit
                    if (metrics.CommitLimitBytes == 0)
                    {
                        metrics.CommitLimitBytes = perfInfo.CommitLimit.ToInt64() * pageSize;
                        metrics.CommittedBytes = perfInfo.CommitTotal.ToInt64() * pageSize;
                    }
                }

                metrics.ProcessCount = perfInfo.ProcessCount;
                metrics.ThreadCount = perfInfo.ThreadCount;
                metrics.HandleCount = perfInfo.HandleCount;
            }

            // 3. Performance Counters (Paging, Pagefile, CPU, Disk)
            try
            {
                metrics.PagesPerSec = Math.Round(_perfCounters.GetPagesPerSec(), 1);
                metrics.PageFaultsPerSec = Math.Round(_perfCounters.GetPageFaultsPerSec(), 1);
                metrics.PagefileUsagePercentage = Math.Round(_perfCounters.GetPagefileUsagePercentage(), 2);
                metrics.PagefilePeakUsagePercentage = Math.Round(_perfCounters.GetPagefilePeakUsagePercentage(), 2);
                metrics.CpuUtilizationPercentage = Math.Round(_perfCounters.GetCpuUtilization(), 1);
                metrics.DiskBytesPerSec = Math.Round(_perfCounters.GetDiskBytesPerSec(), 0);

                // Estimate Pagefile size in bytes from Commit Limit minus Physical RAM
                long pagefileEstimatedTotal = Math.Max(0, metrics.CommitLimitBytes - metrics.TotalPhysicalBytes);
                metrics.PagefileTotalBytesEstimate = pagefileEstimatedTotal;
                if (metrics.PagefileUsagePercentage > 0 && pagefileEstimatedTotal > 0)
                {
                    metrics.PagefileUsedBytesEstimate = (long)(pagefileEstimatedTotal * (metrics.PagefileUsagePercentage / 100.0));
                }
            }
            catch
            {
                // Nonfatal: performance counters fallbacks
            }

            // 4. Memory Compression Process
            InspectMemoryCompression(metrics);

            return metrics;
        }

        private static void InspectMemoryCompression(SystemMemoryMetrics metrics)
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("Memory Compression");
                if (procs != null && procs.Length > 0)
                {
                    using (var p = procs[0])
                    {
                        metrics.IsCompressionActive = true;
                        metrics.MemoryCompressionWorkingSetBytes = p.WorkingSet64;
                        metrics.MemoryCompressionPrivateBytes = p.PrivateMemorySize64;
                    }
                }
                else
                {
                    metrics.IsCompressionActive = false;
                }
            }
            catch
            {
                metrics.IsCompressionActive = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_perfCounters != null)
            {
                _perfCounters.Dispose();
            }
        }
    }
}
