using System;

namespace RAMGuardian.Optimization.Models
{
    public class StartupEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string RegistryKeyPath { get; set; }
        public string FilePath { get; set; }
        public string CommandLine { get; set; }
        public string TargetExecutable { get; set; }
        public bool IsApprovedForOptimization { get; set; }
        public bool IsProtected { get; set; }
        public string ProtectionReason { get; set; }
        public string Status { get; set; }

        public StartupEntry()
        {
            Id = string.Empty;
            Name = string.Empty;
            Location = string.Empty;
            RegistryKeyPath = string.Empty;
            FilePath = string.Empty;
            CommandLine = string.Empty;
            TargetExecutable = string.Empty;
            ProtectionReason = string.Empty;
            Status = "Active";
        }
    }
}
