using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Configuration;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Optimization;

namespace RAMGuardian.Dashboard
{
    public class MainForm : Form
    {
        private readonly ConfigManager _configManager;
        private readonly BackupManager _backupManager;
        private Timer _refreshTimer;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _allowExit = false;

        // UI Controls
        private Label _lblPressureBadge;
        private Label _lblPhysicalDetails;
        private Label _lblAvailableDetails;
        private Label _lblCompressionDetails;
        private Label _lblCommitDetails;
        private Label _lblPagefileDetails;
        private Label _lblPagingRates;
        private Label _lblPoolDetails;
        private Label _lblSystemDetails;
        private Label _lblFooterStatus;

        private MemoryMeterControl _ramMeter;
        private HistoryGraphControl _historyGraph;
        private ListView _lvProcesses;
        private ListBox _lbAlerts;
        private ComboBox _cbMode;

        private DarkButton _btnStartupManager;
        private DarkButton _btnRestoreChanges;
        private DarkButton _btnRefresh;

        public MainForm()
        {
            _configManager = new ConfigManager();
            _backupManager = new BackupManager();

            InitializeComponent();
            SetupTrayIcon();
            LoadLatestSnapshot();

            _refreshTimer = new Timer { Interval = 4000 };
            _refreshTimer.Tick += (s, e) => LoadLatestSnapshot();
            _refreshTimer.Start();
        }

        private void InitializeComponent()
        {
            Text = "RAM Guardian — Intelligent Resource Monitor";
            Size = new Size(1060, 720);
            MinimumSize = new Size(980, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(16, 18, 24);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            // Load Custom Icon if exists
            string iconPath = @"C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\assets\icons\ramguardian.ico";
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            // Top Header Panel
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(22, 25, 34),
                Padding = new Padding(16, 10, 16, 10)
            };

            var lblLogo = new Label
            {
                Text = "RAM GUARDIAN",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                AutoSize = true,
                Location = new Point(16, 12)
            };

            var lblSubtitle = new Label
            {
                Text = "Intelligent Resource Monitor — ASUS VivoBook X515EA (4 GB DDR4 Soldered)",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(145, 150, 170),
                AutoSize = true,
                Location = new Point(18, 38)
            };

            _lblPressureBadge = new Label
            {
                Text = "NORMAL",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 220, 140),
                BackColor = Color.FromArgb(30, 45, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(110, 28),
                Location = new Point(560, 18)
            };

            var lblModePrompt = new Label
            {
                Text = "Mode:",
                ForeColor = Color.FromArgb(160, 165, 185),
                AutoSize = true,
                Location = new Point(690, 23)
            };

            _cbMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 34, 46),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Width = 140,
                Location = new Point(735, 19)
            };
            _cbMode.Items.AddRange(new object[] { "MonitorOnly", "SafeAutomatic", "Paused" });
            _cbMode.SelectedIndex = 0;
            _cbMode.SelectedIndexChanged += CbMode_SelectedIndexChanged;

            _btnStartupManager = new DarkButton
            {
                Text = "Startup Review",
                Width = 110,
                Height = 29,
                Location = new Point(890, 18)
            };
            _btnStartupManager.Click += (s, e) => new StartupManagerForm().ShowDialog(this);

            pnlHeader.Controls.Add(lblLogo);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(_lblPressureBadge);
            pnlHeader.Controls.Add(lblModePrompt);
            pnlHeader.Controls.Add(_cbMode);
            pnlHeader.Controls.Add(_btnStartupManager);

