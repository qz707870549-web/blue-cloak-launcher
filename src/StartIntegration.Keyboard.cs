using System;
using System.Runtime.InteropServices;

namespace Luma;

public sealed partial class StartIntegration
{
    private readonly MouseCallback keyboardCallback;
    private IntPtr keyboardHook;
    private readonly FullscreenWinKeyPolicy fullscreenKeys = new FullscreenWinKeyPolicy();
    private int lastKeyboardEvent;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardData { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    // INPUT's union also contains MOUSEINPUT (32 bytes on x64).
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion { [FieldOffset(0)] public KeyInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Data; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    private void InstallKeyboardHook()
    {
        // Replace on the hook thread, never during a held Win gesture.
        if (disposed || GetAsyncKeyState(0x5b) < 0 || GetAsyncKeyState(0x5c) < 0) return;
        if (fullscreenKeys.IsHandling)
        {
            if (unchecked(Environment.TickCount - lastKeyboardEvent) < 12000) return;
            fullscreenKeys.Reset();
        }
        IntPtr next = SetWindowsHookEx(13, keyboardCallback, GetModuleHandle(null), 0u);
        if (next == IntPtr.Zero) return;
        IntPtr previous = keyboardHook; keyboardHook = next;
        if (previous != IntPtr.Zero) UnhookWindowsHookEx(previous);

    }

    private void OpenFromKeyboard()
    {
        program.Dispatcher.BeginInvoke((Action)(() =>
        {
            if (!disposed && program.State.WinKeyOpensLibrary)
            {
                CrashLog.Write("start-request-key");
                if (program.StartGuard?.Pending ?? false) program.OpenStartSurface(KeyboardStartButton());
                else program.ToggleStartSurface(KeyboardStartButton());
            }
        }));
    }

    private static Input KeyEvent(ushort key, uint flags)
    {
        return new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyInput { Key = key, Flags = flags, Extra = KeyboardInputIdentity.Tag } } };
    }

    internal StartButtonBounds ForegroundStartButton() => KeyboardStartButton();

    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || disposed) return CallNextHookEx(keyboardHook, code, message, data);
        try
        {
            int msg = message.ToInt32();
            if (msg != 0x100 && msg != 0x101 && msg != 0x104 && msg != 0x105)
                return CallNextHookEx(keyboardHook, code, message, data);
            var key = (KeyboardData)Marshal.PtrToStructure(data, typeof(KeyboardData));
            if (KeyboardInputIdentity.IsOwn(key.Extra)) return CallNextHookEx(keyboardHook, code, message, data);
            lastKeyboardEvent = Environment.TickCount;
            bool down = msg == 0x100 || msg == 0x104;
            bool modifier = GetAsyncKeyState(0x10) < 0 || GetAsyncKeyState(0x11) < 0 || GetAsyncKeyState(0x12) < 0;
            bool handling = fullscreenKeys.IsHandling;
            // Legacy policy parameter stays true: capture never depends on fullscreen detection.
            var action = fullscreenKeys.Process((int)key.Key, down, false, program.State.WinKeyOpensLibrary, true, modifier);
            if (action == FullscreenWinAction.Toggle) { OpenFromKeyboard(); return new IntPtr(1); }
            if (action == FullscreenWinAction.Suppress) return new IntPtr(1);
            if (action == FullscreenWinAction.ReplayChord)
            {
                var chord = new[] { KeyEvent((ushort)fullscreenKeys.PendingWin, 1), KeyEvent((ushort)key.Key, key.Flags & 1) };
                uint sent = SendInput(2, chord, Marshal.SizeOf(typeof(Input)));
                if (sent > 0) fullscreenKeys.MarkReplayed();
                if (sent == 2) return new IntPtr(1);
                program.Dispatcher.BeginInvoke((Action)(() => CrashLog.Write("fullscreen-win-chord-input-blocked")));
                return CallNextHookEx(keyboardHook, code, message, data);
            }
            if (handling || fullscreenKeys.IsHandling) return CallNextHookEx(keyboardHook, code, message, data);
        }
        catch { fullscreenKeys.Cancel(); }
        return CallNextHookEx(keyboardHook, code, message, data);
    }

    private StartButtonBounds KeyboardStartButton()
    {
        var foreground = GetForegroundWindow();
        var screen = foreground == IntPtr.Zero ? System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position) : System.Windows.Forms.Screen.FromHandle(foreground);
        foreach (var button in buttons)
            if (screen.Bounds.Contains(button.Left, button.Top)) return button;
        return null; // Same monitor, centered above its taskbar if discovery is pending.
    }
}
