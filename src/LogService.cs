using System;
using System.IO;
using System.Text;

namespace AutoFlow
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    /// <summary>
    /// 轻量滚动日志：按天一个文件，保留指定天数。
    /// 所有写入异常都被吞掉，日志本身不允许影响主流程。
    /// </summary>
    public static class Log
    {
        private static readonly object SyncRoot = new object();
        private static string _logDir;
        private static int _retainDays = 30;
        private static LogLevel _minLevel = LogLevel.Info;

        public static void Init(string logDir, int retainDays, LogLevel minLevel)
        {
            lock (SyncRoot)
            {
                _logDir = logDir;
                if (retainDays > 0) _retainDays = retainDays;
                _minLevel = minLevel;
                AppPaths.EnsureDirectory(_logDir);
            }
            Cleanup();
        }

        public static void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public static void Warn(string message)
        {
            Write(LogLevel.Warn, message);
        }

        public static void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        public static void Error(string message, Exception ex)
        {
            StringBuilder sb = new StringBuilder(message);
            if (ex != null)
            {
                sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.Append(Environment.NewLine).Append(ex.StackTrace);
                }
            }
            Write(LogLevel.Error, sb.ToString());
        }

        private static void Write(LogLevel level, string message)
        {
            if (level < _minLevel) return;

            try
            {
                lock (SyncRoot)
                {
                    if (string.IsNullOrEmpty(_logDir))
                    {
                        _logDir = AppPaths.LogDir;
                        AppPaths.EnsureDirectory(_logDir);
                    }

                    string file = Path.Combine(_logDir, DateTime.Now.ToString("yyyyMMdd") + ".log");
                    string line = string.Format("{0:HH:mm:ss.fff} [{1,-5}] {2}{3}",
                        DateTime.Now, level.ToString().ToUpperInvariant(), message, Environment.NewLine);
                    File.AppendAllText(file, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志失败不影响业务
            }
        }

        private static void Cleanup()
        {
            try
            {
                if (string.IsNullOrEmpty(_logDir) || !Directory.Exists(_logDir)) return;
                DateTime limit = DateTime.Now.Date.AddDays(-_retainDays);
                foreach (string file in Directory.GetFiles(_logDir, "*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < limit)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
