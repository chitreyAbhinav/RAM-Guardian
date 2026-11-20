using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RAMGuardian.Dashboard
{
    static class Program
    {
        private const string MutexName = @"Global\RAMGuardian_Dashboard_Mutex";

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
            bool isNewInstance;
            using (var mutex = new Mutex(true, MutexName, out isNewInstance))
            {
                if (!isNewInstance)
                {
                    // Existing instance running; bring to foreground
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
            }
        }
    }
}
