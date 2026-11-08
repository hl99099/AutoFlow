using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoFlow
{
    /// <summary>单个动作的编辑对话框。</summary>
    public class ActionEditForm : Form
    {
        private readonly ActionConfig _action;

        private ComboBox _typeBox;
        private Panel _launchPanel;
        private Panel _closePanel;

        private TextBox _pathBox;
        private TextBox _argsBox;
        private TextBox _workDirBox;
        private NumericUpDown _delayBox;
        private CheckBox _hiddenBox;
        private CheckBox _adminBox;
        private CheckBox _skipBox;
        private CheckBox _cancelBox;

        private TextBox _targetBox;
        private CheckBox _forceBox;
        private CheckBox _killTreeBox;

        public ActionEditForm(ActionConfig action)
        {
            _action = action ?? new ActionConfig();
            Icon = AppIcon.Value;
            InitializeUi();
            LoadAction();
        }

        private void InitializeUi()
        {
            Text = "编辑动作";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 452);
            BackColor = UiTheme.Window;
            Font = UiTheme.BaseFont;

            Label typeLabel = MakeLabel("动作类型", 16, 19);
            _typeBox = new ComboBox();
            _typeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _typeBox.Location = new Point(90, 15);
            _typeBox.Size = new Size(180, 25);
            _typeBox.Items.Add("启动程序");
            _typeBox.Items.Add("关闭程序");
            _typeBox.SelectedIndexChanged += delegate { UpdatePanels(); };

            _launchPanel = BuildLaunchPanel();
            _closePanel = BuildClosePanel();

            Button okButton = UiTheme.CreateButton("确定", true);
            okButton.Location = new Point(330, 404);
            okButton.Size = new Size(84, 34);
            okButton.Click += OnOk;

            Button cancelButton = UiTheme.CreateButton("取消", false);
            cancelButton.Location = new Point(422, 404);
            cancelButton.Size = new Size(80, 34);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(typeLabel);
            Controls.Add(_typeBox);
            Controls.Add(_launchPanel);
            Controls.Add(_closePanel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private Panel BuildLaunchPanel()
        {
            Panel panel = UiTheme.CreateCard();
            panel.Location = new Point(16, 52);
            panel.Size = new Size(488, 330);

            Label pathLabel = MakeLabel("程序路径", 0, 12);
            _pathBox = new TextBox();
            _pathBox.Location = new Point(72, 8);
            _pathBox.Size = new Size(330, 25);

            Button browse = UiTheme.CreateButton("浏览...", false);
            browse.Location = new Point(410, 6);
            browse.Size = new Size(72, 30);
            browse.Click += delegate { BrowseProgram(); };

            Label argsLabel = MakeLabel("启动参数", 0, 50);
            _argsBox = new TextBox();
            _argsBox.Location = new Point(72, 46);
            _argsBox.Size = new Size(410, 25);

            Label dirLabel = MakeLabel("工作目录", 0, 88);
            _workDirBox = new TextBox();
            _workDirBox.Location = new Point(72, 84);
            _workDirBox.Size = new Size(330, 25);

            Button browseDir = UiTheme.CreateButton("浏览...", false);
            browseDir.Location = new Point(410, 82);
            browseDir.Size = new Size(72, 30);
            browseDir.Click += delegate { BrowseFolder(); };

            Label delayLabel = MakeLabel("启动延迟", 0, 126);
            _delayBox = new NumericUpDown();
            _delayBox.Location = new Point(72, 122);
            _delayBox.Size = new Size(100, 25);
            _delayBox.Minimum = 0;
            _delayBox.Maximum = 3600;
            _delayBox.TextAlign = HorizontalAlignment.Right;

            Label delayHint = new Label();
            delayHint.Text = "秒（0 表示立即启动）";
            delayHint.Location = new Point(180, 126);
            delayHint.Size = new Size(200, 20);
            delayHint.ForeColor = UiTheme.Muted;

            _hiddenBox = new CheckBox();
            _hiddenBox.Text = "隐藏窗口（bat 不弹黑框）";
            _hiddenBox.Location = new Point(72, 158);
            _hiddenBox.Size = new Size(300, 24);

            _adminBox = new CheckBox();
            _adminBox.Text = "以管理员身份运行（会弹 UAC）";
            _adminBox.Location = new Point(72, 186);
            _adminBox.Size = new Size(300, 24);

            _skipBox = new CheckBox();
            _skipBox.Text = "目标已在运行时跳过启动";
            _skipBox.Location = new Point(72, 214);
            _skipBox.Size = new Size(300, 24);

            _cancelBox = new CheckBox();
            _cancelBox.Text = "延迟结束时，源程序已退出则取消启动";
            _cancelBox.Location = new Point(72, 242);
            _cancelBox.Size = new Size(340, 24);

            panel.Controls.Add(pathLabel);
            panel.Controls.Add(_pathBox);
            panel.Controls.Add(browse);
            panel.Controls.Add(argsLabel);
            panel.Controls.Add(_argsBox);
            panel.Controls.Add(dirLabel);
            panel.Controls.Add(_workDirBox);
            panel.Controls.Add(browseDir);
            panel.Controls.Add(delayLabel);
            panel.Controls.Add(_delayBox);
            panel.Controls.Add(delayHint);
            panel.Controls.Add(_hiddenBox);
            panel.Controls.Add(_adminBox);
            panel.Controls.Add(_skipBox);
            panel.Controls.Add(_cancelBox);
            return panel;
        }

        private Panel BuildClosePanel()
        {
            Panel panel = UiTheme.CreateCard();
            panel.Location = new Point(16, 52);
            panel.Size = new Size(488, 330);

            Label nameLabel = MakeLabel("进程名", 0, 12);
            _targetBox = new TextBox();
            _targetBox.Location = new Point(72, 8);
            _targetBox.Size = new Size(280, 25);

            Label hint = UiTheme.CreateHint(UiTheme.ProcessNameHint, 72, 38, 410, 78);

            _forceBox = new CheckBox();
            _forceBox.Text = "直接强制结束（不尝试正常关闭）";
            _forceBox.Location = new Point(72, 118);
            _forceBox.Size = new Size(340, 24);

            _killTreeBox = new CheckBox();
            _killTreeBox.Text = "同时结束该程序的所有子进程";
            _killTreeBox.Location = new Point(72, 146);
            _killTreeBox.Size = new Size(340, 24);

            panel.Controls.Add(nameLabel);
            panel.Controls.Add(_targetBox);
            panel.Controls.Add(hint);
            panel.Controls.Add(_forceBox);
            panel.Controls.Add(_killTreeBox);
            return panel;
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(68, 20);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private void LoadAction()
        {
            _pathBox.Text = _action.Path;
            _argsBox.Text = _action.Args;
            _workDirBox.Text = _action.WorkingDir;
            _delayBox.Value = ClampSeconds(_action.DelayMs / 1000);
            _hiddenBox.Checked = _action.RunHidden;
            _adminBox.Checked = _action.RunAsAdmin;
            _skipBox.Checked = _action.SkipIfRunning;
            _cancelBox.Checked = _action.CancelIfSourceExited;
            _targetBox.Text = _action.TargetProcessName;
            _forceBox.Checked = _action.Force;
            _killTreeBox.Checked = _action.KillTree;

            if (_action.Type == ActionTypes.Close)
            {
                _typeBox.SelectedIndex = 1;
            }
            else
            {
                _typeBox.SelectedIndex = 0;
            }
            UpdatePanels();
        }

        private static decimal ClampSeconds(int seconds)
        {
            if (seconds < 0) return 0;
            if (seconds > 3600) return 3600;
            return seconds;
        }

        private void UpdatePanels()
        {
            bool isLaunch = _typeBox.SelectedIndex == 0;
            _launchPanel.Visible = isLaunch;
            _closePanel.Visible = !isLaunch;
        }

        private void BrowseProgram()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要启动的程序";
                dialog.Filter = "程序或脚本|*.exe;*.bat;*.cmd;*.com|所有文件|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _pathBox.Text = dialog.FileName;
                    if (string.IsNullOrEmpty(_workDirBox.Text))
                    {
                        _workDirBox.Text = System.IO.Path.GetDirectoryName(dialog.FileName);
                    }
                }
            }
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择工作目录";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _workDirBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OnOk(object sender, EventArgs e)
        {
            bool isLaunch = _typeBox.SelectedIndex == 0;

            if (isLaunch)
            {
                if (string.IsNullOrEmpty(_pathBox.Text.Trim()))
                {
                    MessageBox.Show(this, "请填写要启动的程序路径。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _action.Type = ActionTypes.Launch;
                _action.Path = _pathBox.Text.Trim();
                _action.Args = _argsBox.Text.Trim();
                _action.WorkingDir = _workDirBox.Text.Trim();
                _action.DelayMs = (int)_delayBox.Value * 1000;
                _action.RunHidden = _hiddenBox.Checked;
                _action.RunAsAdmin = _adminBox.Checked;
                _action.SkipIfRunning = _skipBox.Checked;
                _action.CancelIfSourceExited = _cancelBox.Checked;
            }
            else
            {
                string target = ConfigService.NormalizeProcessName(_targetBox.Text);
                if (string.IsNullOrEmpty(target))
                {
                    MessageBox.Show(this, "请填写要关闭的进程名。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _action.Type = ActionTypes.Close;
                _action.TargetProcessName = target;
                _action.Force = _forceBox.Checked;
                _action.KillTree = _killTreeBox.Checked;
                _action.DelayMs = 0;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
