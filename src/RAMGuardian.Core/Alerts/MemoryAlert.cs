using System;

namespace RAMGuardian.Core.Alerts
{
    public enum AlertSeverity
    {
        Info,
        Warning,
        High,
        Critical
    }

    public class MemoryAlert
    {
        public DateTime TimestampUtc { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string TargetComponent { get; set; }

        public MemoryAlert()
        {
            TimestampUtc = DateTime.UtcNow;
            Severity = AlertSeverity.Info;
            Category = "General";
            Title = string.Empty;
            Message = string.Empty;
            TargetComponent = string.Empty;
        }

        public MemoryAlert(AlertSeverity severity, string category, string title, string message, string target = "")
        {
            TimestampUtc = DateTime.UtcNow;
            Severity = severity;
            Category = category;
            Title = title;
            Message = message;
            TargetComponent = target ?? string.Empty;
        }
    }
}
