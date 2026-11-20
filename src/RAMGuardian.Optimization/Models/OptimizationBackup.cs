using System;

namespace RAMGuardian.Optimization.Models
{
    public class OptimizationBackup
    {
        public string BackupId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string ActionId { get; set; }
        public string Reason { get; set; }
        public StartupEntry OriginalEntry { get; set; }
        public bool IsRestored { get; set; }
        public DateTime? RestoredUtc { get; set; }

        public OptimizationBackup()
        {
            BackupId = string.Empty;
            CreatedUtc = DateTime.UtcNow;
            ActionId = string.Empty;
            Reason = string.Empty;
            OriginalEntry = new StartupEntry();
            IsRestored = false;
        }
    }
}
