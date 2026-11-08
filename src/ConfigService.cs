using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace AutoFlow
{
    public class ValidationResult
    {
        public List<string> Errors;
        public List<string> Warnings;

        public ValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }

        public string ToMessage()
        {
            List<string> lines = new List<string>();
            foreach (string e in Errors) lines.Add("· " + e);
            foreach (string w in Warnings) lines.Add("· " + w);
            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }

    public static class ConfigService
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(AppConfig));

        public static AppConfig Load()
        {
            string path = AppPaths.ConfigPath;
            try
            {
                if (!File.Exists(path))
                {
                    Log.Info("配置文件不存在，创建默认配置：" + path);
                    AppConfig fresh = new AppConfig();
                    Save(fresh);
                    return fresh;
                }

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    AppConfig config = (AppConfig)Serializer.Deserialize(fs);
                    Normalize(config);
                    return config;
                }
            }
            catch (Exception ex)
            {
                Log.Error("读取配置失败：" + path, ex);
                QuarantineBrokenFile(path);
                AppConfig fallback = new AppConfig();
                try
                {
                    Save(fallback);
                }
                catch (Exception saveEx)
                {
                    Log.Error("写入默认配置失败", saveEx);
                }
                return fallback;
            }
        }

        public static void Save(AppConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");

            string path = AppPaths.ConfigPath;
            AppPaths.EnsureDirectory(Path.GetDirectoryName(path));

            if (File.Exists(path))
            {
                string backup = path + ".bak";
                try
                {
                    File.Copy(path, backup, true);
                }
                catch (Exception ex)
                {
                    Log.Warn("备份配置失败：" + ex.Message);
                }
            }

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.Encoding = new UTF8Encoding(false);

            // 先写临时文件再替换，避免写入中断导致配置损坏
            string tempPath = path + ".tmp";
            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(fs, settings))
            {
                Serializer.Serialize(writer, config);
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
            Log.Info("配置已保存：" + path);
        }

        public static ValidationResult Validate(AppConfig config)
        {
            ValidationResult result = new ValidationResult();
            if (config == null)
            {
                result.Errors.Add("配置为空");
                return result;
            }

            HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < config.Rules.Count; i++)
            {
                RuleConfig rule = config.Rules[i];
                string label = string.IsNullOrEmpty(rule.Name) ? "第 " + (i + 1) + " 条规则" : rule.Name;

                if (string.IsNullOrEmpty(rule.Name) || rule.Name.Trim().Length == 0)
                {
                    result.Errors.Add(label + "：规则名称不能为空");
                }

                if (rule.Trigger == null)
                {
                    result.Errors.Add(label + "：缺少触发条件");
                    continue;
                }

                string processName = NormalizeProcessName(rule.Trigger.ProcessName);
                if (string.IsNullOrEmpty(processName))
                {
                    result.Errors.Add(label + "：触发进程名不能为空");
                }

                if (rule.Trigger.Type != TriggerTypes.Start && rule.Trigger.Type != TriggerTypes.Stop)
                {
                    result.Errors.Add(label + "：触发类型只能是 start 或 stop");
                }

                string key = rule.Trigger.Type + "|" + processName;
                if (!string.IsNullOrEmpty(processName))
                {
                    if (seenNames.Contains(key))
                    {
                        result.Warnings.Add(label + "：与已有规则的触发条件重复（" + processName + "）");
                    }
                    else
                    {
                        seenNames.Add(key);
                    }
                }

                if (!string.IsNullOrEmpty(rule.Trigger.FilePath) && !File.Exists(rule.Trigger.FilePath))
                {
                    result.Warnings.Add(label + "：触发路径不存在（" + rule.Trigger.FilePath + "）");
                }

                if (rule.Actions == null || rule.Actions.Count == 0)
                {
                    result.Warnings.Add(label + "：没有配置任何动作");
                    continue;
                }

                for (int j = 0; j < rule.Actions.Count; j++)
                {
                    ActionConfig action = rule.Actions[j];
                    if (action.Type == ActionTypes.Launch)
                    {
                        if (string.IsNullOrEmpty(action.Path) || action.Path.Trim().Length == 0)
                        {
                            result.Errors.Add(label + "：动作 #" + (j + 1) + " 未指定程序路径");
                        }
                        else if (!File.Exists(action.Path))
                        {
                            result.Warnings.Add(label + "：目标程序不存在（" + action.Path + "）");
                        }
                    }
                    else if (action.Type == ActionTypes.Close)
                    {
                        if (string.IsNullOrEmpty(NormalizeProcessName(action.TargetProcessName)))
                        {
                            result.Errors.Add(label + "：动作 #" + (j + 1) + " 未指定要关闭的进程名");
                        }
                    }
                    else
                    {
                        result.Errors.Add(label + "：动作 #" + (j + 1) + " 类型不合法");
                    }

                    if (action.DelayMs < 0)
                    {
                        result.Errors.Add(label + "：延迟时间不能为负数");
                    }
                }
            }

            return result;
        }

        /// <summary>补齐 .exe 后缀并去掉首尾空白</summary>
        public static string NormalizeProcessName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string result = name.Trim();
            if (result.Length == 0) return string.Empty;
            if (result.IndexOf('\\') >= 0 || result.IndexOf('/') >= 0)
            {
                result = Path.GetFileName(result);
            }
            if (!result.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                result = result + ".exe";
            }
            return result;
        }

        private static void Normalize(AppConfig config)
        {
            if (config.Settings == null) config.Settings = new SettingsConfig();
            if (config.Rules == null) config.Rules = new List<RuleConfig>();
            if (config.Settings.PollIntervalMs <= 0) config.Settings.PollIntervalMs = 3000;
            if (config.Settings.LogRetainDays <= 0) config.Settings.LogRetainDays = 30;
            if (string.IsNullOrEmpty(config.Settings.LogLevel)) config.Settings.LogLevel = "Info";

            foreach (RuleConfig rule in config.Rules)
            {
                if (string.IsNullOrEmpty(rule.Id)) rule.Id = Guid.NewGuid().ToString("N");
                if (rule.Trigger == null) rule.Trigger = new TriggerConfig();
                if (rule.Actions == null) rule.Actions = new List<ActionConfig>();
                rule.Trigger.ProcessName = NormalizeProcessName(rule.Trigger.ProcessName);
                foreach (ActionConfig action in rule.Actions)
                {
                    action.TargetProcessName = NormalizeProcessName(action.TargetProcessName);
                }
            }
        }

        private static void QuarantineBrokenFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string broken = path + ".broken." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Move(path, broken);
                Log.Warn("配置文件格式错误，已转移到：" + broken);
            }
            catch (Exception ex)
            {
                Log.Error("转移损坏配置文件失败", ex);
            }
        }
    }
}
