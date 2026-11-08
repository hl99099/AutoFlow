using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoFlow
{
    /// <summary>全局设置对话框。</summary>
    public class SettingsForm : Form
    {
        private readonly AppConfig _config;

        private CheckBox _pollBox;
        private NumericUpDown _pollIntervalBox;
        private CheckBox _trayBox;
        private NumericUpDown _retainBox;
        private CheckBox _autoStartBox;

        public bool AutoStartChanged { get; private set; }

        public SettingsForm(AppConfig config)
        {
            _config = config ?? new AppConfig();
            Icon = AppIcon.Value;
            InitializeUi();
            LoadSettings();
        }

        private void InitializeUi()
        {
            Text = "设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 374);
            BackColor = UiTheme.Window;
            Font = UiTheme.BaseFont;
            Icon = AppIcon.Value;

            Panel monitorCard = UiTheme.CreateCard();
            monitorCard.Location = new Point(16, 16);
            monitorCard.Size = new Size(488, 108);

            Label monitorTitle = UiTheme.CreateSectionTitle("监听");
            monitorTitle.Location = new Point(18, 14);

            _pollBox = new CheckBox();
            _pollBox.Text = "启用轮询兜底（WMI 事件不可用时仍能生效）";
            _pollBox.Location = new Point(18, 44);
            _pollBox.Size = new Size(440, 24);

            Label intervalLabel = new Label();
            intervalLabel.Text = "轮询间隔";
            intervalLabel.Location = new Point(18, 78);
            intervalLabel.Size = new Size(76, 20);

            _pollIntervalBox = new NumericUpDown();
            _pollIntervalBox.Location = new Point(98, 74);
            _pollIntervalBox.Size = new Size(90, 25);
            _pollIntervalBox.Minimum = 1;
            _pollIntervalBox.Maximum = 60;
            _pollIntervalBox.TextAlign = HorizontalAlignment.Right;

            Label intervalUnit = new Label();
            intervalUnit.Text = "秒";
            intervalUnit.Location = new Point(194, 78);
            intervalUnit.Size = new Size(30, 20);

            monitorCard.Controls.Add(monitorTitle);
            monitorCard.Controls.Add(_pollBox);
            monitorCard.Controls.Add(intervalLabel);
            monitorCard.Controls.Add(_pollIntervalBox);
            monitorCard.Controls.Add(intervalUnit);

            Panel generalCard = UiTheme.CreateCard();
            generalCard.Location = new Point(16, 136);
            generalCard.Size = new Size(488, 140);

            Label generalTitle = UiTheme.CreateSectionTitle("常规");
            generalTitle.Location = new Point(18, 14);

            _trayBox = new CheckBox();
            _trayBox.Text = "关闭窗口时最小化到托盘";
            _trayBox.Location = new Point(18, 44);
            _trayBox.Size = new Size(440, 24);

            _autoStartBox = new CheckBox();
            _autoStartBox.Text = "开机自动启动（通过计划任务，登录时静默运行）";
            _autoStartBox.Location = new Point(18, 74);
            _autoStartBox.Size = new Size(440, 24);

            Label retainLabel = new Label();
            retainLabel.Text = "日志保留";
            retainLabel.Location = new Point(18, 108);
            retainLabel.Size = new Size(76, 20);

            _retainBox = new NumericUpDown();
            _retainBox.Location = new Point(98, 104);
            _retainBox.Size = new Size(90, 25);
            _retainBox.Minimum = 1;
            _retainBox.Maximum = 365;
            _retainBox.TextAlign = HorizontalAlignment.Right;

            Label retainUnit = new Label();
            retainUnit.Text = "天";
            retainUnit.Location = new Point(194, 108);
            retainUnit.Size = new Size(30, 20);

            generalCard.Controls.Add(generalTitle);
            generalCard.Controls.Add(_trayBox);
            generalCard.Controls.Add(_autoStartBox);
            generalCard.Controls.Add(retainLabel);
            generalCard.Controls.Add(_retainBox);
            generalCard.Controls.Add(retainUnit);

            Label pathLabel = new Label();
            pathLabel.Text = "配置文件：" + AppPaths.ConfigPath;
            pathLabel.Location = new Point(18, 288);
            pathLabel.Size = new Size(484, 20);
            pathLabel.ForeColor = UiTheme.Muted;
            pathLabel.AutoEllipsis = true;

            Button okButton = UiTheme.CreateButton("确定", true);
            okButton.Location = new Point(336, 322);
            okButton.Size = new Size(84, 34);
            okButton.Click += OnOk;

            Button cancelButton = UiTheme.CreateButton("取消", false);
            cancelButton.Location = new Point(428, 322);
            cancelButton.Size = new Size(76, 34);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(monitorCard);
            Controls.Add(generalCard);
            Controls.Add(pathLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadSettings()
        {
            SettingsConfig s = _config.Settings;
            _pollBox.Checked = s.PollFallback;
            _pollIntervalBox.Value = Clamp(s.PollIntervalMs / 1000, 1, 60);
            _trayBox.Checked = s.MinimizeToTray;
            _retainBox.Value = Clamp(s.LogRetainDays, 1, 365);
            _autoStartBox.Checked = AutoStart.IsEnabled();
        }

        private static decimal Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void OnOk(object sender, EventArgs e)
        {
            SettingsConfig s = _config.Settings;
            s.PollFallback = _pollBox.Checked;
            s.PollIntervalMs = (int)_pollIntervalBox.Value * 1000;
            s.MinimizeToTray = _trayBox.Checked;
            s.LogRetainDays = (int)_retainBox.Value;

            bool wantAutoStart = _autoStartBox.Checked;
            bool hasAutoStart = AutoStart.IsEnabled();
            if (wantAutoStart != hasAutoStart)
            {
                string message;
                bool ok = wantAutoStart ? AutoStart.Enable(out message) : AutoStart.Disable(out message);
                if (!ok)
                {
                    MessageBox.Show(this,
                        "设置开机自启失败：" + Environment.NewLine + message,
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    AutoStartChanged = true;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
