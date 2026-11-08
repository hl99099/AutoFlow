using System;
using System.IO;

namespace AutoFlow
{
    /// <summary>
    /// 统一解析程序目录、配置文件、日志目录。
    /// 配置优先放在 exe 同目录；若同目录不可写（如装在 Program Files），
    /// 自动回退到 %ProgramData%\AutoFlow。
    /// </summary>
    public static class AppPaths
    {
        private const string ConfigFileName = "AutoFlow.config.xml";
        private const string LegacyConfigFileName = "AutoLauncher.config.xml";
        private const string ProductName = "AutoFlow";
        private const string LegacyProductName = "AutoLauncher";

        private static string _baseDir;
        private static string _configPath;
        private static string _logDir;
        private static bool _resolved;
        private static bool _isFallback;

        public static string BaseDir
        {
            get { EnsureResolved(); return _baseDir; }
        }

        public static string ConfigPath
        {
            get { EnsureResolved(); return _configPath; }
        }

        public static string LogDir
        {
            get { EnsureResolved(); return _logDir; }
        }

        /// <summary>配置是否回退到了 ProgramData（界面需要提示用户）</summary>
        public static bool IsFallbackLocation
        {
            get
            {
                EnsureResolved();
                return _isFallback;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            lock (typeof(AppPaths))
            {
                if (_resolved) return;

                _baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

                if (CanWrite(_baseDir))
                {
                    _configPath = Path.Combine(_baseDir, ConfigFileName);
                    _logDir = Path.Combine(_baseDir, "logs");
                    _isFallback = false;
                }
                else
                {
                    string fallback = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        ProductName);
                    _configPath = Path.Combine(fallback, ConfigFileName);
                    _logDir = Path.Combine(fallback, "logs");
                    _isFallback = true;
                }

                _resolved = true;
            }

            MigrateLegacyConfig();
        }

        /// <summary>首次以新名称启动时，把旧版 AutoLauncher 配置复制过来，避免用户规则丢失。</summary>
        private static void MigrateLegacyConfig()
        {
            try
            {
                if (File.Exists(_configPath)) return;

                string[] candidates = new string[]
                {
                    Path.Combine(_baseDir, LegacyConfigFileName),
                    Path.Combine(Path.GetDirectoryName(_configPath), LegacyConfigFileName),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        LegacyProductName, LegacyConfigFileName)
                };

                foreach (string legacy in candidates)
                {
                    if (string.IsNullOrEmpty(legacy) || !File.Exists(legacy)) continue;
                    if (string.Equals(legacy, _configPath, StringComparison.OrdinalIgnoreCase)) continue;

                    EnsureDirectory(Path.GetDirectoryName(_configPath));
                    File.Copy(legacy, _configPath, false);
                    Log.Info("检测到旧版配置，已迁移：" + legacy + " -> " + _configPath);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("迁移旧版配置失败：" + ex.Message);
            }
        }

        /// <summary>尝试在目录下建临时文件判断写权限</summary>
        public static bool CanWrite(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return false;
                string probe = Path.Combine(dir, "." + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream fs = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.WriteByte(0);
                }
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureDirectory(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
