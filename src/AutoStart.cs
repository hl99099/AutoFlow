using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AutoFlow
{
    /// <summary>开机自启：使用计划任务，避免每次登录弹 UAC。</summary>
    public static class AutoStart
    {
        private const string TaskName = "AutoFlow";
        private const string LegacyTaskName = "AutoLauncher";

        public static bool IsEnabled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = "/Query /TN \"" + TaskName + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(8000);
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("查询计划任务失败：" + ex.Message);
                return false;
            }
        }

        public static bool Enable(out string message)
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string args = string.Format("/Create /TN \"{0}\" /TR \"\\\"{1}\\\"\" /SC ONLOGON /RL HIGHEST /F",
                TaskName, exe);
            bool ok = RunSchtasks(args, out message);
            if (ok) RemoveLegacyTask();
            return ok;
        }

        public static bool Disable(out string message)
        {
            return RunSchtasks("/Delete /TN \"" + TaskName + "\" /F", out message);
        }

        /// <summary>删除旧版 AutoLauncher 计划任务，避免升级后重复自启。</summary>
        private static void RemoveLegacyTask()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = "/Delete /TN \"" + LegacyTaskName + "\" /F";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(8000);
                }
            }
            catch
            {
                // 旧任务不存在或删除失败都不影响新任务
            }
        }

        private static bool RunSchtasks(string arguments, out string message)
        {
            message = string.Empty;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                    bool ok = p.ExitCode == 0;
                    message = output.Trim();
                    if (ok)
                    {
                        Log.Info("计划任务操作成功：" + arguments);
                    }
                    else
                    {
                        Log.Warn("计划任务操作失败：" + output.Trim());
                    }
                    return ok;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Log.Error("执行 schtasks 失败", ex);
                return false;
            }
        }
    }
}
