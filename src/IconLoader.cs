using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Luma;

public static class IconLoader
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern uint PrivateExtractIcons(string file, int index, int width, int height, IntPtr[] icons, uint[] ids, uint count, uint flags);

	public static ImageSource ReadImage(string path)
	{
		try
		{
			if (!String.IsNullOrEmpty(path) && !Path.IsPathRooted(path)) path = Path.Combine(Program.DataDir, path);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return null;
			}
			using FileStream bitmapStream = File.OpenRead(path);
			BitmapFrame bitmapFrame = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.OrderByDescending((BitmapFrame frame) => frame.PixelWidth).First();
			bitmapFrame.Freeze();
			return bitmapFrame;
		}
		catch
		{
			return null;
		}
	}

	private static ImageSource Resource(string path, int index)
	{
		if (!String.IsNullOrEmpty(path) && !Path.IsPathRooted(path)) path = Path.Combine(Program.DataDir, path);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return null;
		}
		if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
		{
			return ReadImage(path);
		}
		IntPtr[] array = new IntPtr[1];
		try
		{
			uint num = PrivateExtractIcons(path, index, 128, 128, array, new uint[1], 1u, 0u);
			if (num == 0 || num == uint.MaxValue || array[0] == IntPtr.Zero)
			{
				return null;
			}
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(array[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bitmapSource.Freeze();
			return bitmapSource;
		}
		catch
		{
			return null;
		}
		finally
		{
			if (array[0] != IntPtr.Zero)
			{
				Native.DestroyIcon(array[0]);
			}
		}
	}

	public static ImageSource Load(Entry app)
	{
		ImageSource imageSource = ReadImage(app.CustomIcon);
		if (imageSource == null && !string.IsNullOrEmpty(app.CustomIcon))
		{
			string fileName = Path.GetFileName(app.CustomIcon);
			imageSource = ReadImage(Path.Combine(Program.DataDir, "custom-icons", fileName)) ?? ReadImage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom-icons", fileName));
		}
		if (imageSource == null)
		{
			imageSource = ReadImage(app.ImagePath);
		}
		if (imageSource != null)
		{
			return imageSource;
		}
		if (SystemShortcuts.IsKnown(app)) return SystemShortcuts.Icon(app);
		if ((app.Target ?? "").StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
		{
			imageSource = SystemShortcuts.Icon(app);
			if (imageSource != null) return imageSource;
		}
		if (app.Website)
		{
			string text = (string.IsNullOrEmpty(app.ImagePath) ? null : Path.GetFileName(app.ImagePath));
			object obj;
			if (!string.IsNullOrEmpty(text))
			{
				obj = ReadImage(Path.Combine(Program.DataDir, "website-icons", text));
				if (obj == null)
				{
					return ReadImage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "website-icons", text));
				}
			}
			else
			{
				obj = null;
			}
			return (ImageSource)obj;
		}
		string text2 = (app.Id ?? "").Split(new string[1] { " | " }, StringSplitOptions.None)[0];
		foreach (string item in new string[2] { app.IconPath, app.Target }.Where((string text5) => !string.IsNullOrEmpty(text5) && text5.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)).Distinct())
		{
			try
			{
				dynamic val = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
				dynamic val2 = val.CreateShortcut(item);
				string text3 = Convert.ToString(val2.IconLocation);
				int num = text3.LastIndexOf(',');
				int result = 0;
				if (num >= 0)
				{
					int.TryParse(text3.Substring(num + 1), out result);
					text3 = text3.Substring(0, num);
				}
				text3 = Environment.ExpandEnvironmentVariables(text3.Trim().Trim('"'));
				imageSource = Resource(text3, result);
				if (imageSource != null)
				{
					return imageSource;
				}
				if (!File.Exists(text2))
				{
					text2 = Convert.ToString(val2.TargetPath);
				}
			}
			catch
			{
			}
		}
		imageSource = Resource(text2, 0);
		if (imageSource != null)
		{
			return imageSource;
		}
		if (File.Exists(text2))
		{
			try
			{
				string directoryName = Path.GetDirectoryName(text2);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text2);
				string[] array = new string[3]
				{
					Path.Combine(directoryName, fileNameWithoutExtension + ".ico"),
					Path.Combine(directoryName, fileNameWithoutExtension + "Resource.dll"),
					Path.Combine(directoryName, fileNameWithoutExtension + ".dll")
				};
				for (int num2 = 0; num2 < array.Length; num2++)
				{
					imageSource = Resource(array[num2], 0);
					if (imageSource != null)
					{
						return imageSource;
					}
				}
				string path = Path.Combine(directoryName, "versions");
				if (Directory.Exists(path))
				{
					foreach (string item2 in Directory.GetDirectories(path).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(4))
					{
						array = new string[3] { "favicon-new.ico", "favicon.ico", "icon.ico" };
						foreach (string text4 in array)
						{
							imageSource = ReadImage(Path.Combine(item2, "resources", "app", "resource", text4));
							if (imageSource != null)
							{
								return imageSource;
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		foreach (string item3 in new string[3] { app.IconPath, app.Target, text2 }.Where((string value) => !string.IsNullOrEmpty(value)).Distinct())
		{
			imageSource = Native.ShellIcon(item3);
			if (imageSource != null)
			{
				return imageSource;
			}
		}
		return null;
	}
}
