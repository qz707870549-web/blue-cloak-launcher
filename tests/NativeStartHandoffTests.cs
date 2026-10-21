using System;
using Luma;
class NativeStartHandoffTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread] static int Main()
    {
        try
        {
            var p = new NativeStartHandoffPolicy();
            Check(!p.Begin(false, true) && !p.Begin(true, false), "disabled replacement and hidden native menu never begin a handoff");
            Check(p.Begin(true, true) && !p.Begin(true, true), "duplicate shell and foreground notifications create only one pending handoff");
            Check(p.Next(true, true, true, true, 10) == NativeStartAction.None, "native dismissal waits until physical Win release");
            Check(p.Next(true, true, false, false, 20) == NativeStartAction.None, "no Escape is sent while a game or editor is foreground");
            Check(p.Next(true, true, true, false, 60) == NativeStartAction.DismissNative && p.Next(true, true, true, false, 120) == NativeStartAction.DismissNative && p.Next(true, true, true, false, 180) == NativeStartAction.None, "native dismissal retries are bounded to two verified shell attempts");
            Check(p.Next(true, false, false, false, 240) == NativeStartAction.ShowLauncher && !p.Pending && p.Next(true, false, false, false, 300) == NativeStartAction.None, "closed native menu produces one ensure-visible action instead of toggling launcher closed");
            p.Begin(true, true); Check(p.Next(false, true, true, false, 10) == NativeStartAction.Stop && !p.Pending, "disabling replacement cancels the native-menu handoff");
            p.Begin(true, true); Check(p.Next(true, true, true, false, 3001) == NativeStartAction.Stop && !p.Pending, "unresponsive shell handoff stops after a finite deadline");
            Check(!p.Begin(true, true), "watchdog cannot restart a timed-out handoff while the same native menu remains visible");
            p.Begin(true, false); Check(p.Begin(true, true), "a genuinely new native-menu visibility edge rearms the bounded handoff");
            p.Cancel(); Check(p.Next(true, false, false, false, 50) == NativeStartAction.None && !p.Begin(true, true), "user dismissal cancels delayed reopen and repeated visible notifications");
            foreach (string process in new[] { "NarakaBladepoint", "game", "notepad", "explorer", "BlueCloakLauncher" })
                Check(!NativeStartHandoffPolicy.IsStartHost(process, true), "game and ordinary app processes cannot receive native dismissal: " + process);
            Check(NativeStartHandoffPolicy.IsStartHost("StartMenuExperienceHost") && NativeStartHandoffPolicy.IsStartHost("startmenuexperiencehost"), "native Start host matching is exact and case insensitive");
            Check(!NativeStartHandoffPolicy.IsStartHost("ShellHost") && NativeStartHandoffPolicy.IsStartHost("ShellHost", true) && !NativeStartHandoffPolicy.IsStartHost("ShellExperienceHost") && NativeStartHandoffPolicy.IsStartHost("ShellExperienceHost", true), "shared shell hosts require an independent Start-visibility confirmation");
            Check(KeyboardInputIdentity.IsOwn(KeyboardInputIdentity.Tag) && !KeyboardInputIdentity.IsOwn(UIntPtr.Zero) && !KeyboardInputIdentity.IsOwn(new UIntPtr(1)), "only our tagged replays bypass capture, not every injected game or driver event");
            string windows = @"C:\Windows";
            Check(NativeStartDismissal.IsSystemHost("StartMenuExperienceHost", windows + @"\SystemApps\Microsoft.Windows.StartMenuExperienceHost_test\StartMenuExperienceHost.exe", windows, "", false), "native dismissal accepts a real Start host under the Windows system-app directory");
            Check(!NativeStartDismissal.IsSystemHost("StartMenuExperienceHost", @"C:\Temp\StartMenuExperienceHost.exe", windows, "", true), "a normal application with a Shell-like name cannot receive dismissal");
            Check(!NativeStartDismissal.IsSystemHost("ShellExperienceHost", windows + @"\SystemApps\Shell\ShellExperienceHost.exe", windows, "", false)
                && NativeStartDismissal.IsSystemHost("ShellExperienceHost", windows + @"\SystemApps\Shell\ShellExperienceHost.exe", windows, "", true), "shared Shell processes require both trusted path and independent Start visibility");
            Check(NativeStartDismissal.IsSystemHost("explorer", windows + @"\explorer.exe", windows, "Shell_TrayWnd", true)
                && !NativeStartDismissal.IsSystemHost("explorer", windows + @"\explorer.exe", windows, "CabinetWClass", true), "only the taskbar, never an ordinary Explorer window, is an eligible fallback target");
            var posted = new System.Collections.Generic.List<uint>();
            Func<IntPtr, uint, UIntPtr, IntPtr, bool> record = (target, message, key, data) => { posted.Add(message); return true; };
            Check(!NativeStartDismissal.TryDismiss(IntPtr.Zero, target => true, record)
                && !NativeStartDismissal.TryDismiss(new IntPtr(0xffff), target => true, record) && posted.Count == 0, "dismissal rejects null and broadcast HWNDs without sending any messages");
            Check(!NativeStartDismissal.TryDismiss(new IntPtr(23), target => false, record) && posted.Count == 0, "unconfirmed game or editor windows receive no close input");
            var shellWindow = new IntPtr(24); var gameWindow = new IntPtr(25); var simulatedForeground = shellWindow;
            bool targetBound = NativeStartDismissal.TryDismiss(shellWindow, target => target == shellWindow, (target, message, key, data) => {
                if (target == gameWindow || target != shellWindow || key.ToUInt32() != 0x1b) throw new Exception("Escape redirected to foreground");
                posted.Add(message); simulatedForeground = gameWindow; return true;
            });
            Check(targetBound && simulatedForeground == gameWindow && posted.Count == 2 && posted[0] == 0x100 && posted[1] == 0x101, "a foreground switch cannot redirect the ordered Escape pair away from the confirmed Shell HWND");
            int posts = 0;
            Check(!NativeStartDismissal.TryDismiss(shellWindow, target => true, (target, message, key, data) => { posts++; return false; }) && posts == 1, "failed targeted dismissal stops immediately without a global-input fallback");
            foreach (int win in new[] { 0x5b, 0x5c })
            {
                var capture = new FullscreenWinKeyPolicy();
                Check(capture.Process(win, true, KeyboardInputIdentity.IsOwn(UIntPtr.Zero), true, true, false) == FullscreenWinAction.Suppress && capture.Process(win, false, false, true, true, false) == FullscreenWinAction.Toggle, "externally forwarded Win events are captured on both edges: " + win);
            }
            int notices = 0; var sink = new NativeStartGuard.Sink(visible => { if (visible) notices++; });
            Check(sink.LauncherVisibilityChange(true) == 0 && sink.LauncherVisibilityChange(false) == 0 && notices == 1, "shell sink forwards visibility notifications without installing a live hook");
            Program.QaMode = true; bool protectedMode = false;
            try { new NativeStartGuard(null); } catch (InvalidOperationException) { protectedMode = true; }
            Check(protectedMode, "offline verification cannot instantiate the system-menu guard or register native hooks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
