using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AutoFlow
{
    /// <summary>
    /// 动作执行器：负责延迟调度、取消条件判断、环路抑制。
    /// 所有实际动作串行执行，避免多条规则同时命中时互相干扰。
    /// </summary>
    public class ActionExecutor : IDisposable
    {
        private readonly object _execLock = new object();
        private readonly object _suppressLock = new object();
        private readonly Dictionary<string, DateTime> _suppress =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Timer> _timers = new List<Timer>();
        private int _gracefulTimeoutMs = 5000;
        private bool _disposed;

        public event EventHandler<string> ActionLogged;

        /// <summary>判断某个进程名是否刚刚由本程序拉起（用于阻止规则自我循环）</summary>
        public bool IsSuppressed(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            string bare = ConfigService.NormalizeProcessName(processName);
            lock (_suppressLock)
            {
                DateTime expire;
                if (_suppress.TryGetValue(bare, out expire))
                {
                    if (DateTime.Now < expire) return true;
                    _suppress.Remove(bare);
                }
            }
            return false;
        }

        private void Suppress(string processName, int seconds)
        {
            if (string.IsNullOrEmpty(processName)) return;
            string bare = ConfigService.NormalizeProcessName(processName);
            lock (_suppressLock)
            {
                _suppress[bare] = DateTime.Now.AddSeconds(seconds);
            }
        }

        /// <summary>按规则触发全部动作，各自独立延迟</summary>
        public void Execute(RuleConfig rule, int sourcePid, string sourceName)
        {
            if (rule == null || rule.Actions == null || rule.Actions.Count == 0) return;

            foreach (ActionConfig action in rule.Actions)
            {
                ActionConfig captured = action;
                Schedule(delegate { RunAction(rule, captured, sourcePid, sourceName); }, action.DelayMs);
            }
        }

        private void Schedule(Action work, int delayMs)
        {
            if (_disposed) return;

            if (delayMs <= 0)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { work(); }
                    catch (Exception ex) { Log.Error("执行动作异常", ex); }
                });
                return;
            }

            Timer timer = null;
            timer = new Timer(delegate
            {
                try
                {
                    lock (_timers) { _timers.Remove(timer); }
                    timer.Dispose();
                }
                catch { }

                try { work(); }
                catch (Exception ex) { Log.Error("执行延迟动作异常", ex); }
            }, null, delayMs, Timeout.Infinite);

            lock (_timers)
            {
                _timers.Add(timer);
            }
        }

        private void RunAction(RuleConfig rule, ActionConfig action, int sourcePid, string sourceName)
        {
            if (_disposed) return;

            lock (_execLock)
            {
                try
                {
                    // 该选项只对“启动”类动作有意义：延迟结束时源程序已退出则不再拉起。
                    // “关闭”类规则的源进程本来就是已退出状态，不能套用这个判断。
                    if (action.Type != ActionTypes.Close
                        && action.CancelIfSourceExited
                        && sourcePid > 0
                        && !ProcessUtil.IsPidAlive(sourcePid))
                    {
                        string msg = string.Format("源进程已退出，取消动作：{0}", action.DisplayName);
                        Log.Info(msg);
                        RaiseLogged(msg);
                        return;
                    }

                    if (action.Type == ActionTypes.Close)
                    {
                        RaiseLogged("执行关闭：" + action.TargetProcessName);
                        ProcessUtil.CloseByName(action.TargetProcessName, action.Force, action.KillTree, _gracefulTimeoutMs);
                        return;
                    }

                    RunLaunch(action);
                }
                catch (Exception ex)
                {
                    Log.Error("执行动作失败：" + action.DisplayName, ex);
                    RaiseLogged("执行失败：" + ex.Message);
                }
            }
        }

        private void RunLaunch(ActionConfig action)
        {
            if (string.IsNullOrEmpty(action.Path))
            {
                Log.Warn("启动动作缺少路径，已跳过");
                return;
            }

            string targetName = Path.GetFileName(action.Path);

            if (action.SkipIfRunning)
            {
                string bare = Path.GetFileNameWithoutExtension(action.Path);
                if (ProcessUtil.IsRunning(bare))
                {
                    string msg = "目标已在运行，跳过：" + targetName;
                    Log.Info(msg);
                    RaiseLogged(msg);
                    return;
                }
            }

            RaiseLogged("执行启动：" + action.DisplayName);

            Process started = ProcessUtil.Launch(action.Path, action.Args, action.WorkingDir,
                action.RunHidden, action.RunAsAdmin);

            if (started != null)
            {
                try { started.Dispose(); }
                catch { }

                // 抑制该进程名一段时间，避免被自己的规则再次捕获形成环路
                Suppress(targetName, 10);
            }
        }

        private void RaiseLogged(string message)
        {
            EventHandler<string> handler = ActionLogged;
            if (handler != null)
            {
                try { handler(this, message); }
                catch { }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            lock (_timers)
            {
                foreach (Timer t in _timers)
                {
                    try { t.Dispose(); }
                    catch { }
                }
                _timers.Clear();
            }
        }
    }
}
