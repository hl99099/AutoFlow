using System;
using System.Threading;
using System.Windows.Forms;

namespace AutoFlow
{
    internal static class Program
    {
        private const string MutexName = "AutoFlow.SingleInstance.Mutex";
        private const string ShowEventName = "AutoFlow.SingleInstance.Show";

        private static Mutex _mutex;
        private static EventWaitHandle _showEvent;
        private static Form _mainForm;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                SignalExistingInstance();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            Log.Init(AppPaths.LogDir, 30, LogLevel.Info);
            Log.Info("==== AutoFlow 启动 ====");
            Log.Info("程序目录：" + AppPaths.BaseDir);
            Log.Info("配置文件：" + AppPaths.ConfigPath);
            Log.Info("管理员权限：" + (TriggerEngine.IsAdministrator ? "是" : "否"));

            AppConfig config = ConfigService.Load();
            Log.Init(AppPaths.LogDir, config.Settings.LogRetainDays, ParseLevel(config.Settings.LogLevel));

            _mainForm = new MainForm(config);
            StartShowListener();

            try
            {
                Application.Run(_mainForm);
            }
            finally
            {
                if (_showEvent != null)
                {
                    try { _showEvent.Close(); }
                    catch { }
                }
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); }
                    catch { }
                    try { _mutex.Dispose(); }
                    catch { }
                }
            }
        }

        private static void SignalExistingInstance()
        {
            try
            {
                using (EventWaitHandle handle = EventWaitHandle.OpenExisting(ShowEventName))
                {
                    handle.Set();
                }
            }
            catch
            {
                MessageBox.Show("AutoFlow 已在运行中。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static void StartShowListener()
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

            Thread thread = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        _showEvent.WaitOne();
                        Form form = _mainForm;
                        if (form == null || form.IsDisposed) continue;

                        form.BeginInvoke(new MethodInvoker(delegate
                        {
                            try
                            {
                                form.Show();
                                form.WindowState = FormWindowState.Normal;
                                form.Activate();
                            }
                            catch { }
                        }));
                    }
                    catch
                    {
                        break;
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "ShowWindowListener";
            thread.Start();
        }

        private static LogLevel ParseLevel(string text)
        {
            if (string.IsNullOrEmpty(text)) return LogLevel.Info;
            try
            {
                return (LogLevel)Enum.Parse(typeof(LogLevel), text, true);
            }
            catch
            {
                return LogLevel.Info;
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.Error("未处理的界面异常", e.Exception);
            MessageBox.Show("程序发生异常，详细信息已写入日志。" + Environment.NewLine + e.Exception.Message,
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("未处理的异常", e.ExceptionObject as Exception);
        }
    }
}
