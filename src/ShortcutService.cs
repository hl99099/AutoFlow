using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoFlow
{
    /// <summary>桌面快捷方式相关操作。</summary>
    internal static class ShortcutService
    {
        private const string ShortcutFileName = "AutoFlow.lnk";

        /// <summary>在桌面创建指向当前 exe 的快捷方式，成功时 message 返回 lnk 路径。</summary>
        public static bool CreateDesktopShortcut(out string message)
        {
            message = string.Empty;
            object shell = null;
            object shortcut = null;

            try
            {
                string exe = Application.ExecutablePath;
                string linkPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutFileName);

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    message = "当前系统缺少 WScript.Shell，无法创建快捷方式";
                    return false;
                }

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { linkPath });

                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty,
                    null, shortcut, new object[] { exe });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty,
                    null, shortcut, new object[] { Path.GetDirectoryName(exe) });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty,
                    null, shortcut, new object[] { exe + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty,
                    null, shortcut, new object[] { "AutoFlow - 自动联动启动管理器" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

                message = linkPath;
                return File.Exists(linkPath);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
            finally
            {
                if (shortcut != null)
                {
                    try { Marshal.ReleaseComObject(shortcut); }
                    catch { }
                }
                if (shell != null)
                {
                    try { Marshal.ReleaseComObject(shell); }
                    catch { }
                }
            }
        }
    }
}
