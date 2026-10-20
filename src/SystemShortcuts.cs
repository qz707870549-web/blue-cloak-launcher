using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Luma;

public static class SystemShortcuts
{
    public static List<Entry> Entries() => new List<Entry>
    {
        Create("此电脑", "20D04FE0-3AEA-1069-A2D8-08002B30309D"),
        Create("回收站", "645FF040-5081-101B-9F08-00AA002F954E"),
        Create("控制面板", "26EE0668-A00A-44D7-9371-BEB064C98683")
    };
    static Entry Create(string name, string guid)
    {
        string target = "shell:::{" + guid + "}";
        return new Entry { Id = target.ToLowerInvariant(), Name = name, Target = target, Group = "电脑", Manual = true };
    }
    public static bool IsKnown(Entry entry) => entry != null && Entries().Any(a => String.Equals(a.Target, entry.Target, StringComparison.OrdinalIgnoreCase));
    public static void Ensure(Config config)
    {
        foreach (var item in Entries())
        {
            if (config.Apps.Any(a => String.Equals(a.Id, item.Id, StringComparison.OrdinalIgnoreCase) || String.Equals(a.Target, item.Target, StringComparison.OrdinalIgnoreCase))) continue;
            if (config.ExcludedIds.Any(id => String.Equals(id, item.Id, StringComparison.OrdinalIgnoreCase)) || config.RemovedEntries.Any(a => String.Equals(a.Id, item.Id, StringComparison.OrdinalIgnoreCase) || String.Equals(a.Target, item.Target, StringComparison.OrdinalIgnoreCase))) continue;
            if (!config.Groups.Contains("电脑")) config.Groups.Add("电脑");
            config.Apps.Add(item);
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShellInfo
    {
        public IntPtr Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attributes, ref ShellInfo info, uint size, uint flags);
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IImageFactory { [PreserveSig] int GetImage(ImageSize size, uint flags, out IntPtr bitmap); }
    [StructLayout(LayoutKind.Sequential)] struct ImageSize { public int Width, Height; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IImageFactory item);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    static ImageSource LargeIcon(string path)
    {
        IImageFactory item = null; IntPtr bitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item) != 0 || item == null) return null;
            if (item.GetImage(new ImageSize { Width = 128, Height = 128 }, 5, out bitmap) != 0 || bitmap == IntPtr.Zero) return null;
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); source.Freeze(); return source;
        }
        catch { return null; }
        finally { if (bitmap != IntPtr.Zero) DeleteObject(bitmap); if (item != null) Marshal.FinalReleaseComObject(item); }
    }
    public static ImageSource Icon(Entry entry)
    {
        if (!IsKnown(entry) && !(entry.Target ?? "").StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase)) return null;
        string path = IsKnown(entry) ? entry.Target.Substring(6) : entry.Target;
        var large = LargeIcon(path); if (large != null) return large;
        IntPtr pidl = IntPtr.Zero; var info = new ShellInfo();
        try
        {
            uint attributes;
            if (SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out attributes) != 0 || pidl == IntPtr.Zero) return null;
            if (SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf(typeof(ShellInfo)), 0x108) == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze(); return bitmap;
        }
        catch { return null; }
        finally { if (info.Icon != IntPtr.Zero) Native.DestroyIcon(info.Icon); if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
    }
}
