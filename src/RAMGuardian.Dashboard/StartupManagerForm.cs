using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RAMGuardian.Logging;
using RAMGuardian.Optimization;
using RAMGuardian.Optimization.Actions;
using RAMGuardian.Optimization.Models;
using RAMGuardian.Security.Policies;

namespace RAMGuardian.Dashboard
{
    public class StartupManagerForm : Form
    {
        private readonly StartupScanner _scanner;
        private readonly BackupManager _backupManager;
        private readonly ActionRegistry _actionRegistry;
        private readonly FileLogger _logger;

        private ListView _listView;
        private DarkButton _btnOptimizeSelected;
        private DarkButton _btnRestoreAll;
        private DarkButton _btnClose;
        private Label _lblStatus;

        public StartupManagerForm()
        {
            _scanner = new StartupScanner();
            _backupManager = new BackupManager();
            _actionRegistry = new ActionRegistry();
            _logger = new FileLogger();

            InitializeComponent();
            LoadStartupEntries();
        }

        private void InitializeComponent()
        {
            Text = "RAM Guardian — Startup & Service Inventory";
            Size = new Size(820, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(18, 20, 26);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            ShowIcon = false;
            MinimizeBox = false;
            MaximizeBox = false;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(16, 12, 16, 0) };
            var lblTitle = new Label
            {
                Text = "STARTUP INVENTORY & PROTECTION REGISTRY",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 216),
                AutoSize = true,
                Location = new Point(14, 10)
            };
            var lblSub = new Label
            {
                Text = "Review discovered startup items. Essential system and user-protected items (AHK, Defender) cannot be disabled.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 165, 185),
                AutoSize = true,
                Location = new Point(15, 34)
            };
            pnlTop.Controls.Add(lblTitle);
            pnlTop.Controls.Add(lblSub);

            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(24, 26, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            _listView.Columns.Add("Entry Name", 140);
            _listView.Columns.Add("Location", 110);
            _listView.Columns.Add("Status / Safety", 130);
            _listView.Columns.Add("Policy Assessment", 380);

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(14, 10, 14, 10) };
            _lblStatus = new Label
            {
                Text = "Ready.",
                ForeColor = Color.FromArgb(160, 165, 185),
                AutoSize = true,
                Location = new Point(16, 18)
            };

            _btnOptimizeSelected = new DarkButton
            {
                Text = "Disable Selected (Reversible)",
                Width = 200,
                Height = 32,
                Location = new Point(340, 10)
            };
            _btnOptimizeSelected.Click += BtnOptimizeSelected_Click;

            _btnRestoreAll = new DarkButton
            {
                Text = "Restore All Backups",
                Width = 140,
                Height = 32,
                Location = new Point(550, 10)
            };
            _btnRestoreAll.Click += BtnRestoreAll_Click;

            _btnClose = new DarkButton
            {
                Text = "Close",
                Width = 90,
                Height = 32,
                Location = new Point(700, 10)
            };
            _btnClose.Click += (s, e) => Close();

            pnlBottom.Controls.Add(_lblStatus);
            pnlBottom.Controls.Add(_btnOptimizeSelected);
            pnlBottom.Controls.Add(_btnRestoreAll);
            pnlBottom.Controls.Add(_btnClose);

            Controls.Add(_listView);
            Controls.Add(pnlTop);
            Controls.Add(pnlBottom);
        }

        private void LoadStartupEntries()
        {
            _listView.Items.Clear();
            List<StartupEntry> entries = _scanner.ScanAll();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var item = new ListViewItem(entry.Name);
                item.SubItems.Add(entry.Location);

                string statusText = entry.IsApprovedForOptimization ? "Approved (User)" : "PROTECTED (Safe)";
                item.SubItems.Add(statusText);
                item.SubItems.Add(entry.ProtectionReason);

                if (!entry.IsApprovedForOptimization)
                {
                    item.ForeColor = Color.FromArgb(100, 220, 140); // Green for safe / protected
                }
                else
                {
                    item.ForeColor = Color.FromArgb(244, 162, 97); // Amber for user-approved target
                }

                item.Tag = entry;
                _listView.Items.Add(item);
            }

            _lblStatus.Text = string.Format("Total {0} entries discovered.", entries.Count);
        }

        private void BtnOptimizeSelected_Click(object sender, EventArgs e)
        {
            if (_listView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an approved startup entry to disable.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = _listView.SelectedItems[0];
            var entry = selectedItem.Tag as StartupEntry;

            if (entry == null || !entry.IsApprovedForOptimization)
            {
                MessageBox.Show("This entry is PROTECTED by RAM Guardian's safety policy and cannot be modified.",
                    "Action Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult dr = MessageBox.Show(
                string.Format("Are you sure you want to disable automatic background startup for '{0}'?\n\nA full safety backup will be created in C:\\ProgramData\\RAM Guardian\\backups and can be reversed at any time.", entry.Name),
                "Confirm Reversible Optimization",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                string actionId = "OPT_STARTUP_" + entry.Name.ToUpperInvariant() + "_DISABLE";
                var action = _actionRegistry.GetAction(actionId);

                string execResult;
                bool ok = false;

                if (action != null)
                {
                    ok = action.Execute(_backupManager, _logger, out execResult);
                }
                else
                {
                    // Generic approved fallback action
                    var fallbackAction = new StartupDisableAction(actionId, entry.Name, "User requested startup disabling", "Relieves background memory pressure", entry.Location == "HKLM_Run");
                    ok = fallbackAction.Execute(_backupManager, _logger, out execResult);
                }

                if (ok)
                {
                    MessageBox.Show(execResult, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadStartupEntries();
                }
                else
                {
                    MessageBox.Show(execResult, "Operation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnRestoreAll_Click(object sender, EventArgs e)
        {
            List<string> errors;
            int restored = _backupManager.RestoreAll(out errors);

            if (restored > 0)
            {
                MessageBox.Show(string.Format("Successfully restored {0} change(s) back to original state.", restored),
                    "Changes Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadStartupEntries();
            }
            else
            {
                MessageBox.Show("No active changes needed restoration.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
