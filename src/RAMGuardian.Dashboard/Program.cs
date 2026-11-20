using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RAMGuardian.Dashboard
{
    static class Program
    {
        private const string MutexName = @"Local\RAMGuardian_Dashboard_Mutex";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            };

            Application.ThreadException += (s, e) =>
            {
                LogCrash("Application.ThreadException", e.Exception);
            };

            try
            {
                bool isNewInstance = true;
                Mutex mutex = null;
                try
                {
                    mutex = new Mutex(true, MutexName, out isNewInstance);
                }
                catch (AbandonedMutexException)
                {
                    isNewInstance = true;
                }
                catch
                {
                    isNewInstance = true;
                }

                if (!isNewInstance)
                {
                    IntPtr hWnd = FindWindow(null, "RAM Guardian — Intelligent Resource Monitor");
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindowAsync(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());

                if (mutex != null)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                    mutex.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogCrash("Main", ex);
            }
        }

        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                string dir = @"C:\ProgramData\RAM Guardian\logs";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "dashboard_crash.log");
                string msg = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] Crash in {1}: {2}\n{3}\n\n",
                    DateTime.Now, source, ex != null ? ex.Message : "Unknown", ex != null ? ex.StackTrace : "");
                File.AppendAllText(path, msg);
            }
            catch { }
        }
    }
}
