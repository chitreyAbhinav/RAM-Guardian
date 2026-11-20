using System;

namespace RAMGuardian.Common.Models
{
    public class ProcessMemoryInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public long WorkingSetBytes { get; set; }
        public long PrivateBytes { get; set; }
        public string ExecutablePath { get; set; }
        public string MainWindowTitle { get; set; }
        public bool HasWindow { get; set; }
        public DateTime? StartTime { get; set; }
        public bool IsProtected { get; set; }
        public string ProtectionReason { get; set; }

        public double WorkingSetMB
        {
            get { return WorkingSetBytes / (1024.0 * 1024.0); }
        }

        public double PrivateMB
        {
            get { return PrivateBytes / (1024.0 * 1024.0); }
        }

        public ProcessMemoryInfo()
        {
            ProcessName = string.Empty;
            ExecutablePath = string.Empty;
            MainWindowTitle = string.Empty;
            ProtectionReason = string.Empty;
        }
    }
}
