using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Luma;

public static class StartupRegistration
{
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "BlueCloakLauncher";
    public static bool LegacyEnabled()
    {
        if (Program.QaMode) return false;
        using (var key = Registry.CurrentUser.OpenSubKey(RunPath))
            return key != null && (key.GetValue(Name) != null || key.GetValue("LumaAIRI") != null || key.GetValue("LumaDock") != null);
    }
    public static string TaskXml(string executable, string sid)
    {
        if (!Path.IsPathRooted(executable)) throw new ArgumentException("启动程序必须使用完整路径。");
        string path = SecurityElement.Escape(executable), user = SecurityElement.Escape(sid);
        return "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
            "<RegistrationInfo><Description>蓝斗篷启动：登录后启动，可在软件设置中关闭。</Description></RegistrationInfo>" +
            "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId><Delay>PT10S</Delay></LogonTrigger></Triggers>" +
            "<Principals><Principal id=\"User\"><UserId>" + user + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
            "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
            "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><StartWhenAvailable>true</StartWhenAvailable><ExecutionTimeLimit>PT0S</ExecutionTimeLimit>" +
            "<RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings>" +
            "<Actions Context=\"User\"><Exec><Command>" + path + "</Command><Arguments>--background</Arguments><WorkingDirectory>" +
            SecurityElement.Escape(Path.GetDirectoryName(executable)) + "</WorkingDirectory></Exec></Actions></Task>";
    }
    public static string Apply(bool enabled)
    {
        if (Program.QaMode) throw new InvalidOperationException("离线验证模式不能修改系统启动项。");
        string executable = Process.GetCurrentProcess().MainModule.FileName;
        object service = null, folder = null, task = null;
        Exception schedulerError = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            dynamic scheduler = service; scheduler.Connect(); folder = scheduler.GetFolder("\\"); dynamic root = folder;
            if (enabled) task = root.RegisterTask(Name, TaskXml(executable, WindowsIdentity.GetCurrent().User.Value), 6, null, null, 3, null);
            else { try { root.DeleteTask(Name, 0); } catch (COMException error) when ((uint)error.HResult == 0x80070002) { } }
        }
        catch (Exception error) { schedulerError = error; CrashLog.Write("startup-task-registration", error); }
        finally
        {
            foreach (var item in new[] { task, folder, service }) if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item);
        }
        using (var key = Registry.CurrentUser.CreateSubKey(RunPath))
        {
            if (enabled) key.SetValue(Name, "\"" + executable + "\" --background");
            else key.DeleteValue(Name, false);
            key.DeleteValue("LumaAIRI", false); key.DeleteValue("LumaDock", false);
        }
        if (!enabled && schedulerError != null) throw new InvalidOperationException("登录启动项已移除，但计划任务移除失败，请重试。", schedulerError);
        return enabled ? (schedulerError == null ? "已启用登录任务（延迟 10 秒、失败重试）和备用启动项。" : "计划任务注册失败，已启用备用启动项。详情见启动日志。") : "已关闭登录启动任务和备用启动项。";
    }
}
