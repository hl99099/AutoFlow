using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoFlow
{
    /// <summary>规则编辑对话框：触发条件 + 多个动作。</summary>
    public class RuleEditForm : Form
    {
        private readonly RuleConfig _rule;

        private TextBox _nameBox;
        private ComboBox _triggerTypeBox;
        private TextBox _processBox;
        private TextBox _filePathBox;
        private ListView _actionList;

        public RuleEditForm(RuleConfig rule)
        {
            _rule = rule ?? new RuleConfig();
            Icon = AppIcon.Value;
            InitializeUi();
            LoadRule();
        }

        public RuleConfig Result
        {
            get { return _rule; }
        }

        private void InitializeUi()
        {
            Text = "编辑规则";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 626);
            BackColor = UiTheme.Window;
            Font = UiTheme.BaseFont;
            Icon = AppIcon.Value;

            Panel triggerCard = UiTheme.CreateCard();
            triggerCard.Location = new Point(20, 16);
            triggerCard.Size = new Size(600, 276);

            Label triggerTitle = UiTheme.CreateSectionTitle("触发条件");
            triggerTitle.Location = new Point(18, 14);

            Label nameLabel = MakeLabel("规则名称", 18, 50, 70);
            _nameBox = new TextBox();
            _nameBox.Location = new Point(96, 46);
            _nameBox.Size = new Size(486, 26);

            Label typeLabel = MakeLabel("触发时机", 18, 88, 70);
            _triggerTypeBox = new ComboBox();
            _triggerTypeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _triggerTypeBox.Location = new Point(96, 84);
            _triggerTypeBox.Size = new Size(180, 26);
            _triggerTypeBox.Items.Add("程序启动时");
            _triggerTypeBox.Items.Add("程序关闭时");

            Label processLabel = MakeLabel("进程名", 18, 126, 70);
            _processBox = new TextBox();
            _processBox.Location = new Point(96, 122);
            _processBox.Size = new Size(300, 26);

            Button pickProcess = UiTheme.CreateButton("选择...", false);
            pickProcess.Location = new Point(406, 120);
            pickProcess.Size = new Size(80, 30);
            pickProcess.Click += delegate { PickProcess(); };

            Label processHint = UiTheme.CreateHint(UiTheme.ProcessNameHint, 96, 152, 486, 70);

            Label pathLabel = MakeLabel("路径校验", 18, 234, 70);
            _filePathBox = new TextBox();
            _filePathBox.Location = new Point(96, 230);
            _filePathBox.Size = new Size(300, 26);

            Button pickPath = UiTheme.CreateButton("浏览...", false);
            pickPath.Location = new Point(406, 228);
            pickPath.Size = new Size(80, 30);
            pickPath.Click += delegate { PickExactPath(); };

            triggerCard.Controls.Add(nameLabel);
            triggerCard.Controls.Add(_nameBox);
            triggerCard.Controls.Add(typeLabel);
            triggerCard.Controls.Add(_triggerTypeBox);
            triggerCard.Controls.Add(processLabel);
            triggerCard.Controls.Add(_processBox);
            triggerCard.Controls.Add(pickProcess);
            triggerCard.Controls.Add(processHint);
            triggerCard.Controls.Add(pathLabel);
            triggerCard.Controls.Add(_filePathBox);
            triggerCard.Controls.Add(pickPath);
            triggerCard.Controls.Add(triggerTitle);

            Panel actionCard = UiTheme.CreateCard();
            actionCard.Location = new Point(20, 304);
            actionCard.Size = new Size(600, 260);

            Label actionTitle = UiTheme.CreateSectionTitle("执行动作");
            actionTitle.Location = new Point(18, 14);

            _actionList = new ListView();
            _actionList.Location = new Point(18, 44);
            _actionList.Size = new Size(430, 170);
            _actionList.View = View.Details;
            _actionList.FullRowSelect = true;
            _actionList.MultiSelect = false;
            _actionList.HideSelection = false;
            _actionList.BorderStyle = BorderStyle.None;
            _actionList.BackColor = UiTheme.Surface;
            _actionList.ForeColor = UiTheme.Text;
            _actionList.Font = UiTheme.ListFont;
            _actionList.Columns.Add("类型", 90);
            _actionList.Columns.Add("目标", 240);
            _actionList.Columns.Add("延迟", 90);
            _actionList.DoubleClick += delegate { EditAction(); };

            Button addAction = UiTheme.CreateButton("添加", true);
            addAction.Location = new Point(462, 44);
            addAction.Size = new Size(122, 34);
            addAction.Click += delegate { AddAction(); };

            Button editAction = UiTheme.CreateButton("编辑", false);
            editAction.Location = new Point(462, 88);
            editAction.Size = new Size(122, 34);
            editAction.Click += delegate { EditAction(); };

            Button removeAction = UiTheme.CreateButton("删除", false);
            removeAction.Location = new Point(462, 132);
            removeAction.Size = new Size(122, 34);
            removeAction.Click += delegate { RemoveAction(); };

            actionCard.Controls.Add(_actionList);
            actionCard.Controls.Add(addAction);
            actionCard.Controls.Add(editAction);
            actionCard.Controls.Add(removeAction);
            actionCard.Controls.Add(actionTitle);

            Button okButton = UiTheme.CreateButton("确定", true);
            okButton.Location = new Point(536, 580);
            okButton.Size = new Size(84, 34);
            okButton.Click += OnOk;

            Button cancelButton = UiTheme.CreateButton("取消", false);
            cancelButton.Location = new Point(444, 580);
            cancelButton.Size = new Size(80, 34);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(triggerCard);
            Controls.Add(actionCard);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private static Label MakeLabel(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 20);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private void LoadRule()
        {
            _nameBox.Text = _rule.Name;
            _triggerTypeBox.SelectedIndex = _rule.Trigger != null && _rule.Trigger.Type == TriggerTypes.Stop ? 1 : 0;
            _processBox.Text = _rule.Trigger == null ? string.Empty : _rule.Trigger.ProcessName;
            _filePathBox.Text = _rule.Trigger == null ? string.Empty : _rule.Trigger.FilePath;
            RefreshActionList();
        }

        private void RefreshActionList()
        {
            _actionList.BeginUpdate();
            _actionList.Items.Clear();
            foreach (ActionConfig action in _rule.Actions)
            {
                ListViewItem item = new ListViewItem(action.Type == ActionTypes.Close ? "关闭程序" : "启动程序");
                item.SubItems.Add(action.DisplayName);
                item.SubItems.Add(action.DelayMs > 0
                    ? (action.DelayMs / 1000.0).ToString("0.#") + " 秒"
                    : "立即");
                item.Tag = action;
                _actionList.Items.Add(item);
            }
            _actionList.EndUpdate();
        }

        private void PickProcess()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要监视的程序";
                dialog.Filter = "程序|*.exe|所有文件|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _processBox.Text = Path.GetFileName(dialog.FileName);
                }
            }
        }

        private void PickExactPath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择用于路径校验的程序";
                dialog.Filter = "程序|*.exe|所有文件|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _filePathBox.Text = dialog.FileName;
                    if (string.IsNullOrEmpty(_processBox.Text.Trim()))
                    {
                        _processBox.Text = Path.GetFileName(dialog.FileName);
                    }
                }
            }
        }

        private void AddAction()
        {
            ActionConfig action = new ActionConfig();
            using (ActionEditForm dialog = new ActionEditForm(action))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _rule.Actions.Add(action);
                    RefreshActionList();
                }
            }
        }

        private void EditAction()
        {
            if (_actionList.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "请先选中一个动作。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ActionConfig action = _actionList.SelectedItems[0].Tag as ActionConfig;
            if (action == null) return;

            using (ActionEditForm dialog = new ActionEditForm(action))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshActionList();
                }
            }
        }

        private void RemoveAction()
        {
            if (_actionList.SelectedItems.Count == 0) return;
            ActionConfig action = _actionList.SelectedItems[0].Tag as ActionConfig;
            if (action == null) return;

            _rule.Actions.Remove(action);
            RefreshActionList();
        }

        private void OnOk(object sender, EventArgs e)
        {
            string name = _nameBox.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "请填写规则名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string processName = ConfigService.NormalizeProcessName(_processBox.Text);
            if (processName.Length == 0)
            {
                MessageBox.Show(this, "请填写要监视的进程名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_rule.Actions.Count == 0)
            {
                MessageBox.Show(this, "请至少添加一个动作。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string filePath = _filePathBox.Text.Trim();
            if (filePath.Length > 0 && !File.Exists(filePath))
            {
                MessageBox.Show(this, "路径校验填写的文件不存在，请重新选择或清空该项。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _rule.Name = name;
            _rule.Trigger.Type = _triggerTypeBox.SelectedIndex == 1 ? TriggerTypes.Stop : TriggerTypes.Start;
            _rule.Trigger.ProcessName = processName;
            _rule.Trigger.FilePath = filePath;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
