using System;
using System.IO;
using System.Text;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Serialization;

namespace RAMGuardian.Logging
{
    public class SnapshotWriter
    {
        private readonly string _targetFilePath;
        private readonly string _tempFilePath;

        public SnapshotWriter(string targetFilePath = @"C:\ProgramData\RAM Guardian\latest.json")
        {
            _targetFilePath = targetFilePath;
            _tempFilePath = targetFilePath + ".tmp";

            EnsureDirectory();
        }

        private void EnsureDirectory()
        {
            try
            {
                string dir = Path.GetDirectoryName(_targetFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch { }
        }

        public bool WriteSnapshot(StateSnapshot snapshot)
        {
            if (snapshot == null) return false;

            try
            {
                EnsureDirectory();
                string json = JsonHelper.Serialize(snapshot, true);

                // Write to temp file first
                File.WriteAllText(_tempFilePath, json, Encoding.UTF8);

                // Atomic move/replace
                if (File.Exists(_targetFilePath))
                {
                    File.Delete(_targetFilePath);
                }
                File.Move(_tempFilePath, _targetFilePath);
                return true;
            }
            catch
            {
                // Clean up temp file on failure
                try
                {
                    if (File.Exists(_tempFilePath))
                    {
                        File.Delete(_tempFilePath);
                    }
                }
                catch { }
                return false;
            }
        }
    }
}
