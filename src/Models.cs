using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace AutoFlow
{
    /// <summary>触发类型常量</summary>
    public static class TriggerTypes
    {
        public const string Start = "start";
        public const string Stop = "stop";
    }

    /// <summary>动作类型常量</summary>
    public static class ActionTypes
    {
        public const string Launch = "launch";
        public const string Close = "close";
    }

    [XmlRoot("AppConfig")]
    public class AppConfig
    {
        [XmlAttribute("version")]
        public int Version { get; set; }

        [XmlElement("Settings")]
        public SettingsConfig Settings { get; set; }

        [XmlArray("Rules")]
        [XmlArrayItem("Rule")]
        public List<RuleConfig> Rules { get; set; }

        public AppConfig()
        {
            Version = 1;
            Settings = new SettingsConfig();
            Rules = new List<RuleConfig>();
        }
    }

    public class SettingsConfig
    {
        /// <summary>WMI 事件失效时是否启用轮询兜底</summary>
        [XmlElement("PollFallback")]
        public bool PollFallback { get; set; }

        /// <summary>轮询间隔（毫秒）</summary>
        [XmlElement("PollIntervalMs")]
        public int PollIntervalMs { get; set; }

        /// <summary>关闭主窗口时最小化到托盘</summary>
        [XmlElement("MinimizeToTray")]
        public bool MinimizeToTray { get; set; }

        /// <summary>日志保留天数</summary>
        [XmlElement("LogRetainDays")]
        public int LogRetainDays { get; set; }

        /// <summary>Debug / Info / Warn / Error</summary>
        [XmlElement("LogLevel")]
        public string LogLevel { get; set; }

        public SettingsConfig()
        {
            PollFallback = true;
            PollIntervalMs = 3000;
            MinimizeToTray = true;
            LogRetainDays = 30;
            LogLevel = "Info";
        }
    }

    public class RuleConfig
    {
        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("enabled")]
        public bool Enabled { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("Trigger")]
        public TriggerConfig Trigger { get; set; }

        [XmlArray("Actions")]
        [XmlArrayItem("Action")]
        public List<ActionConfig> Actions { get; set; }

        public RuleConfig()
        {
            Id = Guid.NewGuid().ToString("N");
            Enabled = true;
            Name = "新规则";
            Trigger = new TriggerConfig();
            Actions = new List<ActionConfig>();
        }

        public RuleConfig Clone()
        {
            RuleConfig copy = new RuleConfig();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Enabled = Enabled;
            copy.Name = Name;
            copy.Trigger = Trigger.Clone();
            copy.Actions = new List<ActionConfig>();
            foreach (ActionConfig a in Actions)
            {
                copy.Actions.Add(a.Clone());
            }
            return copy;
        }

        public string TriggerSummary
        {
            get
            {
                if (Trigger == null) return string.Empty;
                string verb = Trigger.Type == TriggerTypes.Stop ? "关闭时" : "启动时";
                return string.Format("{0} {1}", Trigger.ProcessName, verb);
            }
        }

        public string ActionSummary
        {
            get
            {
                if (Actions == null || Actions.Count == 0) return "（无动作）";
                List<string> parts = new List<string>();
                foreach (ActionConfig a in Actions)
                {
                    if (a.Type == ActionTypes.Close)
                    {
                        parts.Add("关闭 " + a.TargetProcessName);
                    }
                    else
                    {
                        string name = a.Path;
                        int idx = name.LastIndexOf('\\');
                        if (idx >= 0 && idx < name.Length - 1) name = name.Substring(idx + 1);
                        if (a.DelayMs > 0)
                        {
                            parts.Add(string.Format("{0}（延迟 {1} 秒）", name, a.DelayMs / 1000.0));
                        }
                        else
                        {
                            parts.Add(name);
                        }
                    }
                }
                return string.Join(" + ", parts.ToArray());
            }
        }
    }

    public class TriggerConfig
    {
        /// <summary>start = 启动时触发；stop = 关闭时触发</summary>
        [XmlAttribute("type")]
        public string Type { get; set; }

        /// <summary>进程名，如 WXWork.exe</summary>
        [XmlElement("ProcessName")]
        public string ProcessName { get; set; }

        /// <summary>可选的完整路径校验，留空表示只按进程名匹配</summary>
        [XmlElement("FilePath")]
        public string FilePath { get; set; }

        public TriggerConfig()
        {
            Type = TriggerTypes.Start;
            ProcessName = string.Empty;
            FilePath = string.Empty;
        }

        public TriggerConfig Clone()
        {
            TriggerConfig copy = new TriggerConfig();
            copy.Type = Type;
            copy.ProcessName = ProcessName;
            copy.FilePath = FilePath;
            return copy;
        }
    }

    public class ActionConfig
    {
        /// <summary>launch = 启动；close = 关闭</summary>
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlElement("Path")]
        public string Path { get; set; }

        [XmlElement("Args")]
        public string Args { get; set; }

        [XmlElement("WorkingDir")]
        public string WorkingDir { get; set; }

        /// <summary>延迟执行毫秒数，0 表示立即</summary>
        [XmlElement("DelayMs")]
        public int DelayMs { get; set; }

        /// <summary>隐藏窗口（bat 不弹黑框）</summary>
        [XmlElement("RunHidden")]
        public bool RunHidden { get; set; }

        /// <summary>以管理员身份启动目标</summary>
        [XmlElement("RunAsAdmin")]
        public bool RunAsAdmin { get; set; }

        /// <summary>目标已在运行时跳过启动</summary>
        [XmlElement("SkipIfRunning")]
        public bool SkipIfRunning { get; set; }

        /// <summary>延迟结束时若源进程已退出则取消本次动作</summary>
        [XmlElement("CancelIfSourceExited")]
        public bool CancelIfSourceExited { get; set; }

        /// <summary>close 动作的目标进程名</summary>
        [XmlElement("TargetProcessName")]
        public string TargetProcessName { get; set; }

        /// <summary>close 动作：直接强杀，不走优雅关闭</summary>
        [XmlElement("Force")]
        public bool Force { get; set; }

        /// <summary>close 动作：同时结束子进程</summary>
        [XmlElement("KillTree")]
        public bool KillTree { get; set; }

        public ActionConfig()
        {
            Type = ActionTypes.Launch;
            Path = string.Empty;
            Args = string.Empty;
            WorkingDir = string.Empty;
            DelayMs = 0;
            RunHidden = true;
            RunAsAdmin = false;
            SkipIfRunning = true;
            CancelIfSourceExited = true;
            TargetProcessName = string.Empty;
            Force = false;
            KillTree = false;
        }

        public ActionConfig Clone()
        {
            ActionConfig copy = new ActionConfig();
            copy.Type = Type;
            copy.Path = Path;
            copy.Args = Args;
            copy.WorkingDir = WorkingDir;
            copy.DelayMs = DelayMs;
            copy.RunHidden = RunHidden;
            copy.RunAsAdmin = RunAsAdmin;
            copy.SkipIfRunning = SkipIfRunning;
            copy.CancelIfSourceExited = CancelIfSourceExited;
            copy.TargetProcessName = TargetProcessName;
            copy.Force = Force;
            copy.KillTree = KillTree;
            return copy;
        }

        public string DisplayName
        {
            get
            {
                if (Type == ActionTypes.Close) return "关闭 " + TargetProcessName;
                return System.IO.Path.GetFileName(Path);
            }
        }
    }
}
