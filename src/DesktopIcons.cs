using System;
using System.Runtime.InteropServices;

namespace Luma;

public static class DesktopIcons
{
    private delegate bool EnumWindow(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string name);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    public static bool SetHidden(bool hidden)
    {
        // QA must never change Explorer or the user's desktop.
        if (Program.QaMode) return true;
        IntPtr view = IntPtr.Zero;
        EnumWindows((window, unused) => {
            var candidate = FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (candidate == IntPtr.Zero) return true;
            view = candidate;
            return false;
        }, IntPtr.Zero);
        if (view == IntPtr.Zero) return false;
        var icons = FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
        if (icons == IntPtr.Zero) return false;
        if (IsWindowVisible(icons) == hidden)
        {
            // Explorer's own View > Show desktop icons command; never touch user files.
            if (SendMessageTimeout(view, 0x111, new IntPtr(0x7402), IntPtr.Zero, 2, 1000, out var result) == IntPtr.Zero) return false;
        }
        return IsWindowVisible(icons) != hidden;
    }
}
