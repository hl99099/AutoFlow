using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Threading;

namespace AutoFlow
{
    public class RuleTriggeredEventArgs : EventArgs
    {
        public RuleConfig Rule { get; set; }
        public int SourcePid { get; set; }
        public string SourceName { get; set; }

        public RuleTriggeredEventArgs(RuleConfig rule, int sourcePid, string sourceName)
        {
            Rule = rule;
            SourcePid = sourcePid;
            SourceName = sourceName;
        }
    }

    /// <summary>
    /// 触发引擎：WMI 进程启动事件为主，轮询为兜底。
    /// 启动类规则在进程出现时触发；关闭类规则通过 PID 绑定 Exited 事件触发。
    /// </summary>
    public class TriggerEngine : IDisposable
    {
        private readonly Func<string, bool> _isSuppressed;
        private readonly object _sync = new object();

        private AppConfig _config;
        private ManagementEventWatcher _watcher;
        private Timer _pollTimer;
        private bool _polling;
        private bool _pollInitialized;
        private bool _disposed;

        private readonly Dictionary<int, List<RuleConfig>> _stopWatch = new Dictionary<int, List<RuleConfig>>();
        private readonly Dictionary<int, Process> _exitHooked = new Dictionary<int, Process>();
        private readonly Dictionary<string, DateTime> _recentFire = new Dictionary<string, DateTime>();
        private readonly Dictionary<int, DateTime> _firstSeen = new Dictionary<int, DateTime>();
        private Dictionary<int, string> _snapshot = new Dictionary<int, string>();

        /// <summary>进程存活时间低于该阈值时，其退出事件不作为“程序关闭”处理</summary>
        private const int MinLifetimeSeconds = 3;

        public event EventHandler<RuleTriggeredEventArgs> RuleTriggered;
        public event EventHandler StatusChanged;

        public bool WmiAvailable { get; private set; }
        public string StatusText { get; private set; }

