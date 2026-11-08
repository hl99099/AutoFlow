using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoFlow
{
    public class MainForm : Form
    {
        private readonly AppConfig _config;

        private ListView _ruleList;
        private TextBox _logBox;
        private Label _statusLabel;
        private ToolStripStatusLabel _pathLabel;
        private ToolStripButton _pauseButton;
        private NotifyIcon _notifyIcon;
        private ToolStripMenuItem _pauseItem;

        private TriggerEngine _engine;
        private ActionExecutor _executor;

        private bool _suppressCheck;
        private bool _exiting;
        private bool _paused;
        private bool _trayTipShown;
        private bool _adminWarned;

        public MainForm(AppConfig config)
        {
            _config = config ?? new AppConfig();
            InitializeUi();
        }

        private void InitializeUi()
        {
            Text = "AutoFlow - 自动联动启动管理器";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 700);
            MinimumSize = new Size(880, 600);
            BackColor = UiTheme.Window;
            Font = UiTheme.BaseFont;
            Icon = AppIcon.Value;
            DoubleBuffered = true;

            ToolStrip toolStrip = new ToolStrip();
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.ImageScalingSize = new Size(16, 16);
            toolStrip.AutoSize = false;
            toolStrip.Height = 58;
            toolStrip.Padding = new Padding(14, 12, 14, 12);
            toolStrip.BackColor = UiTheme.Surface;
            toolStrip.ForeColor = UiTheme.Text;
            toolStrip.Font = UiTheme.BaseFont;
            toolStrip.Renderer = new ToolStripProfessionalRenderer(new UiTheme.ModernColors());

            toolStrip.Items.Add(MakeButton("添加规则", delegate { AddRule(); }));
            toolStrip.Items.Add(MakeButton("编辑", delegate { EditRule(); }));
            toolStrip.Items.Add(MakeButton("删除", delegate { RemoveRule(); }));
            toolStrip.Items.Add(MakeButton("复制", delegate { CopyRule(); }));
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(MakeButton("测试执行", delegate { TestRule(); }));
            toolStrip.Items.Add(new ToolStripSeparator());
            _pauseButton = MakeButton("暂停监听", OnTogglePause);
            toolStrip.Items.Add(_pauseButton);
            toolStrip.Items.Add(MakeButton("设置", delegate { OpenSettings(); }));
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(MakeButton("创建桌面快捷方式", delegate { CreateDesktopShortcut(); }));
            toolStrip.Items.Add(MakeButton("配置目录", delegate { OpenFolder(Path.GetDirectoryName(AppPaths.ConfigPath)); }));
            toolStrip.Items.Add(MakeButton("日志目录", delegate { OpenFolder(AppPaths.LogDir); }));

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 78;
            header.BackColor = UiTheme.Surface;
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(UiTheme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
                }
            };

            PictureBox logo = new PictureBox();
            logo.Image = AppIcon.Value.ToBitmap();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Size = new Size(44, 44);
            logo.Location = new Point(22, 16);
            logo.BackColor = Color.Transparent;

            Label appTitle = new Label();
            appTitle.Text = "AutoFlow";
            appTitle.Font = UiTheme.TitleFont;
            appTitle.ForeColor = UiTheme.Text;
            appTitle.AutoSize = true;
            appTitle.BackColor = Color.Transparent;
            appTitle.Location = new Point(78, 14);

            Label appSubtitle = new Label();
            appSubtitle.Text = "自动联动启动管理器 · AutoFlow Application Manager";
            appSubtitle.Font = UiTheme.SubtitleFont;
            appSubtitle.ForeColor = UiTheme.Muted;
            appSubtitle.AutoSize = true;
            appSubtitle.BackColor = Color.Transparent;
            appSubtitle.Location = new Point(80, 46);

            _statusLabel = new Label();
            _statusLabel.Text = "正在启动...";
            _statusLabel.Font = UiTheme.BaseFont;
            _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            _statusLabel.BackColor = UiTheme.PrimarySoft;
            _statusLabel.ForeColor = UiTheme.Primary;
            _statusLabel.Size = new Size(300, 32);
            _statusLabel.Location = new Point(header.Width - 322, 22);
            _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Resize += delegate { _statusLabel.Left = header.Width - _statusLabel.Width - 22; };

            header.Controls.Add(logo);
            header.Controls.Add(appTitle);
            header.Controls.Add(appSubtitle);
            header.Controls.Add(_statusLabel);

            Panel contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = UiTheme.Window;
            contentPanel.Padding = new Padding(16, 12, 16, 8);

            Panel ruleCard = UiTheme.CreateCard();
            ruleCard.Dock = DockStyle.Fill;
            ruleCard.Padding = new Padding(18, 46, 18, 14);

            Label ruleTitle = UiTheme.CreateSectionTitle("联动规则");
            ruleTitle.Location = new Point(18, 14);
            Label ruleHint = UiTheme.CreateHint("勾选第一列可即时启用或停用规则", 96, 15, 280, 18);

            _ruleList = new ListView();
            _ruleList.Dock = DockStyle.Fill;
            _ruleList.View = View.Details;
            _ruleList.FullRowSelect = true;
            _ruleList.MultiSelect = false;
            _ruleList.HideSelection = false;
            _ruleList.CheckBoxes = true;
            _ruleList.GridLines = false;
            _ruleList.BorderStyle = BorderStyle.None;
            _ruleList.BackColor = UiTheme.Surface;
            _ruleList.ForeColor = UiTheme.Text;
            _ruleList.Font = UiTheme.ListFont;
            _ruleList.Columns.Add("启用", 60);
            _ruleList.Columns.Add("规则名称", 240);
            _ruleList.Columns.Add("触发条件", 240);
            _ruleList.Columns.Add("执行动作", 400);
            _ruleList.Resize += delegate { UpdateRuleColumns(); };
            _ruleList.DoubleClick += delegate { EditRule(); };
            _ruleList.ItemChecked += OnItemChecked;
            UpdateRuleColumns();

            ImageList rowHeight = new ImageList();
            rowHeight.ImageSize = new Size(1, 30);
            rowHeight.ColorDepth = ColorDepth.Depth32Bit;
            rowHeight.Images.Add(new Bitmap(1, 30));
            _ruleList.SmallImageList = rowHeight;

            ruleCard.Controls.Add(_ruleList);
            ruleCard.Controls.Add(ruleTitle);
            ruleCard.Controls.Add(ruleHint);
            contentPanel.Controls.Add(ruleCard);

            Panel logPanel = new Panel();
            logPanel.Dock = DockStyle.Bottom;
            logPanel.Height = 208;
            logPanel.BackColor = UiTheme.Window;
            logPanel.Padding = new Padding(16, 4, 16, 12);

            Panel logCard = UiTheme.CreateCard();
            logCard.Dock = DockStyle.Fill;
            logCard.Padding = new Padding(18, 44, 18, 14);

            Label logTitle = UiTheme.CreateSectionTitle("运行日志");
            logTitle.Location = new Point(18, 14);
            Label logHint = UiTheme.CreateHint("实时显示触发与执行记录", 96, 15, 220, 18);

            _logBox = new TextBox();
            _logBox.Dock = DockStyle.Fill;
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.BackColor = UiTheme.LogBackground;
            _logBox.ForeColor = UiTheme.LogText;
            _logBox.Font = UiTheme.MonoFont;

            logCard.Controls.Add(_logBox);
            logCard.Controls.Add(logTitle);
            logCard.Controls.Add(logHint);
            logPanel.Controls.Add(logCard);

            StatusStrip statusStrip = new StatusStrip();
            statusStrip.SizingGrip = false;
            statusStrip.AutoSize = false;
            statusStrip.Height = 30;
            statusStrip.BackColor = UiTheme.Surface;
            statusStrip.ForeColor = UiTheme.Muted;
            statusStrip.Font = UiTheme.SubtitleFont;
            statusStrip.Renderer = new ToolStripProfessionalRenderer(new UiTheme.ModernColors());
            _pathLabel = new ToolStripStatusLabel();
            _pathLabel.Spring = true;
            _pathLabel.TextAlign = ContentAlignment.MiddleRight;
            _pathLabel.ForeColor = UiTheme.Muted;
            _pathLabel.Text = "配置：" + AppPaths.ConfigPath;
            statusStrip.Items.Add(_pathLabel);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = AppIcon.Value;
            _notifyIcon.Text = "AutoFlow - 自动联动启动管理器";
            _notifyIcon.Visible = true;

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示主界面", null, delegate { RestoreWindow(); });
            _pauseItem = new ToolStripMenuItem("暂停监听", null, OnTogglePause);
            trayMenu.Items.Add(_pauseItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("设置", null, delegate { RestoreWindow(); OpenSettings(); });
            trayMenu.Items.Add("打开配置目录", null, delegate { OpenFolder(Path.GetDirectoryName(AppPaths.ConfigPath)); });
            trayMenu.Items.Add("打开日志目录", null, delegate { OpenFolder(AppPaths.LogDir); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("退出", null, delegate { ExitApplication(); });
            _notifyIcon.ContextMenuStrip = trayMenu;
            _notifyIcon.DoubleClick += delegate { RestoreWindow(); };

            Controls.Add(contentPanel);
            Controls.Add(logPanel);
            Controls.Add(statusStrip);
            Controls.Add(toolStrip);
            Controls.Add(header);

            FormClosing += OnFormClosing;
            Resize += OnResize;
        }

        private static ToolStripButton MakeButton(string text, EventHandler handler)
        {
            ToolStripButton button = new ToolStripButton();
            button.Text = text;
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.AutoSize = true;
            button.Font = UiTheme.BaseFont;
            button.ForeColor = UiTheme.Text;
            button.Padding = new Padding(8, 4, 8, 4);
            button.Margin = new Padding(1, 0, 1, 0);
            button.Click += handler;
            return button;
        }

        private void InitializeServices()
        {
            _executor = new ActionExecutor();
            _executor.ActionLogged += OnActionLogged;

            _engine = new TriggerEngine(_executor.IsSuppressed);
            _engine.RuleTriggered += OnRuleTriggered;
            _engine.StatusChanged += OnEngineStatusChanged;
            _engine.Start(_config);

            AppendLog("程序已启动，配置文件：" + AppPaths.ConfigPath);
            if (AppPaths.IsFallbackLocation)
            {
                AppendLog("注意：程序目录不可写，配置已保存到 " + Path.GetDirectoryName(AppPaths.ConfigPath));
            }
        }

        // ---------- 规则列表 ----------

        private void RefreshRuleList()
        {
            _suppressCheck = true;
            try
            {
                _ruleList.BeginUpdate();
                _ruleList.Items.Clear();
                foreach (RuleConfig rule in _config.Rules)
                {
                    ListViewItem item = new ListViewItem();
                    item.Checked = rule.Enabled;
                    item.SubItems.Add(rule.Name);
                    item.SubItems.Add(rule.TriggerSummary);
                    item.SubItems.Add(rule.ActionSummary);
                    item.Tag = rule;
                    _ruleList.Items.Add(item);
                }
                _ruleList.EndUpdate();
            }
            finally
            {
                _suppressCheck = false;
            }
        }

        private void OnItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_suppressCheck) return;
            RuleConfig rule = e.Item.Tag as RuleConfig;
            if (rule == null) return;

            rule.Enabled = e.Item.Checked;
            if (SaveAll(true))
            {
                AppendLog((rule.Enabled ? "已启用规则：" : "已停用规则：") + rule.Name);
            }
            else
            {
                _suppressCheck = true;
                e.Item.Checked = !e.Item.Checked;
                _suppressCheck = false;
            }
        }

        private RuleConfig GetSelectedRule()
        {
            if (_ruleList.SelectedItems.Count == 0) return null;
            return _ruleList.SelectedItems[0].Tag as RuleConfig;
        }

        private void AddRule()
        {
            RuleConfig rule = new RuleConfig();
            rule.Name = "新规则";
            using (RuleEditForm dialog = new RuleEditForm(rule))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _config.Rules.Add(rule);
                    if (SaveAll(false))
                    {
                        RefreshRuleList();
                        AppendLog("已添加规则：" + rule.Name);
                    }
                    else
                    {
                        _config.Rules.Remove(rule);
                    }
                }
            }
        }

        private void EditRule()
        {
            RuleConfig rule = GetSelectedRule();
            if (rule == null)
            {
                MessageBox.Show(this, "请先选中一条规则。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RuleConfig backup = rule.Clone();
            using (RuleEditForm dialog = new RuleEditForm(rule))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (SaveAll(false))
                    {
                        RefreshRuleList();
                        AppendLog("已修改规则：" + rule.Name);
                    }
                    else
                    {
                        RestoreRule(rule, backup);
                    }
                }
                else
                {
                    RestoreRule(rule, backup);
                }
            }
        }

        private static void RestoreRule(RuleConfig target, RuleConfig backup)
        {
            target.Enabled = backup.Enabled;
            target.Name = backup.Name;
            target.Trigger = backup.Trigger.Clone();
            target.Actions.Clear();
            foreach (ActionConfig action in backup.Actions)
            {
                target.Actions.Add(action.Clone());
            }
        }

        private void CopyRule()
        {
            RuleConfig source = GetSelectedRule();
            if (source == null) return;

            RuleConfig copy = source.Clone();
            copy.Name = source.Name + " - 副本";
            copy.Enabled = false;
            _config.Rules.Add(copy);

            if (SaveAll(false))
            {
                RefreshRuleList();
                AppendLog("已复制规则：" + copy.Name);
            }
            else
            {
                _config.Rules.Remove(copy);
            }
        }

        private void RemoveRule()
        {
            RuleConfig rule = GetSelectedRule();
            if (rule == null) return;

            DialogResult confirm = MessageBox.Show(this,
                "确定删除规则“" + rule.Name + "”吗？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            int index = _config.Rules.IndexOf(rule);
            _config.Rules.Remove(rule);
            if (SaveAll(false))
            {
                RefreshRuleList();
                AppendLog("已删除规则：" + rule.Name);
            }
            else if (index >= 0)
            {
                _config.Rules.Insert(index, rule);
            }
        }

        private void TestRule()
        {
            RuleConfig rule = GetSelectedRule();
            if (rule == null)
            {
                MessageBox.Show(this, "请先选中一条规则。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            AppendLog("手动测试规则：" + rule.Name);
            _executor.Execute(rule, 0, "手动测试");
        }

        private void OpenSettings()
        {
            using (SettingsForm dialog = new SettingsForm(_config))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (SaveAll(false))
                    {
                        if (!_paused) _engine.Reload(_config);
                        AppendLog("设置已保存");
                    }
                }
            }
        }

        // ---------- 引擎回调 ----------

        private void OnRuleTriggered(object sender, RuleTriggeredEventArgs e)
        {
            _executor.Execute(e.Rule, e.SourcePid, e.SourceName);
            AppendLogSafe(string.Format("触发规则：{0}（{1}）", e.Rule.Name, e.SourceName));
        }

        private void OnActionLogged(object sender, string message)
        {
            AppendLogSafe(message);
        }

        private void OnEngineStatusChanged(object sender, EventArgs e)
        {
            string status = _engine.StatusText;
            RunOnUi(delegate { _statusLabel.Text = status; });
        }

        private void OnTogglePause(object sender, EventArgs e)
        {
            _paused = !_paused;
            if (_paused)
            {
                _engine.Stop();
                _pauseItem.Text = "恢复监听";
                _pauseButton.Text = "恢复监听";
                AppendLog("已暂停监听");
            }
            else
            {
                _engine.Start(_config);
                _pauseItem.Text = "暂停监听";
                _pauseButton.Text = "暂停监听";
                AppendLog("已恢复监听");
            }
            RunOnUi(delegate
            {
                _statusLabel.Text = _paused ? "已暂停" : _engine.StatusText;
                _statusLabel.BackColor = _paused ? UiTheme.DangerSoft : UiTheme.PrimarySoft;
                _statusLabel.ForeColor = _paused ? UiTheme.Danger : UiTheme.Primary;
            });
        }

        // ---------- 保存与日志 ----------

        private bool SaveAll(bool silent)
        {
            ValidationResult result = ConfigService.Validate(_config);
            if (!result.IsValid)
            {
                if (!silent)
                {
                    MessageBox.Show(this,
                        "配置校验未通过：" + Environment.NewLine + result.ToMessage(),
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            try
            {
                ConfigService.Save(_config);
                if (!_paused) _engine.Reload(_config);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("保存配置失败", ex);
                if (!silent)
                {
                    MessageBox.Show(this, "保存配置失败：" + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return false;
            }
        }

        private void AppendLogSafe(string message)
        {
            RunOnUi(delegate { AppendLog(message); });
        }

        private void AppendLog(string message)
        {
            if (_logBox == null || _logBox.IsDisposed) return;

            if (_logBox.TextLength > 200000)
            {
                _logBox.Clear();
            }

            _logBox.AppendText(string.Format("[{0:HH:mm:ss}] {1}{2}",
                DateTime.Now, message, Environment.NewLine));
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        private void RunOnUi(MethodInvoker action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch
            {
            }
        }

        private void UpdateRuleColumns()
        {
            if (_ruleList == null || _ruleList.Columns.Count < 4) return;
            int width = _ruleList.ClientSize.Width - 60 - 240 - 240;
            _ruleList.Columns[3].Width = Math.Max(200, width - 12);
        }

        private void CreateDesktopShortcut()
        {
            string message;
            if (ShortcutService.CreateDesktopShortcut(out message))
            {
                AppendLog("已创建桌面快捷方式：" + message);
                MessageBox.Show(this, "桌面快捷方式已创建：" + Environment.NewLine + message,
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AppendLog("创建桌面快捷方式失败：" + message);
                MessageBox.Show(this, "创建桌面快捷方式失败：" + Environment.NewLine + message,
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                AppPaths.EnsureDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                Log.Warn("打开目录失败：" + ex.Message);
            }
        }

        // ---------- 窗体行为 ----------

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 控件句柄创建完成后再初始化，避免 ListView 复选框状态回放触发误保存
            InitializeServices();
            RefreshRuleList();

            _statusLabel.Text = _engine.StatusText;

            if (!TriggerEngine.IsAdministrator && !_adminWarned)
            {
                _adminWarned = true;
                AppendLog("未以管理员身份运行：WMI 进程事件不可用，已依赖轮询兜底。");
                MessageBox.Show(this,
                    "当前未以管理员身份运行。" + Environment.NewLine + Environment.NewLine +
                    "WMI 进程启动事件需要管理员权限，程序将降级为轮询模式，触发存在数秒延迟。" +
                    Environment.NewLine + "如需实时触发，请右键以管理员身份运行。",
                    "权限提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && _config.Settings.MinimizeToTray)
            {
                Hide();
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_exiting) return;

            if (_config.Settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!_trayTipShown)
                {
                    _trayTipShown = true;
                    _notifyIcon.ShowBalloonTip(3000, "AutoFlow",
                        "程序仍在后台监听，双击托盘图标可恢复窗口。", ToolTipIcon.Info);
                }
                return;
            }

            Shutdown();
        }

        private void ExitApplication()
        {
            _exiting = true;
            Shutdown();
            Close();
        }

        private void Shutdown()
        {
            try
            {
                if (_engine != null) _engine.Dispose();
            }
            catch { }

            try
            {
                if (_executor != null) _executor.Dispose();
            }
            catch { }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            Log.Info("程序退出");
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }
    }
}
