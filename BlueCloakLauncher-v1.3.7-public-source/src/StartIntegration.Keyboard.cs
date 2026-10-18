using System;
using System.Runtime.InteropServices;

namespace Luma;

public sealed partial class StartIntegration
{
    private readonly MouseCallback keyboardCallback;
    private IntPtr keyboardHook;
    private readonly WinKeyPolicy winPolicy = new WinKeyPolicy();

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

    private static Input KeyEvent(ushort key, uint flags)
    {
        return new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyInput { Key = key, Flags = flags } } };
    }

    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || disposed) return CallNextHookEx(keyboardHook, code, message, data);
        try
        {
            int msg = message.ToInt32();
            if (msg != 0x100 && msg != 0x101 && msg != 0x104 && msg != 0x105)
                return CallNextHookEx(keyboardHook, code, message, data);
            var key = (KeyboardData)Marshal.PtrToStructure(data, typeof(KeyboardData));
            bool modifier = GetAsyncKeyState(0x10) < 0 || GetAsyncKeyState(0x11) < 0 || GetAsyncKeyState(0x12) < 0;
            if (winPolicy.Process((int)key.Key, msg == 0x100 || msg == 0x104,
                (key.Flags & 0x10) != 0, program.State.WinKeyOpensLibrary, modifier))
            {
                // A harmless unassigned key masks the native Start action. Replay
                // Win-up in the same batch so Windows never retains a held Win key.
                var inputs = new[] { KeyEvent(0xe8, 0), KeyEvent(0xe8, 2), KeyEvent((ushort)key.Key, 3) };
                uint sent = SendInput(3, inputs, Marshal.SizeOf(typeof(Input)));
                if (sent == 3)
                {
                    program.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (!disposed && program.State.WinKeyOpensLibrary) program.ToggleStartSurface(KeyboardStartButton());
                    }));
                    return new IntPtr(1);
                }
                // On injection failure pass the physical release through.
                if (sent == 1) SendInput(1, new[] { KeyEvent(0xe8, 2) }, Marshal.SizeOf(typeof(Input)));
            }
        }
        catch { winPolicy.Cancel(); }
        return CallNextHookEx(keyboardHook, code, message, data);
    }

    private StartButtonBounds KeyboardStartButton()
    {
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        foreach (var button in buttons)
            if (screen.Bounds.Contains(button.Left, button.Top)) return button;
        return null; // Same monitor, centered above its taskbar if discovery is pending.
    }
}