        public static bool IsAdministrator
        {
            get
            {
                try
                {
                    using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    {
                        WindowsPrincipal principal = new WindowsPrincipal(identity);
                        return principal.IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public TriggerEngine(Func<string, bool> isSuppressed)
        {
            _isSuppressed = isSuppressed;
            StatusText = "未启动";
        }

        public void Start(AppConfig config)
        {
            _config = config ?? new AppConfig();
            StopInternal();

            TryStartWmi();
            StartPolling();
            BindExistingStopRules();

            if (!WmiAvailable && !_config.Settings.PollFallback)
            {
                SetStatus("WMI 不可用且未启用轮询，规则不会生效");
            }
            else if (!WmiAvailable)
            {
                SetStatus("WMI 不可用，已降级为轮询模式");
            }
            else if (_config.Settings.PollFallback)
            {
                SetStatus("运行中（WMI 事件 + 轮询兜底）");
            }
            else
            {
                SetStatus("运行中（WMI 事件）");
            }
        }

        public void Reload(AppConfig config)
        {
            Log.Info("重新加载触发引擎");
            Start(config);
        }

        public void Stop()
        {
            StopInternal();
            SetStatus("已停止");
        }

        private void StopInternal()
        {
            lock (_sync)
            {
                if (_watcher != null)
                {
                    try { _watcher.Stop(); }
                    catch { }
                    try { _watcher.Dispose(); }
                    catch { }
                    _watcher = null;
                }

                if (_pollTimer != null)
                {
                    try { _pollTimer.Dispose(); }
                    catch { }
                    _pollTimer = null;
                }

                foreach (KeyValuePair<int, Process> pair in _exitHooked)
                {
                    try { pair.Value.Dispose(); }
                    catch { }
                }
                _exitHooked.Clear();
                _stopWatch.Clear();
                _firstSeen.Clear();
                _snapshot = new Dictionary<int, string>();
                _pollInitialized = false;
            }
        }

        private void TryStartWmi()
        {
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();

                WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
                _watcher = new ManagementEventWatcher(scope, query);
                _watcher.EventArrived += OnWmiProcessStarted;
                _watcher.Start();
                WmiAvailable = true;
                Log.Info("WMI 进程启动事件订阅成功");
            }
            catch (Exception ex)
            {
                WmiAvailable = false;
                if (!IsAdministrator)
                {
                    Log.Warn("WMI 订阅失败（需要管理员权限）：" + ex.Message);
                }
                else
                {
                    Log.Warn("WMI 订阅失败，将依赖轮询：" + ex.Message);
                }

                if (_watcher != null)
                {
                    try { _watcher.Dispose(); }
                    catch { }
                    _watcher = null;
                }
            }
        }

        private void StartPolling()
        {
            if (_config == null || !_config.Settings.PollFallback) return;

            int interval = _config.Settings.PollIntervalMs;
            if (interval < 500) interval = 500;

            _pollTimer = new Timer(Poll, null, interval, interval);
            Log.Info("已启用轮询兜底，间隔 " + interval + " ms");
        }

        private void OnWmiProcessStarted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                string name = Convert.ToString(e.NewEvent["ProcessName"]);
                int pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
                if (string.IsNullOrEmpty(name)) return;
                HandleStarted(ConfigService.NormalizeProcessName(name), pid);
            }
            catch (Exception ex)
            {
                Log.Error("处理 WMI 启动事件失败", ex);
            }
        }

        private void Poll(object state)
        {
            if (_disposed) return;
            lock (_sync)
            {
                if (_polling) return;
                _polling = true;
            }

            try
            {
                Dictionary<int, string> current = TakeSnapshot();

                if (!_pollInitialized)
                {
                    lock (_sync)
                    {
                        _snapshot = current;
                        _pollInitialized = true;
                    }
                    return;
                }

                Dictionary<int, string> previous;
                lock (_sync)
                {
                    previous = _snapshot;
                    _snapshot = current;
                }

                foreach (KeyValuePair<int, string> pair in current)
                {
                    if (!previous.ContainsKey(pair.Key))
                    {
                        HandleStarted(pair.Value, pair.Key);
                    }
                }

                foreach (KeyValuePair<int, string> pair in previous)
                {
                    if (!current.ContainsKey(pair.Key))
                    {
                        HandleStopped(pair.Key, pair.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("轮询进程失败", ex);
            }
            finally
            {
                lock (_sync) { _polling = false; }
            }
        }

        private static Dictionary<int, string> TakeSnapshot()
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            Process[] all = Process.GetProcesses();
            foreach (Process p in all)
            {
                try
                {
                    map[p.Id] = ConfigService.NormalizeProcessName(p.ProcessName);
                }
                catch
                {
                }
                finally
                {
                    try { p.Dispose(); }
                    catch { }
                }
            }
            return map;
        }

        private void HandleStarted(string processName, int pid)
        {
            List<RuleConfig> rules = GetRules();
            if (rules.Count == 0) return;

            // 关闭类规则：绑定该进程的退出事件
            bool stopMatched = false;
            foreach (RuleConfig rule in rules)
            {
                if (rule.Trigger == null || rule.Trigger.Type != TriggerTypes.Stop) continue;
                if (Match(rule, processName, pid))
                {
                    if (!stopMatched)
                    {
                        stopMatched = true;
                        lock (_sync)
                        {
                            if (!_firstSeen.ContainsKey(pid)) _firstSeen[pid] = DateTime.Now;
                        }
                    }
                    HookExit(pid, rule);
                }
            }

            // 启动类规则：立即触发
            foreach (RuleConfig rule in rules)
            {
                if (rule.Trigger == null || rule.Trigger.Type != TriggerTypes.Start) continue;
                if (Match(rule, processName, pid))
                {
                    Fire(rule, pid, processName);
                }
            }
        }

        private void HandleStopped(int pid, string processName)
        {
            // 部分程序启动时会先拉起一个短命辅助进程（如 Win11 记事本），
            // 它的退出不代表用户关闭了程序，按存活时长过滤掉。
            DateTime firstSeen;
            bool known;
            lock (_sync)
            {
                known = _firstSeen.TryGetValue(pid, out firstSeen);
                _firstSeen.Remove(pid);
            }

            if (known && firstSeen != DateTime.MinValue
                && (DateTime.Now - firstSeen).TotalSeconds < MinLifetimeSeconds)
            {
                Log.Debug(string.Format("进程存活过短，忽略关闭事件：{0} (PID {1})", processName, pid));
                UnhookExit(pid);
                return;
            }

            List<RuleConfig> rules = new List<RuleConfig>();
            lock (_sync)
            {
                List<RuleConfig> bound;
                if (_stopWatch.TryGetValue(pid, out bound))
                {
                    rules.AddRange(bound);
                }
            }

            // 轮询路径下可能没有绑定记录，按进程名再补一次匹配
            if (rules.Count == 0)
            {
                foreach (RuleConfig rule in GetRules())
                {
                    if (rule.Trigger == null || rule.Trigger.Type != TriggerTypes.Stop) continue;
                    if (string.Equals(ConfigService.NormalizeProcessName(rule.Trigger.ProcessName), processName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        rules.Add(rule);
                    }
                }
            }

            if (rules.Count > 0)
            {
                Log.Info(string.Format("检测到进程退出：{0} (PID {1})", processName, pid));
            }

            foreach (RuleConfig rule in rules)
            {
                Fire(rule, pid, processName);
            }

            UnhookExit(pid);
        }

        private void HookExit(int pid, RuleConfig rule)
        {
            lock (_sync)
            {
                List<RuleConfig> rules;
                if (!_stopWatch.TryGetValue(pid, out rules))
                {
                    rules = new List<RuleConfig>();
                    _stopWatch[pid] = rules;
                }
                if (!rules.Contains(rule)) rules.Add(rule);

                if (_exitHooked.ContainsKey(pid)) return;

                try
                {
                    Process p = Process.GetProcessById(pid);
                    p.EnableRaisingEvents = true;
                    p.Exited += OnHookedProcessExited;
                    _exitHooked[pid] = p;
                    Log.Debug("已绑定退出监听：PID " + pid);
                }
                catch (Exception ex)
                {
                    Log.Debug("绑定退出监听失败 PID=" + pid + "：" + ex.Message);
                }
            }
        }

        private void OnHookedProcessExited(object sender, EventArgs e)
        {
            Process p = sender as Process;
            if (p == null) return;

            int pid = -1;
            string name = string.Empty;
            try
            {
                pid = p.Id;
                name = ConfigService.NormalizeProcessName(p.ProcessName);
            }
            catch
            {
                return;
            }

            HandleStopped(pid, name);
        }

        private void UnhookExit(int pid)
        {
            lock (_sync)
            {
                Process p;
                if (_exitHooked.TryGetValue(pid, out p))
                {
                    _exitHooked.Remove(pid);
                    try
                    {
                        p.Exited -= OnHookedProcessExited;
                        p.Dispose();
                    }
                    catch { }
                }
                _stopWatch.Remove(pid);
            }
        }

        private void BindExistingStopRules()
        {
            List<RuleConfig> rules = GetRules();
            bool hasStop = false;
            foreach (RuleConfig rule in rules)
            {
                if (rule.Trigger != null && rule.Trigger.Type == TriggerTypes.Stop) hasStop = true;
            }
            if (!hasStop) return;

            int bound = 0;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    string name = ConfigService.NormalizeProcessName(p.ProcessName);
                    foreach (RuleConfig rule in rules)
                    {
                        if (rule.Trigger == null || rule.Trigger.Type != TriggerTypes.Stop) continue;
                        if (string.Equals(ConfigService.NormalizeProcessName(rule.Trigger.ProcessName), name,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            lock (_sync) { _firstSeen[p.Id] = DateTime.MinValue; }
                            HookExit(p.Id, rule);
                            bound++;
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    try { p.Dispose(); }
                    catch { }
                }
            }
            Log.Info("已绑定现有进程的退出监听，共 " + bound + " 项");
        }

        private bool Match(RuleConfig rule, string processName, int pid)
        {
            string expected = ConfigService.NormalizeProcessName(rule.Trigger.ProcessName);
            if (!string.Equals(expected, processName, StringComparison.OrdinalIgnoreCase)) return false;

            if (!string.IsNullOrEmpty(rule.Trigger.FilePath))
            {
                string actual = ProcessUtil.GetProcessPath(pid, 2);
                if (string.IsNullOrEmpty(actual))
                {
                    Log.Debug("无法获取进程路径，跳过路径校验 PID=" + pid);
                    return false;
                }
                try
                {
                    if (!string.Equals(Path.GetFullPath(actual), Path.GetFullPath(rule.Trigger.FilePath),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        private void Fire(RuleConfig rule, int pid, string processName)
        {
            if (_isSuppressed != null && _isSuppressed(processName))
            {
                Log.Debug("跳过由本程序拉起的进程：" + processName);
                return;
            }

            // 按规则去重：部分程序（如 Win11 记事本）启动时会同时产生多个同名进程，
            // 若按 PID 去重会重复执行动作，因此同一规则在短时间内只允许触发一次。
            string key = rule.Id;
            lock (_sync)
            {
                DateTime last;
                if (_recentFire.TryGetValue(key, out last) && (DateTime.Now - last).TotalSeconds < 5)
                {
                    Log.Debug("规则重复触发已被合并：" + rule.Name);
                    return;
                }
                _recentFire[key] = DateTime.Now;

                if (_recentFire.Count > 200) _recentFire.Clear();
            }

            Log.Info(string.Format("规则命中：{0}（源进程 {1} PID {2}）", rule.Name, processName, pid));

            EventHandler<RuleTriggeredEventArgs> handler = RuleTriggered;
            if (handler != null)
            {
                RuleTriggeredEventArgs args = new RuleTriggeredEventArgs(rule, pid, processName);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { handler(this, args); }
                    catch (Exception ex) { Log.Error("规则回调异常", ex); }
                });
            }
        }

        private List<RuleConfig> GetRules()
        {
            List<RuleConfig> result = new List<RuleConfig>();
            AppConfig config = _config;
            if (config == null || config.Rules == null) return result;
            foreach (RuleConfig rule in config.Rules)
            {
                if (rule.Enabled && rule.Trigger != null) result.Add(rule);
            }
            return result;
        }

        private void SetStatus(string text)
        {
            StatusText = text;
            EventHandler handler = StatusChanged;
            if (handler != null)
            {
                try { handler(this, EventArgs.Empty); }
                catch { }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            StopInternal();
        }
    }
}
