using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace AutoFlow
{
    /// <summary>进程查找、启动、关闭的底层封装。</summary>
    public static class ProcessUtil
    {
        /// <summary>按进程名查找（不区分大小写，自动去除 .exe 后缀）</summary>
        public static List<Process> FindByName(string processName)
        {
            List<Process> result = new List<Process>();
            string bare = ToBareName(processName);
            if (bare.Length == 0) return result;

            try
            {
                foreach (Process p in Process.GetProcessesByName(bare))
                {
                    result.Add(p);
                }
            }
            catch (Exception ex)
            {
                Log.Error("枚举进程失败：" + processName, ex);
            }
            return result;
        }

        public static bool IsRunning(string processName)
        {
            List<Process> list = FindByName(processName);
            bool running = list.Count > 0;
            foreach (Process p in list)
            {
                try { p.Dispose(); }
                catch { }
            }
            return running;
        }

        public static bool IsPidAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    return !p.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 通过 WMI 获取进程完整路径。进程可能刚启动或已退出，内部做少量重试。
        /// 失败返回空字符串。
        /// </summary>
        public static string GetProcessPath(int pid, int retries)
        {
            for (int i = 0; i <= retries; i++)
            {
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                        "SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = " + pid))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            object value = obj["ExecutablePath"];
                            if (value != null)
                            {
                                string path = value.ToString();
                                if (!string.IsNullOrEmpty(path)) return path;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("获取进程路径失败 PID=" + pid + "：" + ex.Message);
                }

                if (i < retries) System.Threading.Thread.Sleep(120);
            }
            return string.Empty;
        }

        /// <summary>
        /// 启动目标程序。返回启动的 Process（bat/cmd 经过 cmd.exe 转发，PID 仅供参考），失败返回 null。
        /// </summary>
        public static Process Launch(string path, string args, string workingDir, bool hidden, bool runAsAdmin)
        {
            if (string.IsNullOrEmpty(path))
            {
                Log.Warn("启动动作缺少路径，已跳过");
                return null;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Log.Error("目标程序不存在：" + path);
                return null;
            }

            string dir = string.IsNullOrEmpty(workingDir) ? Path.GetDirectoryName(path) : workingDir;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            bool isScript = ext == ".bat" || ext == ".cmd";

            ProcessStartInfo psi = new ProcessStartInfo();
            if (isScript)
            {
                // .bat/.cmd 必须通过 cmd.exe 执行
                psi.FileName = Environment.GetEnvironmentVariable("COMSPEC");
                if (string.IsNullOrEmpty(psi.FileName)) psi.FileName = "cmd.exe";
                psi.Arguments = string.Format("/c \"{0}\"{1}", path, string.IsNullOrEmpty(args) ? "" : " " + args);
            }
            else
            {
                psi.FileName = path;
                psi.Arguments = args ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;

            if (runAsAdmin)
            {
                // 提权必须走 ShellExecute，此路径下无法隐藏窗口
                psi.UseShellExecute = true;
                psi.Verb = "runas";
            }
            else
            {
                psi.UseShellExecute = false;
                if (hidden)
                {
                    psi.CreateNoWindow = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                }
            }

            try
            {
                Process p = Process.Start(psi);
                Log.Info("已启动：" + path + (string.IsNullOrEmpty(args) ? "" : " " + args));
                return p;
            }
            catch (Exception ex)
            {
                Log.Error("启动失败：" + path, ex);
                return null;
            }
        }

        /// <summary>
        /// 关闭同名进程。force=false 时先尝试优雅关闭，超时后强杀。
        /// 返回处理掉的进程数量。
        /// </summary>
        public static int CloseByName(string processName, bool force, bool killTree, int gracefulTimeoutMs)
        {
            List<Process> targets = FindByName(processName);
            if (targets.Count == 0)
            {
                Log.Info("关闭动作跳过，进程未运行：" + processName);
                return 0;
            }

            int closed = 0;
            foreach (Process p in targets)
            {
                try
                {
                    if (killTree)
                    {
                        KillTreeByPid(p.Id);
                        closed++;
                        continue;
                    }

                    if (!force)
                    {
                        try
                        {
                            if (p.CloseMainWindow())
                            {
                                if (p.WaitForExit(gracefulTimeoutMs))
                                {
                                    closed++;
                                    continue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug("优雅关闭失败，转为强杀：" + ex.Message);
                        }
                    }

                    p.Kill();
                    closed++;
                }
                catch (Exception ex)
                {
                    Log.Warn("关闭进程失败 PID=" + SafeId(p) + "：" + ex.Message);
                }
                finally
                {
                    try { p.Dispose(); }
                    catch { }
                }
            }

            Log.Info(string.Format("已关闭 {0} 个进程：{1}", closed, processName));
            return closed;
        }

        private static void KillTreeByPid(int pid)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "taskkill.exe";
                psi.Arguments = string.Format("/PID {0} /T /F", pid);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process killer = Process.Start(psi))
                {
                    if (killer != null) killer.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("结束进程树失败 PID=" + pid + "：" + ex.Message);
            }
        }

        private static int SafeId(Process p)
        {
            try { return p.Id; }
            catch { return -1; }
        }

        public static string ToBareName(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return string.Empty;
            string name = processName.Trim();
            if (name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
            {
                name = Path.GetFileName(name);
            }
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }
            return name;
        }
    }
}
