using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Luma;

// Documented shell visibility notifications provide a fallback when a game or
// input driver opens Start without delivering a normal physical Win gesture.
public sealed class NativeStartGuard : IDisposable
{
    [ComImport, Guid("2246EA2D-CAEA-4444-A3C4-6DE827E44313"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAppVisibility
    {
        [PreserveSig] int GetAppVisibilityOnMonitor(IntPtr monitor, out int mode);
        [PreserveSig] int IsLauncherVisible([MarshalAs(UnmanagedType.Bool)] out bool visible);
        [PreserveSig] int Advise([MarshalAs(UnmanagedType.Interface)] IVisibilityEvents sink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
    }
    [ComVisible(true), Guid("6584CE6B-7D82-49C2-89C9-C6BC02BA8C38"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IVisibilityEvents
    {
        [PreserveSig] int AppVisibilityOnMonitorChanged(IntPtr monitor, int previous, int current);
        [PreserveSig] int LauncherVisibilityChange([MarshalAs(UnmanagedType.Bool)] bool visible);
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class Sink : IVisibilityEvents
    {
        readonly Action<bool> changed;
        public Sink(Action<bool> callback) { changed = callback; }
        public int AppVisibilityOnMonitorChanged(IntPtr monitor, int previous, int current) => 0;
        public int LauncherVisibilityChange(bool visible) { try { changed(visible); } catch { } return 0; }
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    delegate void WinEvent(IntPtr hook, uint eventId, IntPtr window, int objectId, int child, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool PostMessage(IntPtr window, uint message, UIntPtr key, IntPtr data);
    readonly Program owner;
    readonly NativeStartHandoffPolicy policy = new NativeStartHandoffPolicy();
    readonly DispatcherTimer retry, watchdog;
    IAppVisibility visibility;
    Sink sink;
    uint cookie;
    readonly WinEvent foregroundChanged;
    IntPtr foregroundHook;
    int began;
    bool disposed;
    StartButtonBounds anchor;
    public bool Pending => policy.Pending;
    bool Enabled => !disposed && (owner.State.WinKeyOpensLibrary || owner.State.ReplaceStartMenu);

    public NativeStartGuard(Program program)
    {
        if (Program.QaMode) throw new InvalidOperationException("离线验证不能接管系统菜单。");
        owner = program;
        foregroundChanged = delegate
        {
            if (!disposed) owner.Dispatcher.BeginInvoke(DispatcherPriority.Send, (Action)(() => { if (Enabled && NativeVisible()) Observe(true); }));
        };
        foregroundHook = SetWinEventHook(3, 3, IntPtr.Zero, foregroundChanged, 0, 0, 2);
        retry = new DispatcherTimer(DispatcherPriority.Send, owner.Dispatcher) { Interval = TimeSpan.FromMilliseconds(60) };
        retry.Tick += delegate { Advance(); };
        watchdog = new DispatcherTimer(DispatcherPriority.Background, owner.Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        watchdog.Tick += delegate
        {
            if (!Enabled) { Cancel(); return; }
            // Also observe the hidden edge to rearm after a timed-out handoff.
            Observe(NativeVisible());
        };
        try
        {
            visibility = (IAppVisibility)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("7E5FE3D9-985F-4908-91F9-EE19F9FD1514")));
            sink = new Sink(value => owner.Dispatcher.BeginInvoke(DispatcherPriority.Send, (Action)(() => Observe(value))));
            Marshal.ThrowExceptionForHR(visibility.Advise(sink, out cookie));
        }
        catch (Exception error) { CrashLog.Write("native-start-notification-unavailable", error); }
        watchdog.Start();
    }
    static bool ForegroundIsStart(IntPtr window, bool shellConfirmed = false)
    {
        if (window == IntPtr.Zero) return false;
        try
        {
            uint process; GetWindowThreadProcessId(window, out process);
            using (var item = Process.GetProcessById((int)process))
            {
                var name = new System.Text.StringBuilder(128); GetClassName(window, name, name.Capacity);
                return NativeStartDismissal.IsSystemHost(item.ProcessName, item.MainModule.FileName,
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), name.ToString(), shellConfirmed);
            }
        }
        catch { return false; }
    }
    bool NativeVisible()
    {
        try { bool visible; if (visibility != null && visibility.IsLauncherVisible(out visible) == 0 && visible) return true; }
        catch { }
        return ForegroundIsStart(GetForegroundWindow());
    }
    void Observe(bool visible)
    {
        if (disposed) return;
        if (policy.Begin(Enabled, visible))
        {
            CrashLog.Write("native-start-handoff-begin");
            began = Environment.TickCount;
            anchor = owner.StartIntegration?.ForegroundStartButton();
            retry.Start();
        }
        if (Pending) Advance();
    }
    void Advance()
    {
        if (disposed) return;
        IntPtr foreground = GetForegroundWindow();
        bool nativeVisible = NativeVisible();
        var action = policy.Next(Enabled, nativeVisible, ForegroundIsStart(foreground, nativeVisible),
            GetAsyncKeyState(0x5b) < 0 || GetAsyncKeyState(0x5c) < 0, unchecked(Environment.TickCount - began));
        if (action == NativeStartAction.DismissNative)
        {
            // Never send Escape to a game, editor, or arbitrary foreground app.
            if (!NativeStartDismissal.TryDismiss(foreground, window => ForegroundIsStart(window, nativeVisible), PostMessage))
                CrashLog.Write("native-start-dismiss-input-blocked");
        }
        if (action == NativeStartAction.ShowLauncher)
        {
            CrashLog.Write("native-start-handoff-complete");
            retry.Stop();
            owner.OpenStartSurface(anchor); // Ensure visible, never toggle a menu already opened by the key hook.
        }
        if (action == NativeStartAction.Stop)
        {
            retry.Stop();
            CrashLog.Write("native-start-handoff-not-completed");
        }
    }
    public void Cancel() { policy.Cancel(); retry.Stop(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Cancel(); watchdog.Stop();
        if (foregroundHook != IntPtr.Zero) UnhookWinEvent(foregroundHook);
        foregroundHook = IntPtr.Zero;
        try { if (visibility != null && cookie != 0) visibility.Unadvise(cookie); } catch { }
        try { if (visibility != null && Marshal.IsComObject(visibility)) Marshal.FinalReleaseComObject(visibility); } catch { }
        visibility = null; sink = null;
    }
}
