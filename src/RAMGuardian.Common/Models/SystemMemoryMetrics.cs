using System;

namespace RAMGuardian.Common.Models
{
    public class SystemMemoryMetrics
    {
        public DateTime TimestampUtc { get; set; }

        // Physical Memory
        public long TotalPhysicalBytes { get; set; }
        public long AvailablePhysicalBytes { get; set; }
        public long UsedPhysicalBytes { get; set; }
        public double MemoryLoadPercentage { get; set; }

        // Commit Memory (Virtual Memory backing)
        public long CommittedBytes { get; set; }
        public long CommitLimitBytes { get; set; }
        public long CommitPeakBytes { get; set; }
        public double CommitLoadPercentage { get; set; }

        // Kernel Pools & System Cache
        public long NonpagedPoolBytes { get; set; }
        public long PagedPoolBytes { get; set; }
        public long SystemCacheBytes { get; set; }

        // Paging Activity & Pagefile
        public double PagesPerSec { get; set; }
        public double PageFaultsPerSec { get; set; }
        public double PagefileUsagePercentage { get; set; }
        public double PagefilePeakUsagePercentage { get; set; }
        public long PagefileTotalBytesEstimate { get; set; }
        public long PagefileUsedBytesEstimate { get; set; }

        // Memory Compression
        public long MemoryCompressionWorkingSetBytes { get; set; }
        public long MemoryCompressionPrivateBytes { get; set; }
        public bool IsCompressionActive { get; set; }

        // System Activity
        public double CpuUtilizationPercentage { get; set; }
        public double DiskBytesPerSec { get; set; }
        public int ProcessCount { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }

        // Helper calculations for human display in MB and GB
        public double TotalPhysicalMB
        {
            get { return TotalPhysicalBytes / (1024.0 * 1024.0); }
        }

        public double AvailablePhysicalMB
        {
            get { return AvailablePhysicalBytes / (1024.0 * 1024.0); }
        }

        public double UsedPhysicalMB
        {
            get { return UsedPhysicalBytes / (1024.0 * 1024.0); }
        }

        public double CommittedMB
        {
            get { return CommittedBytes / (1024.0 * 1024.0); }
        }

        public double CommitLimitMB
        {
            get { return CommitLimitBytes / (1024.0 * 1024.0); }
        }

        public double NonpagedPoolMB
        {
            get { return NonpagedPoolBytes / (1024.0 * 1024.0); }
        }

        public double PagedPoolMB
        {
            get { return PagedPoolBytes / (1024.0 * 1024.0); }
        }

        public double SystemCacheMB
        {
            get { return SystemCacheBytes / (1024.0 * 1024.0); }
        }

        public double MemoryCompressionWorkingSetMB
        {
            get { return MemoryCompressionWorkingSetBytes / (1024.0 * 1024.0); }
        }

        public SystemMemoryMetrics()
        {
            TimestampUtc = DateTime.UtcNow;
        }
    }
}
