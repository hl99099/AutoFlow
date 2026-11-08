using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using AutoFlow;

internal static class SmokeTest
{
    private static int _failed;

    private static void Check(bool condition, string name, string detail)
    {
        if (condition)
        {
            Console.WriteLine("  PASS  " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  " + name + "  -> " + detail);
        }
    }

    private static int Main()
    {
        Log.Init(Path.Combine(Path.GetTempPath(), "al_smoke_logs"), 1, LogLevel.Info);

        Console.WriteLine("[1] 进程名规范化");
        Check(ConfigService.NormalizeProcessName("notepad") == "notepad.exe", "补全后缀", ConfigService.NormalizeProcessName("notepad"));
        Check(ConfigService.NormalizeProcessName("C:\\Windows\\NOTEPAD.EXE") == "NOTEPAD.EXE", "提取文件名", ConfigService.NormalizeProcessName("C:\\Windows\\NOTEPAD.EXE"));
        Check(ConfigService.NormalizeProcessName("  ") == "", "空输入", ConfigService.NormalizeProcessName("  "));

        Console.WriteLine("[2] 配置序列化往返");
        AppConfig original = new AppConfig();
        RuleConfig rule = new RuleConfig();
        rule.Id = "r1";
        rule.Name = "测试规则";
        rule.Trigger.Type = TriggerTypes.Start;
        rule.Trigger.ProcessName = "notepad.exe";
        ActionConfig action = new ActionConfig();
        action.Type = ActionTypes.Launch;
        action.Path = @"C:\Windows\notepad.exe";
        action.DelayMs = 1500;
        rule.Actions.Add(action);
        original.Rules.Add(rule);

        string tempFile = Path.Combine(Path.GetTempPath(), "al_smoke_" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
            using (FileStream fs = new FileStream(tempFile, FileMode.Create))
            {
                serializer.Serialize(fs, original);
            }

            AppConfig loaded;
            using (FileStream fs = new FileStream(tempFile, FileMode.Open))
            {
                loaded = (AppConfig)serializer.Deserialize(fs);
            }

            Check(loaded.Rules.Count == 1, "规则数量", loaded.Rules.Count.ToString());
            Check(loaded.Rules[0].Name == "测试规则", "规则名称", loaded.Rules[0].Name);
            Check(loaded.Rules[0].Actions[0].DelayMs == 1500, "延迟毫秒", loaded.Rules[0].Actions[0].DelayMs.ToString());
            Check(loaded.Settings.PollIntervalMs == 3000, "默认轮询间隔", loaded.Settings.PollIntervalMs.ToString());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        Console.WriteLine("[3] 配置校验");
        AppConfig invalid = new AppConfig();
        RuleConfig badRule = new RuleConfig();
        badRule.Name = "";
        badRule.Trigger.ProcessName = "";
        invalid.Rules.Add(badRule);
        ValidationResult validation = ConfigService.Validate(invalid);
        Check(!validation.IsValid, "空规则应校验失败", validation.ToMessage());

        Console.WriteLine("[4] 进程探测");
        Check(ProcessUtil.IsRunning("explorer"), "explorer 在运行", "未找到 explorer.exe");
        Check(!ProcessUtil.IsRunning("definitely_not_running_xyz"), "不存在的进程", "误判为运行中");

        Console.WriteLine("[5] 启动与关闭");
        System.Diagnostics.Process started = ProcessUtil.Launch(
            @"C:\Windows\System32\ping.exe", "-n 30 127.0.0.1", @"C:\Windows\System32", true, false);
        Check(started != null, "启动 ping", "启动失败");

        System.Threading.Thread.Sleep(600);
        Check(ProcessUtil.IsRunning("ping"), "ping 已运行", "未检测到 ping");

        int closed = ProcessUtil.CloseByName("ping", true, false, 2000);
        Check(closed >= 1, "关闭 ping", "关闭数量 " + closed);

        if (started != null)
        {
            try { started.Dispose(); }
            catch { }
        }

        System.Threading.Thread.Sleep(300);
        Check(!ProcessUtil.IsRunning("ping"), "ping 已退出", "仍在运行");

        Console.WriteLine();
        if (_failed == 0)
        {
            Console.WriteLine("ALL PASSED");
            return 0;
        }

        Console.WriteLine(_failed + " CHECK(S) FAILED");
        return 1;
    }
}
