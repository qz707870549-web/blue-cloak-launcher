using System;
using System.IO;

namespace Luma;

// Keep dismissal bound to an identified Shell HWND. A foreground change must
// never redirect an Escape into a game or editor via the global input stream.
public static class NativeStartDismissal
{
    public static bool IsSystemHost(string process, string executable, string windows, string windowClass, bool shellConfirmed)
    {
        try
        {
            string root = Path.GetFullPath(windows).TrimEnd(Path.DirectorySeparatorChar);
            string path = Path.GetFullPath(executable);
            bool systemApp = path.StartsWith(root + "\\SystemApps\\", StringComparison.OrdinalIgnoreCase);
            if (!String.Equals(Path.GetFileName(path), process + ".exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (NativeStartHandoffPolicy.IsStartHost(process, shellConfirmed))
                return systemApp || (String.Equals(process, "ShellHost", StringComparison.OrdinalIgnoreCase) && String.Equals(path, Path.Combine(root, "System32", "ShellHost.exe"), StringComparison.OrdinalIgnoreCase));
            return shellConfirmed && String.Equals(process, "explorer", StringComparison.OrdinalIgnoreCase)
                && String.Equals(path, Path.Combine(root, "explorer.exe"), StringComparison.OrdinalIgnoreCase)
                && (windowClass == "Shell_TrayWnd" || windowClass == "Shell_SecondaryTrayWnd");
        }
        catch { return false; }
    }

    public static bool TryDismiss(IntPtr window, Func<IntPtr, bool> confirmed,
        Func<IntPtr, uint, UIntPtr, IntPtr, bool> post)
    {
        if (window == IntPtr.Zero || window == new IntPtr(0xffff) || !confirmed(window)) return false;
        // WM_KEYDOWN / WM_KEYUP, VK_ESCAPE, scan code 1; no global SendInput.
        if (!post(window, 0x100, new UIntPtr(0x1b), new IntPtr(0x00010001))) return false;
        return confirmed(window) && post(window, 0x101, new UIntPtr(0x1b), new IntPtr(unchecked((int)0xc0010001)));
    }
}
