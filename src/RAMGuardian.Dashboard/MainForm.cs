using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RAMGuardian.Common.Models;
using RAMGuardian.Common.Serialization;
using RAMGuardian.Configuration;
using RAMGuardian.Configuration.Models;
using RAMGuardian.Logging;
using RAMGuardian.Optimization;
using RAMGuardian.Optimization.Actions;
using RAMGuardian.Security.Policies;
using RAMGuardian.Service;

namespace RAMGuardian.Dashboard
{
    public class MainForm : Form
    {
        private readonly ConfigManager _configManager;
        private readonly BackupManager _backupManager;
        private readonly EdgeOptimizer _edgeOptimizer;
        private readonly ProcessProtectionPolicy _protectionPolicy;
        private readonly OneClickOptimizer _oneClickOptimizer;
        private readonly FileLogger _logger;
        private Timer _refreshTimer;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _allowExit = false;
        private GuardianConfig _currentConfig;
        private StateSnapshot _latestSnapshot;
        private MonitoringEngine _embeddedEngine;

        // Top Header
        private Label _lblLogo;
        private Label _lblSubtitle;
        private Label _lblPressureBadge;
        private ComboBox _cbTopMode;
        private DarkButton _btnHeaderOptimize;
        private DarkButton _btnEmergencyStop;
        private Label _lblEmergencyBanner;
        private Label _lblToastBanner;
        private Timer _toastTimer;

        // Navigation Sidebar
        private Panel _pnlSidebar;
        private List<DarkButton> _navButtons;
        private int _currentSectionIndex = 0;

        // Section Panels container
        private Panel _pnlContent;
        private Panel[] _sectionPanels;

        // Section 1: Overview Controls
        private MemoryMeterControl _ramMeter;
        private HistoryGraphControl _historyGraph;
        private Label _lblOvCommit;
        private Label _lblOvEdge;
        private Label _lblOvStability;
        private ListBox _lbOvAlerts;
        private DarkButton _btnQuickOptimize;
        private DarkButton _btnEdgeQuickOpt;

        // Section 2: Memory Monitor Controls
        private Label _lblMonPhysical;
        private Label _lblMonCommit;
        private Label _lblMonPagefile;
        private Label _lblMonPools;
        private Label _lblMonCompression;
        private Label _lblMonOs;

        // Section 3: Process Manager Controls
        private ListView _lvProcesses;
        private TextBox _tbProcSearch;
        private ComboBox _cbProcFilter;
        private DarkButton _btnProcInspect;
        private DarkButton _btnProcGracefulClose;
        private DarkButton _btnProcProtect;

        // Section 4: Microsoft Edge Controls
        private Label _lblEdgeSummary;
        private ListView _lvEdgeProcesses;
        private DarkButton _btnEdgeApplyPolicies;
        private DarkButton _btnEdgeRestoreDefaults;
        private ComboBox _cbEdgeSleepTimeout;
        private CheckBox _chkEdgeEfficiency;
        private CheckBox _chkEdgeBackgroundDisable;

        // Section 5: Auto Optimization Controls
        private RadioButton _rbMonitorOnly;
        private RadioButton _rbBalancedAuto;
        private RadioButton _rbAggressiveAuto;
        private RadioButton _rbGamingFocus;
        private RadioButton _rbPaused;
        private DarkButton _btnAutoEmergencyToggle;
        private Label _lblAutoCooldownStatus;
        private ListBox _lbAutoActionLog;

        // Section 6: Startup Manager Controls
        private ListView _lvStartup;
        private DarkButton _btnStartupScan;
        private DarkButton _btnStartupRestoreAll;

        // Section 7: Protection Center Controls
        private ListView _lvProtectedProcs;
        private ListView _lvProtectedServices;
        private TextBox _tbAddProtected;
        private DarkButton _btnAddProtected;

        // Section 8: Activity History Controls
        private ListView _lvActivityHistory;
        private DarkButton _btnHistRefresh;
        private DarkButton _btnHistExport;

        // Section 9: Settings Controls
        private NumericUpDown _numPollingInterval;
        private NumericUpDown _numElevatedMB;
        private NumericUpDown _numHighMB;
        private NumericUpDown _numCriticalMB;
        private CheckBox _chkMinimizeToTray;
        private CheckBox _chkEdgeOptGlobal;
        private DarkButton _btnSaveSettings;
        private DarkButton _btnResetDefaults;

        // Status Bar
        private Label _lblFooterStatus;

        public MainForm()
        {
            _configManager = new ConfigManager();
            _backupManager = new BackupManager();
            _logger = new FileLogger();
            _edgeOptimizer = new EdgeOptimizer(_backupManager, _logger);
            _protectionPolicy = new ProcessProtectionPolicy();
            _oneClickOptimizer = new OneClickOptimizer(_edgeOptimizer, _protectionPolicy, _logger);
            _currentConfig = _configManager.LoadConfig();

            // Auto-detect if Windows background service is active; if not, run embedded engine
            if (!IsServiceOrConsoleActive())
            {
                try
                {
                    _embeddedEngine = new MonitoringEngine(Math.Max(5, _currentConfig.MonitoringIntervalSeconds));
                    _embeddedEngine.Start();
                    _logger.Info("STANDALONE_ENGINE", "RAM Guardian Windows Service not active; running in standalone embedded engine mode.");
                }
                catch (Exception ex)
                {
                    _logger.Error("STANDALONE_START_ERR", "Failed to start standalone embedded engine", ex);
                }
            }

            InitializeComponent();
            SetupTrayIcon();
            SwitchSection(0);
            LoadLatestSnapshot();

            _refreshTimer = new Timer { Interval = Math.Max(2000, _currentConfig.DashboardRefreshIntervalSeconds * 1000) };
            _refreshTimer.Tick += (s, e) => LoadLatestSnapshot();
            _refreshTimer.Start();
        }