            // Bottom Status Strip
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(20, 22, 30),
                Padding = new Padding(16, 8, 16, 8)
            };

            _lblFooterStatus = new Label
            {
                Text = "Initializing monitor...",
                ForeColor = Color.FromArgb(150, 155, 175),
                AutoSize = true,
                Location = new Point(14, 9)
            };

            _btnRestoreChanges = new DarkButton
            {
                Text = "Restore Managed Changes",
                Width = 180,
                Height = 24,
                Location = new Point(710, 6)
            };
            _btnRestoreChanges.Click += BtnRestoreChanges_Click;

            _btnRefresh = new DarkButton
            {
                Text = "Refresh",
                Width = 80,
                Height = 24,
                Location = new Point(900, 6)
            };
            _btnRefresh.Click += (s, e) => LoadLatestSnapshot();

            pnlFooter.Controls.Add(_lblFooterStatus);
            pnlFooter.Controls.Add(_btnRestoreChanges);
            pnlFooter.Controls.Add(_btnRefresh);

            // Main Content Area: Split left and right columns
            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(16, 18, 24)
            };
            pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));

            // Left Column (Gauges and Details)
            var pnlLeft = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            // Card 1: Physical Memory
            var cardRam = new DarkCardPanel { Height = 210, Dock = DockStyle.Top };
            var lblRamTitle = new Label
            {
                Text = "PHYSICAL MEMORY (3.7 GB RAM)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                AutoSize = true,
                Location = new Point(12, 10)
            };
            _ramMeter = new MemoryMeterControl { Location = new Point(14, 40), Size = new Size(140, 140) };
            _lblPhysicalDetails = new Label
            {
                Text = "Used: ...\nTotal: 3,790 MB",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.White,
                Location = new Point(170, 50),
                AutoSize = true
            };
            _lblAvailableDetails = new Label
            {
                Text = "Available: ...",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 220, 140),
                Location = new Point(170, 95),
                AutoSize = true
            };
            _lblCompressionDetails = new Label
            {
                Text = "Compression: ...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 165, 185),
                Location = new Point(170, 135),
                AutoSize = true
            };
            cardRam.Controls.Add(lblRamTitle);
            cardRam.Controls.Add(_ramMeter);
            cardRam.Controls.Add(_lblPhysicalDetails);
            cardRam.Controls.Add(_lblAvailableDetails);
            cardRam.Controls.Add(_lblCompressionDetails);

            // Card 2: Virtual Memory & Paging
            var cardVirtual = new DarkCardPanel { Height = 170, Dock = DockStyle.Top };
            var lblVirtTitle = new Label
            {
                Text = "VIRTUAL MEMORY & PAGEFILE",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                AutoSize = true,
                Location = new Point(12, 10)
            };
            _lblCommitDetails = new Label
            {
                Text = "Committed: ...",
                Font = new Font("Segoe UI", 9f),
                Location = new Point(14, 40),
                AutoSize = true
            };
            _lblPagefileDetails = new Label
            {
                Text = "Pagefile Allocated: ...",
                Font = new Font("Segoe UI", 9f),
                Location = new Point(14, 75),
                AutoSize = true
            };
            _lblPagingRates = new Label
            {
                Text = "Paging Activity: ...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 165, 185),
                Location = new Point(14, 110),
                AutoSize = true
            };
            cardVirtual.Controls.Add(lblVirtTitle);
            cardVirtual.Controls.Add(_lblCommitDetails);
            cardVirtual.Controls.Add(_lblPagefileDetails);
            cardVirtual.Controls.Add(_lblPagingRates);

            // Card 3: Kernel Stability & Pools
            var cardPools = new DarkCardPanel { Height = 170, Dock = DockStyle.Top };
            var lblPoolTitle = new Label
            {
                Text = "KERNEL POOLS & STABILITY",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                AutoSize = true,
                Location = new Point(12, 10)
            };
            _lblPoolDetails = new Label
            {
                Text = "Nonpaged Pool: ...",
                Font = new Font("Segoe UI", 9f),
                Location = new Point(14, 40),
                AutoSize = true
            };
            _lblSystemDetails = new Label
            {
                Text = "CPU & System Objects: ...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 165, 185),
                Location = new Point(14, 90),
                AutoSize = true
            };
            cardPools.Controls.Add(lblPoolTitle);
            cardPools.Controls.Add(_lblPoolDetails);
            cardPools.Controls.Add(_lblSystemDetails);

            pnlLeft.Controls.Add(cardPools);
            pnlLeft.Controls.Add(cardVirtual);
            pnlLeft.Controls.Add(cardRam);

            // Right Column (Trends, Top Processes, Alerts)
            var pnlRight = new Panel { Dock = DockStyle.Fill };

            // Trend Graph Card
            var cardGraph = new DarkCardPanel { Height = 150, Dock = DockStyle.Top };
            var lblGraphTitle = new Label
            {
                Text = "MEMORY TREND HISTORY",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                AutoSize = true,
                Location = new Point(12, 8)
            };
            _historyGraph = new HistoryGraphControl { Location = new Point(12, 30), Size = new Size(540, 105), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            cardGraph.Controls.Add(lblGraphTitle);
            cardGraph.Controls.Add(_historyGraph);

            // Process List Card
            var cardProcesses = new DarkCardPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            var lblProcTitle = new Label
            {
                Text = "TOP 15 MEMORY CONSUMERS (Physical Working Set & Committed Private)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                Dock = DockStyle.Top,
                Height = 24
            };

            _lvProcesses = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridLines = true,
                Font = new Font("Segoe UI", 8.5f)
            };
            _lvProcesses.Columns.Add("PID", 55);
            _lvProcesses.Columns.Add("Process Name", 145);
            _lvProcesses.Columns.Add("RAM (Working Set)", 110);
            _lvProcesses.Columns.Add("Private Bytes", 100);
            _lvProcesses.Columns.Add("Safety Status", 110);

            cardProcesses.Controls.Add(_lvProcesses);
            cardProcesses.Controls.Add(lblProcTitle);

            // Alerts Card
            var cardAlerts = new DarkCardPanel { Height = 100, Dock = DockStyle.Bottom };
            var lblAlertTitle = new Label
            {
                Text = "DIAGNOSTIC INSIGHTS & DETECTIONS",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 162, 97),
                AutoSize = true,
                Location = new Point(12, 8)
            };
            _lbAlerts = new ListBox
            {
                Location = new Point(12, 28),
                Size = new Size(540, 62),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.FromArgb(220, 225, 240),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f)
            };
            cardAlerts.Controls.Add(lblAlertTitle);
            cardAlerts.Controls.Add(_lbAlerts);

            pnlRight.Controls.Add(cardProcesses);
            pnlRight.Controls.Add(cardAlerts);
            pnlRight.Controls.Add(cardGraph);

            pnlMain.Controls.Add(pnlLeft, 0, 0);
            pnlMain.Controls.Add(pnlRight, 1, 0);

            Controls.Add(pnlMain);
            Controls.Add(pnlHeader);
            Controls.Add(pnlFooter);
        }

        private void SetupTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open RAM Guardian", null, (s, e) => ShowDashboard());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Operating Mode: Monitor Only", null, (s, e) => ToggleModeFromTray());
            _trayMenu.Items.Add("Restore Managed Changes", null, (s, e) => BtnRestoreChanges_Click(s, e));
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Exit Dashboard", null, (s, e) => ExitApplication());

            _trayIcon = new NotifyIcon
            {
                Text = "RAM Guardian — Active",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            string iconPath = @"C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\assets\icons\ramguardian.ico";
            if (File.Exists(iconPath))
            {
                try { _trayIcon.Icon = new Icon(iconPath); } catch { }
            }

            _trayIcon.DoubleClick += (s, e) => ShowDashboard();
        }

        private void LoadLatestSnapshot()
        {
            try
            {
                string jsonPath = @"C:\ProgramData\RAM Guardian\latest.json";
                if (!File.Exists(jsonPath))
                {
                    _lblFooterStatus.Text = "Waiting for background monitor cycle...";
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                var snap = JsonHelper.Deserialize<StateSnapshot>(json);
                if (snap == null || snap.Metrics == null) return;

                UpdateUIWithSnapshot(snap);
            }
            catch (Exception ex)
            {
                _lblFooterStatus.Text = "Status update error: " + ex.Message;
            }
        }

        private void UpdateUIWithSnapshot(StateSnapshot snap)
        {
            SystemMemoryMetrics m = snap.Metrics;

            // Pressure Badge
            _lblPressureBadge.Text = snap.PressureLevel.ToUpperInvariant();
            switch (snap.PressureLevel)
            {
                case "Critical":
                    _lblPressureBadge.ForeColor = Color.White;
                    _lblPressureBadge.BackColor = Color.FromArgb(230, 57, 70);
                    break;
                case "High":
                    _lblPressureBadge.ForeColor = Color.White;
                    _lblPressureBadge.BackColor = Color.FromArgb(215, 110, 80);
                    break;
                case "Elevated":
                    _lblPressureBadge.ForeColor = Color.Black;
                    _lblPressureBadge.BackColor = Color.FromArgb(244, 162, 97);
                    break;
                default:
                    _lblPressureBadge.ForeColor = Color.FromArgb(100, 220, 140);
                    _lblPressureBadge.BackColor = Color.FromArgb(25, 45, 35);
                    break;
            }

            // Meter & Physical Card
            _ramMeter.Percentage = m.MemoryLoadPercentage;
            _ramMeter.CenterTitle = string.Format("{0:F0} MB", m.UsedPhysicalMB);
            _ramMeter.CenterSubtitle = string.Format("{0:F1}% Used", m.MemoryLoadPercentage);
            _ramMeter.Invalidate();

            _lblPhysicalDetails.Text = string.Format("Used RAM:     {0:F1} MB\nTotal Visible: {1:F1} MB",
                m.UsedPhysicalMB, m.TotalPhysicalMB);

            _lblAvailableDetails.Text = string.Format("Available: {0:F1} MB", m.AvailablePhysicalMB);

            _lblCompressionDetails.Text = m.IsCompressionActive
                ? string.Format("Memory Compression: Active ({0:F1} MB in RAM)", m.MemoryCompressionWorkingSetMB)
                : "Memory Compression: Inactive";

            // Virtual & Pagefile Card
            _lblCommitDetails.Text = string.Format("Committed: {0:F1} MB / {1:F1} MB ({2:F1}%)",
                m.CommittedMB, m.CommitLimitMB, m.CommitLoadPercentage);

            _lblPagefileDetails.Text = string.Format("Pagefile:  {0:F1}% utilized (Peak: {1:F1}%)",
                m.PagefileUsagePercentage, m.PagefilePeakUsagePercentage);

            _lblPagingRates.Text = string.Format("Paging:    {0:F1} pages/sec | Faults: {1:F1}/sec",
                m.PagesPerSec, m.PageFaultsPerSec);

            // Pools Card
            _lblPoolDetails.Text = string.Format("Nonpaged Pool: {0:F1} MB (Base: {1:F1} MB)\nPaged Pool:    {2:F1} MB | Cache: {3:F1} MB",
                m.NonpagedPoolMB, snap.NonpagedPoolBaselineMB, m.PagedPoolMB, m.SystemCacheMB);

            _lblSystemDetails.Text = string.Format("CPU: {0:F1}% | Processes: {1} | Threads: {2} | Handles: {3}",
                m.CpuUtilizationPercentage, m.ProcessCount, m.ThreadCount, m.HandleCount);

            // History Graph
            _historyGraph.AddSample(m.AvailablePhysicalMB, m.CommitLoadPercentage);

            // Top Processes
            _lvProcesses.BeginUpdate();
            _lvProcesses.Items.Clear();
            if (snap.TopProcesses != null)
            {
                int limit = Math.Min(15, snap.TopProcesses.Count);
                for (int i = 0; i < limit; i++)
                {
                    var p = snap.TopProcesses[i];
                    var item = new ListViewItem(p.ProcessId.ToString());
                    item.SubItems.Add(p.ProcessName);
                    item.SubItems.Add(string.Format("{0:F1} MB", p.WorkingSetMB));
                    item.SubItems.Add(string.Format("{0:F1} MB", p.PrivateMB));
                    item.SubItems.Add(p.IsProtected ? "Protected (Safe)" : "User/Other");

                    if (p.IsProtected)
                    {
                        item.ForeColor = Color.FromArgb(120, 200, 150);
                    }
                    _lvProcesses.Items.Add(item);
                }
            }
            _lvProcesses.EndUpdate();

            // Alerts
            _lbAlerts.Items.Clear();
            if (snap.ActiveAlerts != null && snap.ActiveAlerts.Count > 0)
            {
                for (int i = 0; i < snap.ActiveAlerts.Count; i++)
                {
                    _lbAlerts.Items.Add(snap.ActiveAlerts[i]);
                }
            }
            else
            {
                _lbAlerts.Items.Add("System operating within normal parameters. No leak or thrashing anomalies detected.");
            }

            // Footer
            _lblFooterStatus.Text = string.Format("Last Sample: {0:HH:mm:ss} UTC | Mode: {1} | Service Uptime: {2:F0}s",
                snap.TimestampUtc, snap.OperatingMode, snap.ServiceUptimeSeconds);

            // Tray Tooltip
            _trayIcon.Text = string.Format("RAM Guardian [{0}]\nRAM: {1:F1}% | Avail: {2:F0} MB",
                snap.PressureLevel, m.MemoryLoadPercentage, m.AvailablePhysicalMB);
        }

        private void CbMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            string newMode = _cbMode.SelectedItem.ToString();
            var cfg = _configManager.LoadConfig();
            if (cfg.OperatingMode != newMode)
            {
                cfg.OperatingMode = newMode;
                _configManager.SaveConfig(cfg);
            }
        }

        private void ToggleModeFromTray()
        {
            var cfg = _configManager.LoadConfig();
            string nextMode = cfg.OperatingMode == "MonitorOnly" ? "SafeAutomatic" : "MonitorOnly";
            cfg.OperatingMode = nextMode;
            _configManager.SaveConfig(cfg);
            MessageBox.Show(string.Format("Operating mode switched to: {0}", nextMode), "RAM Guardian Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnRestoreChanges_Click(object sender, EventArgs e)
        {
            List<string> errors;
            int restored = _backupManager.RestoreAll(out errors);

            if (restored > 0)
            {
                MessageBox.Show(string.Format("Successfully restored {0} RAM Guardian managed change(s) back to original state.", restored),
                    "Changes Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No active changes needed restoration. System is in original configuration.",
                    "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowDashboard()
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
        }

        private void ExitApplication()
        {
            _allowExit = true;
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.ShowBalloonTip(2000, "RAM Guardian", "Minimized to tray. Background monitoring remains active.", ToolTipIcon.Info);
            }
            base.OnFormClosing(e);
        }
    }
}
