using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoFlow
{
    /// <summary>统一获取程序图标：优先取 exe 内嵌图标，失败时回退系统默认图标。</summary>
    internal static class AppIcon
    {
        private static readonly object SyncRoot = new object();
        private static Icon _value;

        public static Icon Value
        {
            get
            {
                lock (SyncRoot)
                {
                    if (_value == null)
                    {
                        try
                        {
                            _value = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                        }
                        catch
                        {
                        }

                        if (_value == null) _value = SystemIcons.Application;
                    }
                    return _value;
                }
            }
        }
    }
}