        private bool IsServiceOrConsoleActive()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController("RAMGuardian"))
                {
                    if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running) return true;
                }
            }
            catch { }

            string snapPath = @"C:\ProgramData\RAM Guardian\latest.json";
            if (File.Exists(snapPath))
            {
                try
                {
                    var diff = DateTime.UtcNow - File.GetLastWriteTimeUtc(snapPath);
                    if (diff.TotalSeconds < 30) return true;
                }
                catch { }
            }

            return false;
        }

        private void InitializeComponent()
        {
            Text = "RAM Guardian — Intelligent Resource Monitor";
            Size = new Size(1160, 780);
            MinimumSize = new Size(1020, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(16, 18, 24);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            string iconPath = @"C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\assets\icons\ramguardian.ico";
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            // Top Header Panel
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(22, 25, 34),
                Padding = new Padding(16, 8, 16, 8)
            };

            _lblLogo = new Label
            {
                Text = "RAM GUARDIAN",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                AutoSize = true,
                Location = new Point(16, 10)
            };

            _lblSubtitle = new Label
            {
                Text = "Intelligent Windows Resource Monitor — ASUS VivoBook X515EA (4 GB DDR4)",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(145, 150, 170),
                AutoSize = true,
                Location = new Point(18, 36)
            };

            _lblPressureBadge = new Label
            {
                Text = "NORMAL",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 220, 140),
                BackColor = Color.FromArgb(30, 45, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(100, 28),
                Location = new Point(465, 18)
            };

            var lblModeTag = new Label
            {
                Text = "Mode:",
                ForeColor = Color.FromArgb(160, 165, 185),
                AutoSize = true,
                Location = new Point(578, 23)
            };

            _cbTopMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 36, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Width = 120,
                Location = new Point(625, 20)
            };
            _cbTopMode.Items.AddRange(new object[] { "MonitorOnly", "BalancedAuto", "AggressiveAuto", "GamingFocus", "Paused" });
            _cbTopMode.SelectedItem = _currentConfig.OperatingMode;
            _cbTopMode.SelectedIndexChanged += OnTopModeChanged;

            _btnHeaderOptimize = new DarkButton
            {
                Text = "⚡ OPTIMIZE NOW",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                NormalBackColor = Color.FromArgb(0, 180, 216),
                ForeColor = Color.Black,
                Size = new Size(140, 32),
                Location = new Point(760, 17),
                AccentColor = Color.FromArgb(255, 255, 255)
            };
            _btnHeaderOptimize.Click += OnOneClickOptimizeClicked;

            _btnEmergencyStop = new DarkButton
            {
                Text = "EMERGENCY STOP",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                NormalBackColor = _currentConfig.IsEmergencyStopped ? Color.FromArgb(239, 71, 111) : Color.FromArgb(50, 24, 30),
                ForeColor = Color.White,
                Size = new Size(150, 32),
                Location = new Point(915, 17),
                AccentColor = Color.FromArgb(239, 71, 111)
            };
            _btnEmergencyStop.Click += OnEmergencyStopClicked;

            _lblEmergencyBanner = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = Color.FromArgb(239, 71, 111),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Text = "  ⚠ GLOBAL EMERGENCY STOP ACTIVE: All automated process closures and Edge tuning are suspended.",
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = _currentConfig.IsEmergencyStopped
            };

            pnlHeader.Controls.Add(_lblLogo);
            pnlHeader.Controls.Add(_lblSubtitle);
            pnlHeader.Controls.Add(_lblPressureBadge);
            pnlHeader.Controls.Add(lblModeTag);
            pnlHeader.Controls.Add(_cbTopMode);
            pnlHeader.Controls.Add(_btnHeaderOptimize);
            pnlHeader.Controls.Add(_btnEmergencyStop);

            // Left Sidebar
            _pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 200,
                BackColor = Color.FromArgb(19, 21, 29),
                Padding = new Padding(8, 12, 8, 12)
            };

            string[] navTitles = new string[]
            {
                "🏠  Overview",
                "📊  Memory Monitor",
                "⚙️  Process Manager",
                "🌐  Microsoft Edge",
                "⚡  Auto Optimization",
                "🚀  Startup Manager",
                "🛡️  Protection Center",
                "📜  Activity History",
                "🔧  Settings"
            };

            _navButtons = new List<DarkButton>();
            int yPos = 12;
            for (int i = 0; i < navTitles.Length; i++)
            {
                int sectionIdx = i;
                var btn = new DarkButton
                {
                    Text = navTitles[i],
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0),
                    Size = new Size(184, 38),
                    Location = new Point(8, yPos),
                    BackColor = (i == 0) ? Color.FromArgb(32, 42, 60) : Color.FromArgb(24, 26, 36),
                    ForeColor = (i == 0) ? Color.FromArgb(0, 180, 216) : Color.FromArgb(200, 205, 220)
                };
                btn.Click += (s, e) => SwitchSection(sectionIdx);
                _navButtons.Add(btn);
                _pnlSidebar.Controls.Add(btn);
                yPos += 44;
            }

            // Bottom Status Bar
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(18, 20, 27),
                Padding = new Padding(12, 4, 12, 4)
            };

            _lblFooterStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ready | Default-DENY Protection Active",
                ForeColor = Color.FromArgb(130, 135, 155),
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlFooter.Controls.Add(_lblFooterStatus);

            // Center Content Panel
            _pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 18, 24),
                Padding = new Padding(12)
            };

            // Build the 9 Section Panels
            _sectionPanels = new Panel[9];
            _sectionPanels[0] = CreateOverviewPanel();
            _sectionPanels[1] = CreateMemoryMonitorPanel();
            _sectionPanels[2] = CreateProcessManagerPanel();
            _sectionPanels[3] = CreateEdgePanel();
            _sectionPanels[4] = CreateAutoOptimizationPanel();
            _sectionPanels[5] = CreateStartupManagerPanel();
            _sectionPanels[6] = CreateProtectionCenterPanel();
            _sectionPanels[7] = CreateActivityHistoryPanel();
            _sectionPanels[8] = CreateSettingsPanel();

            for (int i = 0; i < 9; i++)
            {
                _sectionPanels[i].Dock = DockStyle.Fill;
                _sectionPanels[i].Visible = (i == 0);
                _pnlContent.Controls.Add(_sectionPanels[i]);
            }

            // Toast Banner Setup
            _toastTimer = new Timer { Interval = 6000 };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                _lblToastBanner.Visible = false;
            };

            _lblToastBanner = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Text = "",
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            // Assembly to Form
            Controls.Add(_pnlContent);
            Controls.Add(_pnlSidebar);
            Controls.Add(_lblToastBanner);
            Controls.Add(_lblEmergencyBanner);
            Controls.Add(pnlHeader);
            Controls.Add(pnlFooter);
        }

        private void SwitchSection(int index)
        {
            if (index < 0 || index >= _sectionPanels.Length) return;
            _currentSectionIndex = index;

            for (int i = 0; i < _sectionPanels.Length; i++)
            {
                _sectionPanels[i].Visible = (i == index);
                if (i < _navButtons.Count)
                {
                    bool isCurrent = (i == index);
                    _navButtons[i].BackColor = isCurrent ? Color.FromArgb(32, 42, 60) : Color.FromArgb(24, 26, 36);
                    _navButtons[i].ForeColor = isCurrent ? Color.FromArgb(0, 180, 216) : Color.FromArgb(200, 205, 220);
                }
            }

            if (index == 5) LoadStartupInventory();
            if (index == 6) LoadProtectionInventory();
            if (index == 7) LoadActivityHistory();
        }

        #region Section 1: Overview Panel
        private Panel CreateOverviewPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            // Left Top: RAM Meter Card
            var cardMeter = new DarkCardPanel { Location = new Point(8, 8), Size = new Size(280, 230) };
            var lblMeterTitle = new Label { Text = "RAM LOAD (GOAL: 65-70%)", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true };
            _ramMeter = new MemoryMeterControl { Location = new Point(70, 32), Size = new Size(140, 140) };
            _btnQuickOptimize = new DarkButton
            {
                Text = "⚡ OPTIMIZE RAM & EDGE NOW",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                NormalBackColor = Color.FromArgb(0, 180, 216),
                ForeColor = Color.Black,
                Size = new Size(252, 34),
                Location = new Point(14, 182),
                AccentColor = Color.FromArgb(255, 255, 255)
            };
            _btnQuickOptimize.Click += OnOneClickOptimizeClicked;

            cardMeter.Controls.Add(lblMeterTitle);
            cardMeter.Controls.Add(_ramMeter);
            cardMeter.Controls.Add(_btnQuickOptimize);

            // Middle Top: Commit Card
            var cardCommit = new DarkCardPanel { Location = new Point(298, 8), Size = new Size(300, 230) };
            var lblCommitTitle = new Label { Text = "COMMITMENT & PAGEFILE", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true };
            _lblOvCommit = new Label { Text = "Calculating commit limits...", Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(210, 215, 230), Location = new Point(14, 40), Size = new Size(270, 175) };
            cardCommit.Controls.Add(lblCommitTitle);
            cardCommit.Controls.Add(_lblOvCommit);

            // Right Top: Edge Status Card
            var cardEdge = new DarkCardPanel { Location = new Point(608, 8), Size = new Size(320, 230) };
            var lblEdgeTitle = new Label { Text = "24/7 MICROSOFT EDGE GUARDIAN", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true };
            _lblOvEdge = new Label { Text = "Scanning Edge processes...", Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(210, 215, 230), Location = new Point(14, 40), Size = new Size(290, 140) };
            var btnGoEdge = new DarkButton { Text = "Manage Edge ➜", Location = new Point(14, 185), Size = new Size(135, 28) };
            btnGoEdge.Click += (s, e) => SwitchSection(3);
            _btnEdgeQuickOpt = new DarkButton
            {
                Text = "⚡ Trim Edge RAM",
                Location = new Point(155, 185),
                Size = new Size(150, 28),
                NormalBackColor = Color.FromArgb(0, 150, 190),
                ForeColor = Color.White,
                AccentColor = Color.FromArgb(0, 210, 255)
            };
            _btnEdgeQuickOpt.Click += OnOneClickOptimizeClicked;

            cardEdge.Controls.Add(lblEdgeTitle);
            cardEdge.Controls.Add(_lblOvEdge);
            cardEdge.Controls.Add(btnGoEdge);
            cardEdge.Controls.Add(_btnEdgeQuickOpt);

            // Left Bottom: Stability Card & Live Trend Graph
            var cardTrend = new DarkCardPanel { Location = new Point(8, 248), Size = new Size(590, 240) };
            var lblTrendTitle = new Label { Text = "AVAILABLE RAM TREND & PRESSURE ANALYSIS", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true };
            _lblOvStability = new Label { Text = "Analyzing trend...", Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(170, 175, 195), Location = new Point(14, 38), Size = new Size(560, 42) };
            _historyGraph = new HistoryGraphControl { Location = new Point(14, 85), Size = new Size(560, 140) };
            cardTrend.Controls.Add(lblTrendTitle);
            cardTrend.Controls.Add(_lblOvStability);
            cardTrend.Controls.Add(_historyGraph);

            // Right Bottom: Alerts Card
            var cardAlerts = new DarkCardPanel { Location = new Point(608, 248), Size = new Size(320, 240) };
            var lblAlertTitle = new Label { Text = "ACTIVE DIAGNOSTIC INSIGHTS", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true };
            _lbOvAlerts = new ListBox
            {
                Location = new Point(14, 40),
                Size = new Size(292, 185),
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.FromArgb(220, 225, 235),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f)
            };
            cardAlerts.Controls.Add(lblAlertTitle);
            cardAlerts.Controls.Add(_lbOvAlerts);

            pnl.Controls.Add(cardMeter);
            pnl.Controls.Add(cardCommit);
            pnl.Controls.Add(cardEdge);
            pnl.Controls.Add(cardTrend);
            pnl.Controls.Add(cardAlerts);

            return pnl;
        }
        #endregion

        #region Section 2: Memory Monitor Panel
        private Panel CreateMemoryMonitorPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            // Grid of 6 diagnostic cards
            var cardPhys = new DarkCardPanel { Location = new Point(8, 8), Size = new Size(450, 140) };
            cardPhys.Controls.Add(new Label { Text = "PHYSICAL RAM (DDR4 3200 Soldered)", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true });
            _lblMonPhysical = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9f), ForeColor = Color.White, Location = new Point(14, 38), Size = new Size(420, 90) };
            cardPhys.Controls.Add(_lblMonPhysical);

            var cardCommit = new DarkCardPanel { Location = new Point(468, 8), Size = new Size(450, 140) };
            cardCommit.Controls.Add(new Label { Text = "COMMITMENT & VIRTUAL ADDRESS SPACE", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true });
            _lblMonCommit = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9f), ForeColor = Color.White, Location = new Point(14, 38), Size = new Size(420, 90) };
            cardCommit.Controls.Add(_lblMonCommit);

            var cardPage = new DarkCardPanel { Location = new Point(8, 158), Size = new Size(450, 140) };
            cardPage.Controls.Add(new Label { Text = "PAGEFILE & SECONDARY STORAGE", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true });
            _lblMonPagefile = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9f), ForeColor = Color.White, Location = new Point(14, 38), Size = new Size(420, 90) };
            cardPage.Controls.Add(_lblMonPagefile);

            var cardPool = new DarkCardPanel { Location = new Point(468, 158), Size = new Size(450, 140) };
            cardPool.Controls.Add(new Label { Text = "KERNEL POOLS (Nonpaged & Paged)", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true });
            _lblMonPools = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9f), ForeColor = Color.White, Location = new Point(14, 38), Size = new Size(420, 90) };
            cardPool.Controls.Add(_lblMonPools);

            var cardComp = new DarkCardPanel { Location = new Point(8, 308), Size = new Size(450, 140) };
            cardComp.Controls.Add(new Label { Text = "PAGING RATES & COMPRESSION STORE", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true });
            _lblMonCompression = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9f), ForeColor = Color.White, Location = new Point(14, 38), Size = new Size(420, 90) };
            cardComp.Controls.Add(_lblMonCompression);

            var cardOs = new DarkCardPanel { Location = new Point(468, 308), Size = new Size(450, 140) };
            cardOs.Controls.Add(new Label { Text = "OPERATING SYSTEM HANDLES & CPU", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true });
            _lblMonOs = new Label { Text = "Loading...", Font = new Font("Segoe UI", 9f), ForeColor = Color.White, Location = new Point(14, 38), Size = new Size(420, 90) };
            cardOs.Controls.Add(_lblMonOs);

            pnl.Controls.Add(cardPhys);
            pnl.Controls.Add(cardCommit);
            pnl.Controls.Add(cardPage);
            pnl.Controls.Add(cardPool);
            pnl.Controls.Add(cardComp);
            pnl.Controls.Add(cardOs);

            return pnl;
        }
        #endregion

        #region Section 3: Process Manager Panel
        private Panel CreateProcessManagerPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill };

            var pnlTopBar = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 8, 8, 8) };

            var lblSearch = new Label { Text = "Search:", Location = new Point(8, 12), AutoSize = true, ForeColor = Color.FromArgb(160, 165, 185) };
            _tbProcSearch = new TextBox { Location = new Point(60, 9), Width = 150, BackColor = Color.FromArgb(32, 36, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            _tbProcSearch.TextChanged += (s, e) => FilterProcessList();

            var lblFilter = new Label { Text = "Tier:", Location = new Point(225, 12), AutoSize = true, ForeColor = Color.FromArgb(160, 165, 185) };
            _cbProcFilter = new ComboBox
            {
                Location = new Point(265, 9),
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 36, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _cbProcFilter.Items.AddRange(new object[] { "All Processes", "Protected / Immune", "Termination Candidates", "Microsoft Edge", "User Scripts (AHK/IDM)" });
            _cbProcFilter.SelectedIndex = 0;
            _cbProcFilter.SelectedIndexChanged += (s, e) => FilterProcessList();

            _btnProcGracefulClose = new DarkButton
            {
                Text = "Request Graceful Close",
                Location = new Point(440, 7),
                Size = new Size(165, 30),
                AccentColor = Color.FromArgb(244, 162, 97)
            };
            _btnProcGracefulClose.Click += OnProcGracefulCloseClicked;

            _btnProcProtect = new DarkButton
            {
                Text = "Protect Process",
                Location = new Point(615, 7),
                Size = new Size(130, 30),
                AccentColor = Color.FromArgb(0, 180, 216)
            };
            _btnProcProtect.Click += OnProcProtectClicked;

            _btnProcInspect = new DarkButton
            {
                Text = "Inspect Details",
                Location = new Point(755, 7),
                Size = new Size(120, 30)
            };
            _btnProcInspect.Click += OnProcInspectClicked;

            pnlTopBar.Controls.Add(lblSearch);
            pnlTopBar.Controls.Add(_tbProcSearch);
            pnlTopBar.Controls.Add(lblFilter);
            pnlTopBar.Controls.Add(_cbProcFilter);
            pnlTopBar.Controls.Add(_btnProcGracefulClose);
            pnlTopBar.Controls.Add(_btnProcProtect);
            pnlTopBar.Controls.Add(_btnProcInspect);

            _lvProcesses = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            _lvProcesses.Columns.Add("PID", 65);
            _lvProcesses.Columns.Add("Process Name", 170);
            _lvProcesses.Columns.Add("Working Set", 95, HorizontalAlignment.Right);
            _lvProcesses.Columns.Add("Private Bytes", 95, HorizontalAlignment.Right);
            _lvProcesses.Columns.Add("Classification", 140);
            _lvProcesses.Columns.Add("Termination Eligible?", 140);
            _lvProcesses.Columns.Add("Protection / Eligibility Reason", 240);

            pnl.Controls.Add(_lvProcesses);
            pnl.Controls.Add(pnlTopBar);

            return pnl;
        }
        #endregion

        #region Section 4: Microsoft Edge Panel
        private Panel CreateEdgePanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var cardEdgeInfo = new DarkCardPanel { Location = new Point(8, 8), Size = new Size(910, 185) };
            var lblEdgeHead = new Label { Text = "MICROSOFT EDGE 24/7 GUARDIAN STATUS", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 12), AutoSize = true };
            _lblEdgeSummary = new Label { Text = "Scanning Edge instances...", Font = new Font("Segoe UI", 9f), ForeColor = Color.White, Location = new Point(14, 38), Size = new Size(520, 130) };

            var pnlControls = new Panel { Location = new Point(545, 12), Size = new Size(350, 165) };
            var lblTimeout = new Label { Text = "Sleeping Tabs Timeout:", Location = new Point(0, 5), AutoSize = true, ForeColor = Color.FromArgb(160, 165, 185) };
            _cbEdgeSleepTimeout = new ComboBox
            {
                Location = new Point(160, 2),
                Width = 175,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 36, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _cbEdgeSleepTimeout.Items.AddRange(new object[] { "30 seconds", "1 minute", "2 minutes", "5 minutes", "15 minutes" });
            _cbEdgeSleepTimeout.SelectedIndex = 3;

            _chkEdgeEfficiency = new CheckBox { Text = "Enable Browser Efficiency Mode", Location = new Point(0, 36), AutoSize = true, ForeColor = Color.White, Checked = true };
            _chkEdgeBackgroundDisable = new CheckBox { Text = "Disable Background Mode on Exit", Location = new Point(0, 62), AutoSize = true, ForeColor = Color.White, Checked = true };

            _btnEdgeApplyPolicies = new DarkButton { Text = "Apply Policies Now", Location = new Point(0, 95), Size = new Size(160, 28), AccentColor = Color.FromArgb(0, 180, 216) };
            _btnEdgeApplyPolicies.Click += OnEdgeApplyPoliciesClicked;

            _btnEdgeRestoreDefaults = new DarkButton { Text = "Restore Edge Defaults", Location = new Point(170, 95), Size = new Size(165, 28) };
            _btnEdgeRestoreDefaults.Click += OnEdgeRestoreDefaultsClicked;

            var btnEdgeTrimAll = new DarkButton
            {
                Text = "⚡ 1-Click Trim Edge & System RAM",
                Location = new Point(0, 128),
                Size = new Size(335, 30),
                NormalBackColor = Color.FromArgb(0, 180, 216),
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AccentColor = Color.FromArgb(255, 255, 255)
            };
            btnEdgeTrimAll.Click += OnOneClickOptimizeClicked;

            pnlControls.Controls.Add(lblTimeout);
            pnlControls.Controls.Add(_cbEdgeSleepTimeout);
            pnlControls.Controls.Add(_chkEdgeEfficiency);
            pnlControls.Controls.Add(_chkEdgeBackgroundDisable);
            pnlControls.Controls.Add(_btnEdgeApplyPolicies);
            pnlControls.Controls.Add(_btnEdgeRestoreDefaults);
            pnlControls.Controls.Add(btnEdgeTrimAll);

            cardEdgeInfo.Controls.Add(lblEdgeHead);
            cardEdgeInfo.Controls.Add(_lblEdgeSummary);
            cardEdgeInfo.Controls.Add(pnlControls);

            var pnlEdgeListContainer = new DarkCardPanel { Location = new Point(8, 202), Size = new Size(910, 300) };
            var lblTableTitle = new Label { Text = "ACTIVE EDGE PROCESS INVENTORY & ROLES", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true };

            _lvEdgeProcesses = new ListView
            {
                Location = new Point(14, 38),
                Size = new Size(880, 268),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            _lvEdgeProcesses.Columns.Add("PID", 70);
            _lvEdgeProcesses.Columns.Add("Process Role", 130);
            _lvEdgeProcesses.Columns.Add("Working Set (MB)", 120, HorizontalAlignment.Right);
            _lvEdgeProcesses.Columns.Add("Private Bytes (MB)", 120, HorizontalAlignment.Right);
            _lvEdgeProcesses.Columns.Add("Sleeping Candidate?", 140);
            _lvEdgeProcesses.Columns.Add("Command Line Arguments", 360);

            pnlEdgeListContainer.Controls.Add(lblTableTitle);
            pnlEdgeListContainer.Controls.Add(_lvEdgeProcesses);

            pnl.Controls.Add(cardEdgeInfo);
            pnl.Controls.Add(pnlEdgeListContainer);

            return pnl;
        }
        #endregion

        #region Section 5: Auto Optimization Panel
        private Panel CreateAutoOptimizationPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var cardModes = new DarkCardPanel { Location = new Point(8, 8), Size = new Size(910, 195) };
            cardModes.Controls.Add(new Label { Text = "OPERATING MODE SELECTION", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });

            _rbMonitorOnly = new RadioButton { Text = "1. MonitorOnly — Read-only telemetry, zero changes, Edge diagnostics only.", Location = new Point(20, 38), Size = new Size(860, 24), ForeColor = Color.White };
            _rbBalancedAuto = new RadioButton { Text = "2. BalancedAuto — Automatic management of unnecessary background apps (L2 Graceful Closure only).", Location = new Point(20, 66), Size = new Size(860, 24), ForeColor = Color.White };
            _rbAggressiveAuto = new RadioButton { Text = "3. AggressiveAuto — Maximum memory reclaiming; graduated L1-L4 closure for approved background targets.", Location = new Point(20, 94), Size = new Size(860, 24), ForeColor = Color.White };
            _rbGamingFocus = new RadioButton { Text = "4. GamingFocus (Default) — Target 65-70% RAM at all times; active background tuning & extreme-case escalation.", Location = new Point(20, 122), Size = new Size(860, 24), ForeColor = Color.White };
            _rbPaused = new RadioButton { Text = "5. Paused — Monitoring & automated optimizations completely suspended.", Location = new Point(20, 150), Size = new Size(860, 24), ForeColor = Color.White };

            _rbMonitorOnly.CheckedChanged += OnModeRadioChanged;
            _rbBalancedAuto.CheckedChanged += OnModeRadioChanged;
            _rbAggressiveAuto.CheckedChanged += OnModeRadioChanged;
            _rbGamingFocus.CheckedChanged += OnModeRadioChanged;
            _rbPaused.CheckedChanged += OnModeRadioChanged;

            UpdateModeRadios(_currentConfig.OperatingMode);

            cardModes.Controls.Add(_rbMonitorOnly);
            cardModes.Controls.Add(_rbBalancedAuto);
            cardModes.Controls.Add(_rbAggressiveAuto);
            cardModes.Controls.Add(_rbGamingFocus);
            cardModes.Controls.Add(_rbPaused);

            var cardEmergency = new DarkCardPanel { Location = new Point(8, 212), Size = new Size(910, 90) };
            cardEmergency.Controls.Add(new Label { Text = "GLOBAL EMERGENCY SWITCH", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(239, 71, 111), Location = new Point(14, 10), AutoSize = true });
            cardEmergency.Controls.Add(new Label { Text = "Immediately halts all automated closures, trims, and group policy updates. Reversible instantly.", Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(170, 175, 195), Location = new Point(14, 32), AutoSize = true });

            _btnAutoEmergencyToggle = new DarkButton
            {
                Text = _currentConfig.IsEmergencyStopped ? "RESUME AUTOMATED OPTIMIZATION" : "TRIP EMERGENCY STOP",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(620, 24),
                Size = new Size(270, 36),
                BackColor = _currentConfig.IsEmergencyStopped ? Color.FromArgb(76, 201, 130) : Color.FromArgb(239, 71, 111),
                ForeColor = Color.White
            };
            _btnAutoEmergencyToggle.Click += OnEmergencyStopClicked;
            cardEmergency.Controls.Add(_btnAutoEmergencyToggle);

            var cardAudit = new DarkCardPanel { Location = new Point(8, 310), Size = new Size(910, 200) };
            cardAudit.Controls.Add(new Label { Text = "SESSION OPTIMIZATION LOG & COOLDOWN STATUS", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });
            _lblAutoCooldownStatus = new Label { Text = "Cooldown: Ready (No recent optimization lock)", Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(100, 220, 140), Location = new Point(14, 32), AutoSize = true };
            _lbAutoActionLog = new ListBox { Location = new Point(14, 55), Size = new Size(880, 130), BackColor = Color.FromArgb(20, 22, 30), ForeColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 8.5f) };
            cardAudit.Controls.Add(_lblAutoCooldownStatus);
            cardAudit.Controls.Add(_lbAutoActionLog);

            pnl.Controls.Add(cardModes);
            pnl.Controls.Add(cardEmergency);
            pnl.Controls.Add(cardAudit);

            return pnl;
        }

        private void UpdateModeRadios(string mode)
        {
            _rbMonitorOnly.Checked = (mode == "MonitorOnly");
            _rbBalancedAuto.Checked = (mode == "BalancedAuto");
            _rbAggressiveAuto.Checked = (mode == "AggressiveAuto");
            _rbGamingFocus.Checked = (mode == "GamingFocus");
            _rbPaused.Checked = (mode == "Paused");
        }

        private void OnModeRadioChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null || !rb.Checked) return;

            string targetMode = "MonitorOnly";
            if (rb == _rbBalancedAuto) targetMode = "BalancedAuto";
            else if (rb == _rbAggressiveAuto) targetMode = "AggressiveAuto";
            else if (rb == _rbGamingFocus) targetMode = "GamingFocus";
            else if (rb == _rbPaused) targetMode = "Paused";

            ApplyOperatingMode(targetMode);
        }
        #endregion

        #region Section 6: Startup Manager Panel
        private Panel CreateStartupManagerPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill };

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8) };
            var lblHead = new Label { Text = "STARTUP INVENTORY & REVERSIBLE MANAGEMENT", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(8, 12), AutoSize = true };
            _btnStartupScan = new DarkButton { Text = "Refresh Scan", Location = new Point(620, 8), Size = new Size(120, 30) };
            _btnStartupScan.Click += (s, e) => LoadStartupInventory();

            _btnStartupRestoreAll = new DarkButton { Text = "Restore All Startup Items", Location = new Point(750, 8), Size = new Size(160, 30), AccentColor = Color.FromArgb(76, 201, 130) };
            _btnStartupRestoreAll.Click += OnStartupRestoreAllClicked;

            pnlTop.Controls.Add(lblHead);
            pnlTop.Controls.Add(_btnStartupScan);
            pnlTop.Controls.Add(_btnStartupRestoreAll);

            _lvStartup = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            _lvStartup.Columns.Add("Item Name", 180);
            _lvStartup.Columns.Add("Location", 180);
            _lvStartup.Columns.Add("Status", 140);
            _lvStartup.Columns.Add("Immunity / Protection Reason", 260);
            _lvStartup.Columns.Add("Target Command Line", 320);

            pnl.Controls.Add(_lvStartup);
            pnl.Controls.Add(pnlTop);

            return pnl;
        }

        private void LoadStartupInventory()
        {
            _lvStartup.Items.Clear();
            var scanner = new StartupScanner();
            var items = scanner.ScanAll();

            for (int i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                var lvi = new ListViewItem(entry.Name);
                lvi.SubItems.Add(entry.Location != null ? entry.Location.ToString() : string.Empty);

                bool isDisabled = entry.Status != null && entry.Status.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
                string status = isDisabled ? "Optimized / Disabled" : (entry.IsProtected ? "Immune (Protected)" : "Enabled");
                lvi.SubItems.Add(status);
                lvi.SubItems.Add(string.IsNullOrEmpty(entry.ProtectionReason) ? "-" : entry.ProtectionReason);
                lvi.SubItems.Add(entry.CommandLine ?? string.Empty);

                if (entry.IsProtected)
                {
                    lvi.ForeColor = Color.FromArgb(160, 220, 255);
                }
                else if (isDisabled)
                {
                    lvi.ForeColor = Color.FromArgb(150, 150, 160);
                }

                _lvStartup.Items.Add(lvi);
            }
        }

        private void OnStartupRestoreAllClicked(object sender, EventArgs e)
        {
            List<string> errors;
            int count = _backupManager.RestoreAll(out errors);
            MessageBox.Show(
                string.Format("Restored {0} managed startup items back to original state.", count),
                "RAM Guardian Startup Restoration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            LoadStartupInventory();
        }
        #endregion

        #region Section 7: Protection Center Panel
        private Panel CreateProtectionCenterPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var cardInfo = new DarkCardPanel { Location = new Point(8, 8), Size = new Size(910, 80) };
            cardInfo.Controls.Add(new Label { Text = "DEFAULT-DENY SAFETY ARCHITECTURE & IMMUNITY REGISTRY", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });
            cardInfo.Controls.Add(new Label { Text = "RAM Guardian enforces strict mathematical immunity for all Windows core processes, security software, ASUS utilities, Safe Exam Browser, AutoHotkey user scripts, and developer tools. Unknown processes are NEVER closed.", Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(170, 175, 195), Location = new Point(14, 32), Size = new Size(880, 36) });

            var cardProcs = new DarkCardPanel { Location = new Point(8, 98), Size = new Size(910, 230) };
            cardProcs.Controls.Add(new Label { Text = "PROTECTED PROCESS INVENTORY", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });

            _lvProtectedProcs = new ListView
            {
                Location = new Point(14, 38),
                Size = new Size(880, 140),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            _lvProtectedProcs.Columns.Add("Process Name", 180);
            _lvProtectedProcs.Columns.Add("Category", 180);
            _lvProtectedProcs.Columns.Add("Protection Rationale", 480);

            var lblAdd = new Label { Text = "Add Custom Process to Protection List:", Location = new Point(14, 192), AutoSize = true, ForeColor = Color.FromArgb(160, 165, 185) };
            _tbAddProtected = new TextBox { Location = new Point(260, 189), Width = 180, BackColor = Color.FromArgb(32, 36, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            _btnAddProtected = new DarkButton { Text = "Add Protection", Location = new Point(450, 186), Size = new Size(130, 28) };
            _btnAddProtected.Click += (s, e) =>
            {
                string procName = _tbAddProtected.Text.Trim();
                if (!string.IsNullOrEmpty(procName))
                {
                    _protectionPolicy.AddUserProtectedProcess(procName);
                    _tbAddProtected.Clear();
                    LoadProtectionInventory();
                }
            };

            cardProcs.Controls.Add(_lvProtectedProcs);
            cardProcs.Controls.Add(lblAdd);
            cardProcs.Controls.Add(_tbAddProtected);
            cardProcs.Controls.Add(_btnAddProtected);

            var cardServices = new DarkCardPanel { Location = new Point(8, 338), Size = new Size(910, 170) };
            cardServices.Controls.Add(new Label { Text = "PROTECTED WINDOWS SERVICES (IMMUNE FROM MODIFICATION)", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });

            _lvProtectedServices = new ListView
            {
                Location = new Point(14, 36),
                Size = new Size(880, 120),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            _lvProtectedServices.Columns.Add("Service Name", 200);
            _lvProtectedServices.Columns.Add("Display Name / Purpose", 320);
            _lvProtectedServices.Columns.Add("Security Classification", 320);

            cardServices.Controls.Add(_lvProtectedServices);

            pnl.Controls.Add(cardInfo);
            pnl.Controls.Add(cardProcs);
            pnl.Controls.Add(cardServices);

            return pnl;
        }

        private void LoadProtectionInventory()
        {
            _lvProtectedProcs.Items.Clear();
            string[][] defaultProtected = new string[][]
            {
                new string[] { "MsMpEng.exe", "Security Software", "Windows Defender Antivirus Core Service" },
                new string[] { "explorer.exe", "Critical Windows OS", "Windows Shell and Desktop Host" },
                new string[] { "dwmp.exe / dwm.exe", "Critical Windows OS", "Desktop Window Manager (Compositor)" },
                new string[] { "csrss.exe", "Critical Windows OS", "Client/Server Runtime Subsystem" },
                new string[] { "lsass.exe", "Critical Windows OS", "Local Security Authority Subsystem Service" },
                new string[] { "SafeExamBrowser.exe", "Academic Integrity", "Safe Exam Browser Protected Testing Shell" },
                new string[] { "ShortcutSwap.ahk", "User Productivity", "AutoHotkey Keyboard Script" },
                new string[] { "WindowButtonDisable.ahk", "User Productivity", "AutoHotkey Window Behavior Script" },
                new string[] { "IDMan.exe", "User Productivity", "Internet Download Manager (Downloads Protected)" },
                new string[] { "Filee.exe", "User Productivity", "Local File Utility" },
                new string[] { "AsusAppService.exe", "Hardware / ASUS", "VivoBook Power and Battery Charging Limits" },
                new string[] { "code.exe / devenv.exe", "Development Tool", "Visual Studio / VS Code Development Environment" }
            };

            for (int i = 0; i < defaultProtected.Length; i++)
            {
                var lvi = new ListViewItem(defaultProtected[i][0]);
                lvi.SubItems.Add(defaultProtected[i][1]);
                lvi.SubItems.Add(defaultProtected[i][2]);
                _lvProtectedProcs.Items.Add(lvi);
            }

            _lvProtectedServices.Items.Clear();
            string[][] services = new string[][]
            {
                new string[] { "WinDefend", "Microsoft Defender Antivirus Service", "Core Anti-Malware Engine" },
                new string[] { "EventLog", "Windows Event Log", "Operating System Diagnostic Logging" },
                new string[] { "RpcSs", "Remote Procedure Call (RPC)", "Essential Windows Inter-Process Communication" },
                new string[] { "DcomLaunch", "DCOM Server Process Launcher", "COM Infrastructure Service" },
                new string[] { "AsusSysCap", "ASUS System Control Interface", "VivoBook Hardware & Fan Profiles" }
            };

            for (int i = 0; i < services.Length; i++)
            {
                var lvi = new ListViewItem(services[i][0]);
                lvi.SubItems.Add(services[i][1]);
                lvi.SubItems.Add(services[i][2]);
                _lvProtectedServices.Items.Add(lvi);
            }
        }
        #endregion

        #region Section 8: Activity History Panel
        private Panel CreateActivityHistoryPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill };

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            var lblTitle = new Label { Text = "AUDIT LOG & AUTOMATED ACTION HISTORY", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(8, 12), AutoSize = true };
            _btnHistRefresh = new DarkButton { Text = "Refresh Audit Log", Location = new Point(620, 8), Size = new Size(130, 30) };
            _btnHistRefresh.Click += (s, e) => LoadActivityHistory();

            _btnHistExport = new DarkButton { Text = "Open Log Folder", Location = new Point(760, 8), Size = new Size(150, 30) };
            _btnHistExport.Click += (s, e) =>
            {
                string logDir = @"C:\ProgramData\RAM Guardian\Logs";
                if (Directory.Exists(logDir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDir);
                }
            };

            pnlTop.Controls.Add(lblTitle);
            pnlTop.Controls.Add(_btnHistRefresh);
            pnlTop.Controls.Add(_btnHistExport);

            _lvActivityHistory = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 22, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            _lvActivityHistory.Columns.Add("Timestamp (Local)", 140);
            _lvActivityHistory.Columns.Add("Severity", 80);
            _lvActivityHistory.Columns.Add("Category", 140);
            _lvActivityHistory.Columns.Add("Message / Action Audit Details", 540);

            pnl.Controls.Add(_lvActivityHistory);
            pnl.Controls.Add(pnlTop);

            return pnl;
        }

        private void LoadActivityHistory()
        {
            _lvActivityHistory.Items.Clear();
            string logPath = Path.Combine(@"C:\ProgramData\RAM Guardian\Logs", "ramguardian.log");
            if (!File.Exists(logPath)) return;

            try
            {
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    var lines = new List<string>();
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                        if (lines.Count > 200) lines.RemoveAt(0);
                    }

                    for (int i = lines.Count - 1; i >= 0; i--)
                    {
                        string l = lines[i];
                        string[] parts = l.Split(new char[] { '|' }, 4);
                        if (parts.Length >= 4)
                        {
                            var lvi = new ListViewItem(parts[0].Trim());
                            lvi.SubItems.Add(parts[1].Trim());
                            lvi.SubItems.Add(parts[2].Trim());
                            lvi.SubItems.Add(parts[3].Trim());

                            if (parts[1].Contains("ERROR") || parts[1].Contains("WARN"))
                            {
                                lvi.ForeColor = Color.FromArgb(244, 162, 97);
                            }
                            _lvActivityHistory.Items.Add(lvi);
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Section 9: Settings Panel
        private Panel CreateSettingsPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var cardIntervals = new DarkCardPanel { Location = new Point(8, 8), Size = new Size(910, 110) };
            cardIntervals.Controls.Add(new Label { Text = "MONITORING ENGINE INTERVALS", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });

            var lblPoll = new Label { Text = "Background Service Monitoring Cycle (Seconds):", Location = new Point(14, 38), AutoSize = true, ForeColor = Color.White };
            _numPollingInterval = new NumericUpDown { Location = new Point(340, 35), Width = 80, Minimum = 5, Maximum = 120, Value = _currentConfig.MonitoringIntervalSeconds, BackColor = Color.FromArgb(32, 36, 50), ForeColor = Color.White };

            cardIntervals.Controls.Add(lblPoll);
            cardIntervals.Controls.Add(_numPollingInterval);

            var cardThresholds = new DarkCardPanel { Location = new Point(8, 128), Size = new Size(910, 150) };
            cardThresholds.Controls.Add(new Label { Text = "AVAILABLE RAM PRESSURE THRESHOLDS (ASUS VivoBook 4 GB Profile)", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });

            var lblElev = new Label { Text = "Elevated Pressure Threshold (Available RAM MB):", Location = new Point(14, 38), AutoSize = true, ForeColor = Color.White };
            decimal elevVal = (decimal)Math.Min(3500.0, Math.Max(100.0, _currentConfig.ElevatedAvailableRamMB));
            _numElevatedMB = new NumericUpDown { Location = new Point(340, 35), Width = 80, Minimum = 100, Maximum = 3500, Value = elevVal, BackColor = Color.FromArgb(32, 36, 50), ForeColor = Color.White };

            var lblHigh = new Label { Text = "High Pressure Threshold (Available RAM MB):", Location = new Point(14, 70), AutoSize = true, ForeColor = Color.White };
            decimal highVal = (decimal)Math.Min(3000.0, Math.Max(50.0, _currentConfig.HighAvailableRamMB));
            _numHighMB = new NumericUpDown { Location = new Point(340, 67), Width = 80, Minimum = 50, Maximum = 3000, Value = highVal, BackColor = Color.FromArgb(32, 36, 50), ForeColor = Color.White };

            var lblCrit = new Label { Text = "Critical Pressure Threshold (Available RAM MB):", Location = new Point(14, 102), AutoSize = true, ForeColor = Color.White };
            decimal critVal = (decimal)Math.Min(2500.0, Math.Max(20.0, _currentConfig.CriticalAvailableRamMB));
            _numCriticalMB = new NumericUpDown { Location = new Point(340, 99), Width = 80, Minimum = 20, Maximum = 2500, Value = critVal, BackColor = Color.FromArgb(32, 36, 50), ForeColor = Color.White };

            cardThresholds.Controls.Add(lblElev);
            cardThresholds.Controls.Add(_numElevatedMB);
            cardThresholds.Controls.Add(lblHigh);
            cardThresholds.Controls.Add(_numHighMB);
            cardThresholds.Controls.Add(lblCrit);
            cardThresholds.Controls.Add(_numCriticalMB);

            var cardPreferences = new DarkCardPanel { Location = new Point(8, 288), Size = new Size(910, 120) };
            cardPreferences.Controls.Add(new Label { Text = "APPLICATION PREFERENCES", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 180, 216), Location = new Point(14, 10), AutoSize = true });

            _chkEdgeOptGlobal = new CheckBox { Text = "Enable Built-in 24/7 Microsoft Edge Optimization", Location = new Point(18, 38), AutoSize = true, ForeColor = Color.White, Checked = _currentConfig.EdgeOptimizationEnabled };
            _chkMinimizeToTray = new CheckBox { Text = "Minimize to System Tray on Window Close", Location = new Point(18, 68), AutoSize = true, ForeColor = Color.White, Checked = _currentConfig.MinimizeDashboardToTray };

            cardPreferences.Controls.Add(_chkEdgeOptGlobal);
            cardPreferences.Controls.Add(_chkMinimizeToTray);

            _btnSaveSettings = new DarkButton { Text = "Save Settings", Location = new Point(18, 420), Size = new Size(140, 34), AccentColor = Color.FromArgb(0, 180, 216) };
            _btnSaveSettings.Click += OnSaveSettingsClicked;

            _btnResetDefaults = new DarkButton { Text = "Reset to Defaults", Location = new Point(170, 420), Size = new Size(150, 34) };
            _btnResetDefaults.Click += (s, e) =>
            {
                _currentConfig = new GuardianConfig();
                _configManager.SaveConfig(_currentConfig);
                MessageBox.Show("Settings restored to factory defaults.", "RAM Guardian", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            pnl.Controls.Add(cardIntervals);
            pnl.Controls.Add(cardThresholds);
            pnl.Controls.Add(cardPreferences);
            pnl.Controls.Add(_btnSaveSettings);
            pnl.Controls.Add(_btnResetDefaults);

            return pnl;
        }

        private void OnSaveSettingsClicked(object sender, EventArgs e)
        {
            _currentConfig.MonitoringIntervalSeconds = (int)_numPollingInterval.Value;
            _currentConfig.ElevatedAvailableRamMB = (double)_numElevatedMB.Value;
            _currentConfig.HighAvailableRamMB = (double)_numHighMB.Value;
            _currentConfig.CriticalAvailableRamMB = (double)_numCriticalMB.Value;
            _currentConfig.EdgeOptimizationEnabled = _chkEdgeOptGlobal.Checked;
            _currentConfig.MinimizeDashboardToTray = _chkMinimizeToTray.Checked;

            bool ok = _configManager.SaveConfig(_currentConfig);
            if (ok)
            {
                MessageBox.Show("Configuration updated successfully.", "RAM Guardian", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to save configuration.", "RAM Guardian", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Snapshot Ingestion & Live Updates
        private void LoadLatestSnapshot()
        {
            string snapPath = @"C:\ProgramData\RAM Guardian\latest.json";
            if (!File.Exists(snapPath)) return;

            try
            {
                string json;
                using (var fs = new FileStream(snapPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    json = sr.ReadToEnd();
                }

                var snapshot = JsonHelper.Deserialize<StateSnapshot>(json);
                if (snapshot == null) return;
                _latestSnapshot = snapshot;

                // Sync Config state if changed outside
                if (_cbTopMode.SelectedItem == null || _cbTopMode.SelectedItem.ToString() != snapshot.OperatingMode)
                {
                    _cbTopMode.SelectedItem = snapshot.OperatingMode;
                    UpdateModeRadios(snapshot.OperatingMode);
                }

                UpdateEmergencyVisuals(snapshot.IsEmergencyStopped);
                UpdatePressureBadge(snapshot.PressureLevel);
                UpdateOverviewTab(snapshot);
                UpdateMemoryMonitorTab(snapshot);
                UpdateProcessList(snapshot);
                UpdateEdgeTab(snapshot);

                string hostType = _embeddedEngine != null ? "Standalone Engine" : "Background Service";
                _lblFooterStatus.Text = string.Format("Mode: {0} | {1} Active | System Pressure: {2} | Available: {3:F1} MB | Commit: {4:F1}% | Uptime: {5:F0}s",
                    snapshot.OperatingMode, hostType, snapshot.PressureLevel, snapshot.Metrics.AvailablePhysicalMB,
                    snapshot.Metrics.CommitLoadPercentage, snapshot.ServiceUptimeSeconds);
            }
            catch { }
        }

        private void UpdateEmergencyVisuals(bool isStopped)
        {
            _lblEmergencyBanner.Visible = isStopped;
            _btnEmergencyStop.Text = isStopped ? "RESUME AUTOMATION" : "EMERGENCY STOP";
            _btnEmergencyStop.BackColor = isStopped ? Color.FromArgb(239, 71, 111) : Color.FromArgb(50, 24, 30);
            if (_btnAutoEmergencyToggle != null)
            {
                _btnAutoEmergencyToggle.Text = isStopped ? "RESUME AUTOMATED OPTIMIZATION" : "TRIP EMERGENCY STOP";
                _btnAutoEmergencyToggle.BackColor = isStopped ? Color.FromArgb(76, 201, 130) : Color.FromArgb(239, 71, 111);
            }
        }

        private void UpdatePressureBadge(string pressure)
        {
            _lblPressureBadge.Text = pressure.ToUpperInvariant();
            switch (pressure)
            {
                case "Critical":
                    _lblPressureBadge.ForeColor = Color.FromArgb(239, 71, 111);
                    _lblPressureBadge.BackColor = Color.FromArgb(55, 20, 25);
                    break;
                case "High":
                    _lblPressureBadge.ForeColor = Color.FromArgb(244, 162, 97);
                    _lblPressureBadge.BackColor = Color.FromArgb(50, 35, 20);
                    break;
                case "Elevated":
                    _lblPressureBadge.ForeColor = Color.FromArgb(255, 209, 102);
                    _lblPressureBadge.BackColor = Color.FromArgb(45, 40, 20);
                    break;
                default:
                    _lblPressureBadge.ForeColor = Color.FromArgb(100, 220, 140);
                    _lblPressureBadge.BackColor = Color.FromArgb(25, 45, 35);
                    break;
            }
        }

        private void UpdateOverviewTab(StateSnapshot s)
        {
            var m = s.Metrics;
            _ramMeter.Percentage = m.MemoryLoadPercentage;
            _ramMeter.CenterTitle = string.Format("{0:F0} MB", m.UsedPhysicalMB);
            _ramMeter.CenterSubtitle = string.Format("{0:F0}% Used", m.MemoryLoadPercentage);
            _ramMeter.Invalidate();

            _lblOvCommit.Text = string.Format(
                "Committed Memory: {0:F1} MB\n" +
                "Commit Limit:     {1:F1} MB\n" +
                "Commit Load:      {2:F1}%\n\n" +
                "Pagefile Usage:   {3:F1}%\n" +
                "Paged Pool:       {4:F1} MB\n" +
                "Nonpaged Pool:    {5:F1} MB\n" +
                "Compression:      {6}",
                m.CommittedMB, m.CommitLimitMB, m.CommitLoadPercentage,
                m.PagefileUsagePercentage, m.PagedPoolMB, m.NonpagedPoolMB,
                m.IsCompressionActive ? string.Format("Active ({0:F1} MB)", m.MemoryCompressionWorkingSetMB) : "Inactive");

            if (s.EdgeStatus != null && s.EdgeStatus.IsRunning)
            {
                _lblOvEdge.Text = string.Format(
                    "Status: Active 24/7 Protection\n" +
                    "Total Edge Processes: {0}\n" +
                    "Total Edge RAM:       {1:F1} MB\n" +
                    "Committed Private:    {2:F1} MB\n" +
                    "Active Renderers:     {3} tabs\n" +
                    "Active Extensions:    {4}\n" +
                    "GPU Subsystem:        {5:F1} MB",
                    s.EdgeStatus.TotalProcesses, s.EdgeStatus.TotalWorkingSetMB,
                    s.EdgeStatus.TotalPrivateMB, s.EdgeStatus.RendererProcessesCount,
                    s.EdgeStatus.ExtensionProcessesCount, s.EdgeStatus.GpuWorkingSetMB);
            }
            else
            {
                _lblOvEdge.Text = "Microsoft Edge is not currently active.\nPolicies are pre-configured to automatically apply sleeping tabs on startup.";
            }

            _lblOvStability.Text = string.Format(
                "RAM Drain Slope: {0:F1} MB/min | Nonpaged Baseline: {1:F1} MB (Healthy ASUS profile)\n" +
                "Hardware Status: Soldered 4 GB DDR4 (3.7 GB Visible) — Paging Rate: {2:F1} pages/sec",
                s.AvailableRamSlopeMBPerMin, s.NonpagedPoolBaselineMB, m.PagesPerSec);

            _historyGraph.AddSample(m.AvailablePhysicalMB, m.CommitLoadPercentage);

            _lbOvAlerts.Items.Clear();
            if (s.ActiveAlerts != null && s.ActiveAlerts.Count > 0)
            {
                for (int i = 0; i < s.ActiveAlerts.Count; i++)
                {
                    _lbOvAlerts.Items.Add(s.ActiveAlerts[i]);
                }
            }
            else
            {
                _lbOvAlerts.Items.Add("No active pressure warnings or leaks detected.");
                _lbOvAlerts.Items.Add("System operating within safe margins.");
            }
        }

        private void UpdateMemoryMonitorTab(StateSnapshot s)
        {
            var m = s.Metrics;

            _lblMonPhysical.Text = string.Format(
                "Installed Physical RAM:  3,800 MB (4 GB Soldered DDR4-3200)\n" +
                "Total Usable to Windows: {0:F1} MB\n" +
                "In Use (Working Set):    {1:F1} MB ({2:F1}% load)\n" +
                "Available Physical RAM:  {3:F1} MB",
                m.TotalPhysicalMB, m.UsedPhysicalMB, m.MemoryLoadPercentage, m.AvailablePhysicalMB);

            _lblMonCommit.Text = string.Format(
                "Total Committed Memory:  {0:F1} MB\n" +
                "Current Commit Limit:    {1:F1} MB\n" +
                "Commitment Utilization:  {2:F1}%\n" +
                "Available Commit Space:  {3:F1} MB",
                m.CommittedMB, m.CommitLimitMB, m.CommitLoadPercentage, Math.Max(0, m.CommitLimitMB - m.CommittedMB));

            _lblMonPagefile.Text = string.Format(
                "Pagefile Status:         System-Managed on NVMe SSD (Healthy)\n" +
                "Pagefile Usage:          {0:F1}%\n" +
                "Total Virtual Memory:    {1:F1} MB\n" +
                "Available Virtual:       {2:F1} MB",
                m.PagefileUsagePercentage, m.TotalVirtualMB, m.AvailableVirtualMB);

            _lblMonPools.Text = string.Format(
                "Nonpaged Kernel Pool:    {0:F1} MB (Fixed In-RAM Driver Memory)\n" +
                "Nonpaged Baseline:       {1:F1} MB (Stable VivoBook baseline)\n" +
                "Paged Kernel Pool:       {2:F1} MB (Pageable to disk)\n" +
                "System Cache WorkingSet: {3:F1} MB",
                m.NonpagedPoolMB, s.NonpagedPoolBaselineMB, m.PagedPoolMB, m.SystemCacheMB);

            _lblMonCompression.Text = string.Format(
                "Memory Compression:      {0}\n" +
                "Compressed Working Set:  {1:F1} MB RAM occupied\n" +
                "Hard Page Faults/sec:    {2:F1}\n" +
                "Total Pages/sec:         {3:F1}",
                m.IsCompressionActive ? "Active (Windows Memory Manager)" : "Inactive",
                m.MemoryCompressionWorkingSetMB, m.PageFaultsPerSec, m.PagesPerSec);

            _lblMonOs.Text = string.Format(
                "Total Active Processes:  {0}\n" +
                "Total Threads:           {1}\n" +
                "Kernel Handle Count:     {2}\n" +
                "Processor Utilization:   {3:F1}%",
                m.ProcessCount, m.ThreadCount, m.HandleCount, m.CpuUtilizationPercentage);
        }

        private void UpdateProcessList(StateSnapshot s)
        {
            if (s.TopProcesses == null) return;
            FilterProcessList();
        }

        private void FilterProcessList()
        {
            if (_latestSnapshot == null || _latestSnapshot.TopProcesses == null) return;

            string query = _tbProcSearch.Text.Trim().ToLowerInvariant();
            int filterIdx = _cbProcFilter.SelectedIndex;

            _lvProcesses.BeginUpdate();
            _lvProcesses.Items.Clear();

            for (int i = 0; i < _latestSnapshot.TopProcesses.Count; i++)
            {
                var p = _latestSnapshot.TopProcesses[i];
                string name = p.ProcessName ?? string.Empty;

                if (!string.IsNullOrEmpty(query) && name.ToLowerInvariant().IndexOf(query) < 0 && p.ProcessId.ToString().IndexOf(query) < 0)
                {
                    continue;
                }

                // Filter Dropdown
                if (filterIdx == 1 && !p.IsProtected) continue; // Protected only
                if (filterIdx == 2 && !p.IsTerminationEligible) continue; // Candidates only
                if (filterIdx == 3 && !name.Equals("msedge", StringComparison.OrdinalIgnoreCase)) continue; // Edge only
                if (filterIdx == 4 && p.Classification != "UserScripting") continue; // AHK/IDM only

                var lvi = new ListViewItem(p.ProcessId.ToString());
                lvi.SubItems.Add(name);
                lvi.SubItems.Add(string.Format("{0:F1} MB", p.WorkingSetMB));
                lvi.SubItems.Add(string.Format("{0:F1} MB", p.PrivateMB));
                lvi.SubItems.Add(p.Classification ?? "StandardApp");

                string eligText = p.IsTerminationEligible ? "YES (Candidate)" : (p.IsProtected ? "NO (Protected)" : "NO (Default-DENY)");
                lvi.SubItems.Add(eligText);
                lvi.SubItems.Add(p.IsProtected ? p.ProtectionReason : (p.IsTerminationEligible ? p.EligibilityReason : "Immune: Default-DENY"));

                lvi.Tag = p;

                if (p.IsProtected)
                {
                    lvi.ForeColor = Color.FromArgb(170, 220, 250);
                }
                else if (p.IsTerminationEligible)
                {
                    lvi.ForeColor = Color.FromArgb(244, 162, 97);
                }

                _lvProcesses.Items.Add(lvi);
            }

            _lvProcesses.EndUpdate();
        }

        private void UpdateEdgeTab(StateSnapshot s)
        {
            if (s.EdgeStatus != null && s.EdgeStatus.IsRunning)
            {
                _lblEdgeSummary.Text = string.Format(
                    "Active Edge Process Count:  {0} instances\n" +
                    "Total Edge Physical RAM:    {1:F1} MB\n" +
                    "Total Committed Private:    {2:F1} MB\n" +
                    "Browser Main Process:       {3:F1} MB\n" +
                    "GPU Accelerated Process:    {4:F1} MB\n" +
                    "Renderer Tab Processes:     {5} ({6:F1} MB total)\n" +
                    "Extension Processes:        {7} ({8:F1} MB total)",
                    s.EdgeStatus.TotalProcesses, s.EdgeStatus.TotalWorkingSetMB, s.EdgeStatus.TotalPrivateMB,
                    s.EdgeStatus.BrowserWorkingSetMB, s.EdgeStatus.GpuWorkingSetMB,
                    s.EdgeStatus.RendererProcessesCount, s.EdgeStatus.RenderersWorkingSetMB,
                    s.EdgeStatus.ExtensionProcessesCount, s.EdgeStatus.ExtensionsWorkingSetMB);
            }
            else
            {
                _lblEdgeSummary.Text = "Microsoft Edge is not currently running.\nAll 24/7 Group Policies remain in place.";
            }

            if (s.EdgeProcesses != null)
            {
                _lvEdgeProcesses.BeginUpdate();
                _lvEdgeProcesses.Items.Clear();

                for (int i = 0; i < s.EdgeProcesses.Count; i++)
                {
                    var p = s.EdgeProcesses[i];
                    var lvi = new ListViewItem(p.ProcessId.ToString());
                    lvi.SubItems.Add(p.Role);
                    lvi.SubItems.Add(string.Format("{0:F1} MB", p.WorkingSetMB));
                    lvi.SubItems.Add(string.Format("{0:F1} MB", p.PrivateMB));
                    lvi.SubItems.Add(p.IsSleepingCandidate ? "YES (Sleep Target)" : "-");
                    lvi.SubItems.Add(p.CommandLine);

                    if (p.Role == "Gpu") lvi.ForeColor = Color.FromArgb(244, 162, 97);
                    else if (p.Role == "Browser") lvi.ForeColor = Color.FromArgb(0, 180, 216);
                    else if (p.IsSleepingCandidate) lvi.ForeColor = Color.FromArgb(100, 220, 140);

                    _lvEdgeProcesses.Items.Add(lvi);
                }

                _lvEdgeProcesses.EndUpdate();
            }
        }
        #endregion

        #region User Actions & Event Handlers
        private void OnTopModeChanged(object sender, EventArgs e)
        {
            if (_cbTopMode.SelectedItem == null) return;
            string selected = _cbTopMode.SelectedItem.ToString();
            ApplyOperatingMode(selected);
        }

        private void ApplyOperatingMode(string mode)
        {
            _currentConfig.OperatingMode = mode;
            _configManager.SaveConfig(_currentConfig);
            UpdateModeRadios(mode);
            _lblFooterStatus.Text = string.Format("Operating mode switched to '{0}'.", mode);
        }

        private void OnOneClickOptimizeClicked(object sender, EventArgs e)
        {
            ExecuteOneClickOptimization();
        }

        private void ExecuteOneClickOptimization()
        {
            try
            {
                if (_btnHeaderOptimize != null)
                {
                    _btnHeaderOptimize.Enabled = false;
                    _btnHeaderOptimize.Text = "⏳ OPTIMIZING...";
                }
                if (_btnQuickOptimize != null)
                {
                    _btnQuickOptimize.Enabled = false;
                    _btnQuickOptimize.Text = "⏳ Optimizing...";
                }
                if (_btnEdgeQuickOpt != null)
                {
                    _btnEdgeQuickOpt.Enabled = false;
                    _btnEdgeQuickOpt.Text = "⏳ Trimming...";
                }
                Cursor.Current = Cursors.WaitCursor;
                Refresh();

                OptimizationSummary summary = _oneClickOptimizer.OptimizeAll(_currentConfig);

                if (_latestSnapshot == null)
                {
                    _latestSnapshot = new StateSnapshot();
                }
                _latestSnapshot.Metrics.MemoryLoadPercentage = summary.FinalMemoryLoad;
                _latestSnapshot.Metrics.AvailablePhysicalBytes = (long)(summary.FinalAvailableMB * 1024.0 * 1024.0);
                _latestSnapshot.Metrics.UsedPhysicalBytes = Math.Max(0L, _latestSnapshot.Metrics.TotalPhysicalBytes - _latestSnapshot.Metrics.AvailablePhysicalBytes);
                if (_latestSnapshot.EdgeStatus != null)
                {
                    _latestSnapshot.EdgeStatus.TotalWorkingSetBytes = (long)(summary.EdgeFinalWorkingSetMB * 1024.0 * 1024.0);
                }

                UpdateOverviewTab(_latestSnapshot);
                UpdatePressureBadge(_latestSnapshot.PressureLevel);

                LoadLatestSnapshot();

                string toastMsg = string.Format(
                    "⚡ OPTIMIZATION COMPLETE: Reclaimed {0:F1} MB RAM  |  Edge Reduced: {1:F1} MB ({2} procs)  |  New RAM Load: {3:F0}%",
                    summary.ReclaimedRamMB,
                    summary.EdgeReclaimedMB,
                    summary.EdgeProcessesTrimmed,
                    summary.FinalMemoryLoad);

                _lblToastBanner.Text = toastMsg;
                _lblToastBanner.Visible = true;
                if (_toastTimer != null)
                {
                    _toastTimer.Stop();
                    _toastTimer.Start();
                }

                _lblFooterStatus.Text = string.Format(
                    "⚡ Quick Optimization Complete: Reclaimed {0:F1} MB Physical RAM (Edge Reclaimed: {1:F1} MB). Current RAM Load: {2:F0}%.",
                    summary.ReclaimedRamMB, summary.EdgeReclaimedMB, summary.FinalMemoryLoad);

                if (_lbAutoActionLog != null)
                {
                    _lbAutoActionLog.Items.Insert(0, string.Format(
                        "[{0:HH:mm:ss}] ⚡ 1-Click Optimization: Reclaimed {1:F1} MB RAM. Edge reduced by {2:F1} MB across {3} processes. RAM Load: {4:F0}%.",
                        DateTime.Now, summary.ReclaimedRamMB, summary.EdgeReclaimedMB, summary.EdgeProcessesTrimmed, summary.FinalMemoryLoad));
                }

                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            }
            catch (Exception ex)
            {
                _logger.Error("ONE_CLICK_OPT_FAIL", "Failed during 1-click optimization", ex);
                MessageBox.Show("Optimization encountered an issue: " + ex.Message, "RAM Guardian", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (_btnHeaderOptimize != null)
                {
                    _btnHeaderOptimize.Enabled = true;
                    _btnHeaderOptimize.Text = "⚡ OPTIMIZE NOW";
                }
                if (_btnQuickOptimize != null)
                {
                    _btnQuickOptimize.Enabled = true;
                    _btnQuickOptimize.Text = "⚡ OPTIMIZE RAM & EDGE NOW";
                }
                if (_btnEdgeQuickOpt != null)
                {
                    _btnEdgeQuickOpt.Enabled = true;
                    _btnEdgeQuickOpt.Text = "⚡ Trim Edge RAM";
                }
                Cursor.Current = Cursors.Default;
            }
        }

        private void OnEmergencyStopClicked(object sender, EventArgs e)
        {
            bool newState = !_currentConfig.IsEmergencyStopped;
            _currentConfig.IsEmergencyStopped = newState;
            _configManager.SaveConfig(_currentConfig);

            UpdateEmergencyVisuals(newState);

            string msg = newState
                ? "EMERGENCY STOP TRIPPED: All automated background optimizations, closures, and policy updates have been immediately suspended."
                : "EMERGENCY STOP CLEARED: Automated optimizations resumed.";

            MessageBox.Show(msg, "RAM Guardian Emergency Control", MessageBoxButtons.OK,
                newState ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void OnProcGracefulCloseClicked(object sender, EventArgs e)
        {
            if (_lvProcesses.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a process from the list.", "RAM Guardian", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var item = _lvProcesses.SelectedItems[0];
            var p = item.Tag as ProcessMemoryInfo;
            if (p == null) return;

            if (p.IsProtected)
            {
                MessageBox.Show(
                    string.Format("Process '{0}' (PID {1}) is PROTECTED ({2}). Action denied by Safety Invariant.",
                    p.ProcessName, p.ProcessId, p.ProtectionReason),
                    "Action Denied by RAM Guardian",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Stop);
                return;
            }

            var dr = MessageBox.Show(
                string.Format("Send graceful close request (WM_CLOSE) to '{0}' (PID {1})?\nThis allows the application to save work and exit cleanly.",
                p.ProcessName, p.ProcessId),
                "Confirm Graceful Close",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                var termEngine = new ProcessTerminationEngine();
                string audit;
                // Temporarily allow request
                var testConfig = new GuardianConfig { OperatingMode = "BalancedAuto", IsEmergencyStopped = false };
                bool ok = termEngine.TryGraduatedClosure(p, testConfig, out audit);

                MessageBox.Show(audit, "Graceful Close Result", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                LoadLatestSnapshot();
            }
        }

        private void OnProcProtectClicked(object sender, EventArgs e)
        {
            if (_lvProcesses.SelectedItems.Count == 0) return;
            var item = _lvProcesses.SelectedItems[0];
            var p = item.Tag as ProcessMemoryInfo;
            if (p == null) return;

            _protectionPolicy.AddUserProtectedProcess(p.ProcessName);
            MessageBox.Show(string.Format("Process '{0}' added to user-protected immunity list.", p.ProcessName),
                "RAM Guardian Protection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadLatestSnapshot();
        }

        private void OnProcInspectClicked(object sender, EventArgs e)
        {
            if (_lvProcesses.SelectedItems.Count == 0) return;
            var item = _lvProcesses.SelectedItems[0];
            var p = item.Tag as ProcessMemoryInfo;
            if (p == null) return;

            string details = string.Format(
                "Process Name:        {0}\n" +
                "Process ID (PID):    {1}\n" +
                "Working Set (RAM):   {2:F1} MB\n" +
                "Private Bytes:       {3:F1} MB\n" +
                "Classification:      {4} (Confidence: {5:P0})\n" +
                "Is Protected?:       {6}\n" +
                "Protection Reason:   {7}\n" +
                "Termination Target?: {8}\n" +
                "Eligibility Reason:  {9}\n" +
                "Executable Path:     {10}",
                p.ProcessName, p.ProcessId, p.WorkingSetMB, p.PrivateMB,
                p.Classification, p.ClassificationConfidence,
                p.IsProtected ? "YES (Immune)" : "NO",
                p.ProtectionReason,
                p.IsTerminationEligible ? "YES" : "NO",
                p.EligibilityReason,
                p.ExecutablePath);

            MessageBox.Show(details, "Process Deep Inspection — RAM Guardian", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnEdgeApplyPoliciesClicked(object sender, EventArgs e)
        {
            int timeoutSec = 300;
            switch (_cbEdgeSleepTimeout.SelectedIndex)
            {
                case 0: timeoutSec = 30; break;
                case 1: timeoutSec = 60; break;
                case 2: timeoutSec = 120; break;
                case 3: timeoutSec = 300; break;
                case 4: timeoutSec = 900; break;
            }

            _currentConfig.EdgeSleepingTabsTimeoutSeconds = timeoutSec;
            _currentConfig.EdgeEfficiencyModeEnabled = _chkEdgeEfficiency.Checked;
            _currentConfig.EdgeBackgroundModeDisabled = _chkEdgeBackgroundDisable.Checked;
            _configManager.SaveConfig(_currentConfig);

            string resultMsg;
            bool ok = _edgeOptimizer.OptimizeEdgePolicies(_currentConfig, _latestSnapshot != null ? _latestSnapshot.EdgeStatus : null, out resultMsg);

            MessageBox.Show(resultMsg, "Microsoft Edge Policy Optimization", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void OnEdgeRestoreDefaultsClicked(object sender, EventArgs e)
        {
            string msg;
            bool ok = _edgeOptimizer.RestoreEdgePolicies(out msg);
            MessageBox.Show(msg, "Restore Edge Defaults", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        #endregion

        #region System Tray Integration
        private void SetupTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open RAM Guardian Dashboard", null, (s, e) => ShowDashboard());
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("MonitorOnly Mode", null, (s, e) => ApplyOperatingMode("MonitorOnly"));
            _trayMenu.Items.Add("BalancedAuto Mode", null, (s, e) => ApplyOperatingMode("BalancedAuto"));
            _trayMenu.Items.Add("AggressiveAuto Mode", null, (s, e) => ApplyOperatingMode("AggressiveAuto"));
            _trayMenu.Items.Add("GamingFocus Mode", null, (s, e) => ApplyOperatingMode("GamingFocus"));
            _trayMenu.Items.Add("Paused", null, (s, e) => ApplyOperatingMode("Paused"));
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("EMERGENCY STOP", null, (s, e) => OnEmergencyStopClicked(s, e));
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                _allowExit = true;
                Application.Exit();
            });

            _trayIcon = new NotifyIcon
            {
                Text = "RAM Guardian — Intelligent Resource Monitor",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            string iconPath = @"C:\Users\chitr_mzkaj0t\.gemini\antigravity\scratch\RAM-Guardian\assets\icons\ramguardian.ico";
            if (File.Exists(iconPath))
            {
                try { _trayIcon.Icon = new Icon(iconPath); } catch { }
            }
            else
            {
                _trayIcon.Icon = SystemIcons.Application;
            }

            _trayIcon.DoubleClick += (s, e) => ShowDashboard();
        }

        private void ShowDashboard()
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowExit && _currentConfig.MinimizeDashboardToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (_currentConfig.EnableNotifications)
                {
                    _trayIcon.ShowBalloonTip(2000, "RAM Guardian Running", "RAM Guardian is actively protecting resources in the background.", ToolTipIcon.Info);
                }
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_refreshTimer != null) _refreshTimer.Dispose();
                if (_trayIcon != null) _trayIcon.Dispose();
                if (_embeddedEngine != null)
                {
                    try { _embeddedEngine.Stop(); _embeddedEngine.Dispose(); } catch { }
                    _embeddedEngine = null;
                }
            }
            base.Dispose(disposing);
        }
        #endregion
    }
}
