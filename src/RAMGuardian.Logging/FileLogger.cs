using System;
using System.IO;
using System.Text;

namespace RAMGuardian.Logging
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public class FileLogger
    {
        private static readonly object SyncLock = new object();
        private readonly string _logDirectory;
        private readonly int _retentionDays;
        private readonly long _maxFileSizeBytes;
        private DateTime _lastCleanupDate;

        public FileLogger(string logDirectory = @"C:\ProgramData\RAM Guardian\logs", int retentionDays = 14, long maxFileSizeBytes = 10485760)
        {
            _logDirectory = logDirectory;
            _retentionDays = retentionDays;
            _maxFileSizeBytes = maxFileSizeBytes;
            _lastCleanupDate = DateTime.MinValue;

            EnsureDirectory();
        }

        private void EnsureDirectory()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch { }
        }

        public void Log(LogLevel level, string eventType, string message, Exception ex = null)
        {
            try
            {
                EnsureDirectory();
                CleanupOldLogsIfNeeded();

                string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
                string logFilePath = Path.Combine(_logDirectory, string.Format("ramguardian_{0}.log", dateStr));

                var sb = new StringBuilder();
                sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff UTC}] [{1,-8}] [{2}] {3}",
                    DateTime.UtcNow,
                    level.ToString().ToUpperInvariant(),
                    eventType,
                    message);

                if (ex != null)
                {
                    sb.AppendLine();
                    sb.AppendFormat("    Exception: {0}: {1}", ex.GetType().Name, ex.Message);
                    sb.AppendLine();
                    sb.AppendFormat("    StackTrace: {0}", ex.StackTrace);
                }

                string line = sb.ToString();

                lock (SyncLock)
                {
                    // Check file size guard
                    if (File.Exists(logFilePath))
                    {
                        var fi = new FileInfo(logFilePath);
                        if (fi.Length > _maxFileSizeBytes)
                        {
                            string rolloverPath = Path.Combine(_logDirectory, string.Format("ramguardian_{0}_{1:HHmmss}.log", dateStr, DateTime.UtcNow));
                            try { File.Move(logFilePath, rolloverPath); } catch { }
                        }
                    }

                    using (var writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        writer.WriteLine(line);
                    }
                }

                // Also output to Console if in console mode
                if (Environment.UserInteractive)
                {
                    ConsoleColor oldColor = Console.ForegroundColor;
                    switch (level)
                    {
                        case LogLevel.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                        case LogLevel.Error:
                        case LogLevel.Critical: Console.ForegroundColor = ConsoleColor.Red; break;
                        case LogLevel.Info: Console.ForegroundColor = ConsoleColor.Cyan; break;
                        default: Console.ForegroundColor = ConsoleColor.Gray; break;
                    }
                    Console.WriteLine(line);
                    Console.ForegroundColor = oldColor;
                }
            }
            catch
            {
                // Never crash the application on logger failure
            }
        }

        public void Info(string eventType, string message)
        {
            Log(LogLevel.Info, eventType, message);
        }

        public void Warn(string eventType, string message)
        {
            Log(LogLevel.Warning, eventType, message);
        }

        public void Error(string eventType, string message, Exception ex = null)
        {
            Log(LogLevel.Error, eventType, message, ex);
        }

        public void Critical(string eventType, string message, Exception ex = null)
        {
            Log(LogLevel.Critical, eventType, message, ex);
        }

        private void CleanupOldLogsIfNeeded()
        {
            if ((DateTime.UtcNow - _lastCleanupDate).TotalHours < 24)
            {
                return;
            }

            _lastCleanupDate = DateTime.UtcNow;

            try
            {
                if (!Directory.Exists(_logDirectory)) return;

                string[] files = Directory.GetFiles(_logDirectory, "ramguardian_*.log");
                DateTime cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

                for (int i = 0; i < files.Length; i++)
                {
                    var fi = new FileInfo(files[i]);
                    if (fi.CreationTimeUtc < cutoff)
                    {
                        try { fi.Delete(); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
