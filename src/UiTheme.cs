using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoFlow
{
    /// <summary>统一的现代化配色、字体与控件样式。</summary>
    internal static class UiTheme
    {
        public static readonly Color Window = Color.FromArgb(243, 246, 250);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color Primary = Color.FromArgb(37, 99, 235);
        public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
        public static readonly Color PrimaryPressed = Color.FromArgb(30, 64, 175);
        public static readonly Color PrimarySoft = Color.FromArgb(239, 246, 255);
        public static readonly Color Text = Color.FromArgb(15, 23, 42);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
        public static readonly Color LogBackground = Color.FromArgb(15, 23, 42);
        public static readonly Color LogText = Color.FromArgb(203, 213, 225);

        public static readonly Font BaseFont = new Font("Microsoft YaHei UI", 9F);
        public static readonly Font TitleFont = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
        public static readonly Font SubtitleFont = new Font("Microsoft YaHei UI", 8.5F);
        public static readonly Font SectionFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        public static readonly Font ListFont = new Font("Microsoft YaHei UI", 9.5F);
        public static readonly Font MonoFont = new Font("Consolas", 9.5F);

        /// <summary>进程名填写说明，规则/动作编辑界面共用。</summary>
        public const string ProcessNameHint =
            "进程名必须与任务管理器中的名称一致，可能与 exe 文件名不同（例如 VS Code 的进程名是 Code.exe）。\r\n"
            + "查看方法：按 Ctrl+Shift+Esc 打开任务管理器 →「详细信息」→ 找到该程序 →「名称」列即为进程名。";

        /// <summary>创建扁平风格按钮，primary 为主操作按钮。</summary>
        public static Button CreateButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = false;
            button.Size = new Size(primary ? 92 : 80, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = primary ? PrimaryHover : Color.FromArgb(248, 250, 252);
            button.FlatAppearance.MouseDownBackColor = primary ? PrimaryPressed : Color.FromArgb(241, 245, 249);
            button.BackColor = primary ? Primary : Surface;
            button.ForeColor = primary ? Color.White : Text;
            button.Font = BaseFont;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        /// <summary>带 1px 边框的白色卡片面板。</summary>
        public static Panel CreateCard()
        {
            Panel panel = new Panel();
            panel.BackColor = Surface;
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                }
            };
            return panel;
        }

        public static Label CreateSectionTitle(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = SectionFont;
            label.ForeColor = Text;
            label.AutoSize = true;
            label.BackColor = Color.Transparent;
            return label;
        }

        /// <summary>卡片内的灰色说明文字。</summary>
        public static Label CreateHint(string text, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Text = text;
            label.Font = SubtitleFont;
            label.ForeColor = Muted;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            return label;
        }

        /// <summary>给工具条/状态栏使用的浅色渲染表。</summary>
        internal sealed class ModernColors : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin { get { return Surface; } }
            public override Color ToolStripGradientMiddle { get { return Surface; } }
            public override Color ToolStripGradientEnd { get { return Surface; } }
            public override Color ToolStripBorder { get { return Border; } }
            public override Color ButtonSelectedGradientBegin { get { return PrimarySoft; } }
            public override Color ButtonSelectedGradientMiddle { get { return PrimarySoft; } }
            public override Color ButtonSelectedGradientEnd { get { return PrimarySoft; } }
            public override Color ButtonSelectedBorder { get { return PrimarySoft; } }
            public override Color ButtonSelectedHighlight { get { return PrimarySoft; } }
            public override Color ButtonPressedGradientBegin { get { return Color.FromArgb(219, 234, 254); } }
            public override Color ButtonPressedGradientMiddle { get { return Color.FromArgb(219, 234, 254); } }
            public override Color ButtonPressedGradientEnd { get { return Color.FromArgb(219, 234, 254); } }
            public override Color ButtonPressedBorder { get { return PrimarySoft; } }
            public override Color ButtonPressedHighlight { get { return PrimarySoft; } }
            public override Color ButtonCheckedGradientBegin { get { return PrimarySoft; } }
            public override Color ButtonCheckedGradientMiddle { get { return PrimarySoft; } }
            public override Color ButtonCheckedGradientEnd { get { return PrimarySoft; } }
            public override Color MenuItemSelected { get { return PrimarySoft; } }
            public override Color MenuItemBorder { get { return PrimarySoft; } }
            public override Color MenuStripGradientBegin { get { return Surface; } }
            public override Color MenuStripGradientEnd { get { return Surface; } }
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color StatusStripGradientBegin { get { return Surface; } }
            public override Color StatusStripGradientEnd { get { return Surface; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Surface; } }
        }
    }
}
