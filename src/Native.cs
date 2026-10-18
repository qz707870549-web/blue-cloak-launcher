using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Luma;

public static class Native
{
	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	private interface ImageFactory
	{
		[PreserveSig]
		int GetImage(SizeNative size, int flags, out IntPtr bitmap);
	}

	private struct SizeNative
	{
		public int X;

		public int Y;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct SHFILEINFO
	{
		public IntPtr hIcon;

		public int iIcon;

		public uint attributes;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string name;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string type;
	}

	public delegate bool EnumProc(IntPtr h, IntPtr l);

	public struct Accent
	{
		public int State;

		public int Flags;

		public int Color;

		public int Animation;
	}

	public struct Composition
	{
		public int Attribute;

		public IntPtr Data;

		public int Size;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
	private static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out ImageFactory factory);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr h);

	public static ImageSource ShellIcon(string path)
	{
		ImageFactory factory = null;
		IntPtr bitmap = IntPtr.Zero;
		try
		{
			if (path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
			{
				path = "::{4234d49b-0245-4df3-b780-3893943456e1}\\" + path.Substring(17);
			}
			Guid iid = typeof(ImageFactory).GUID;
			SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
			if (factory.GetImage(new SizeNative
			{
				X = 128,
				Y = 128
			}, 260, out bitmap) != 0 || bitmap == IntPtr.Zero)
			{
				return null;
			}
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bitmapSource.Freeze();
			return bitmapSource;
		}
		catch
		{
			return null;
		}
		finally
		{
			if (bitmap != IntPtr.Zero)
			{
				DeleteObject(bitmap);
			}
			if (factory != null)
			{
				Marshal.ReleaseComObject(factory);
			}
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SHGetFileInfo(string path, uint a, out SHFILEINFO info, uint size, uint flags);

	[DllImport("user32.dll")]
	public static extern bool DestroyIcon(IntPtr h);

	[DllImport("user32.dll")]
	public static extern IntPtr FindWindow(string c, string n);

	[DllImport("user32.dll")]
	public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);

	[DllImport("user32.dll")]
	public static extern bool IsWindowVisible(IntPtr h);

	[DllImport("user32.dll")]
	public static extern bool ShowWindow(IntPtr h, int n);

	[DllImport("user32.dll")]
	public static extern bool EnumWindows(EnumProc p, IntPtr l);

	[DllImport("user32.dll")]
	public static extern int SetWindowCompositionAttribute(IntPtr h, ref Composition a);

	public static void Blur(Window w)
	{
		try
		{
			Accent structure = new Accent
			{
				State = 4,
				Flags = 2,
				Color = -1155260391
			};
			IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
			try
			{
				Marshal.StructureToPtr(structure, intPtr, fDeleteOld: false);
				Composition a = new Composition
				{
					Attribute = 19,
					Data = intPtr,
					Size = Marshal.SizeOf(structure)
				};
				SetWindowCompositionAttribute(new WindowInteropHelper(w).Handle, ref a);
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}
		catch
		{
		}
	}

	public static IntPtr Desktop()
	{
		IntPtr p = FindWindow("Progman", null);
		IntPtr d = FindWindowEx(p, IntPtr.Zero, "SHELLDLL_DefView", null);
		if (d == IntPtr.Zero)
		{
			EnumWindows(delegate(IntPtr h, IntPtr l)
			{
				d = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
				return d == IntPtr.Zero;
			}, IntPtr.Zero);
		}
		return FindWindowEx(d, IntPtr.Zero, "SysListView32", null);
	}
}
