using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Luma;

public static class WindowController
{
	private struct RectNative
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	public static WindowSnapshot Latest = new WindowSnapshot();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr h, StringBuilder text, int max);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(IntPtr h, StringBuilder text, int max);

	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern IntPtr GetWindowLongPtr(IntPtr h, int index);

	[DllImport("user32.dll")]
	private static extern IntPtr GetWindow(IntPtr h, uint cmd);

	[DllImport("user32.dll")]
	private static extern bool IsWindow(IntPtr h);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(IntPtr h);

	[DllImport("user32.dll")]
	private static extern bool ShowWindowAsync(IntPtr h, int cmd);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr h);

	[DllImport("user32.dll")]
	private static extern IntPtr GetLastActivePopup(IntPtr h);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr h, out RectNative rect);

	[DllImport("kernel32.dll")]
	private static extern IntPtr OpenProcess(uint access, bool inherit, uint process);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder text, ref uint size);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetApplicationUserModelId(IntPtr h, ref uint size, StringBuilder text);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out int value, int size);

	[DllImport("user32.dll")]
	public static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);

	[DllImport("user32.dll")]
	public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

	public static string Family(string path)
	{
		string text = (path ?? "").Trim().Replace('/', '\\').ToLowerInvariant();
		switch (Path.GetFileNameWithoutExtension(text))
		{
		case "weixin":
		case "wechat":
		case "wechatappex":
			return "app:wechat";
		default:
			return text;
		}
	}

	public static string Executable(Entry app)
	{
		string text = (app.Id ?? app.Target ?? "").Split(new string[1] { " | " }, StringSplitOptions.None)[0];
		if (text.IndexOf("\\installer\\", StringComparison.OrdinalIgnoreCase) >= 0 && (app.IconPath ?? "").EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				dynamic val = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
				dynamic val2 = val.CreateShortcut(app.IconPath);
				text = DiscoveryRules.ShortcutExecutable(app.IconPath, (string)val2.TargetPath);
			}
			catch
			{
			}
		}
		return text;
	}

	public static bool Matches(Entry app, RunningWindow window)
	{
		string text = Executable(app);
		if (text.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
		{
			if (!string.IsNullOrEmpty(window.AppId))
			{
				return text.Substring(17).Equals(window.AppId, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		if (Family(text) == window.Key)
		{
			return true;
		}
		if (text.IndexOf('\\') < 0)
		{
			return Path.GetFileNameWithoutExtension(text).Equals(window.ProcessName, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static List<RunningWindow> ForApp(Entry app)
	{
		return Latest.Windows.Where((RunningWindow w) => Matches(app, w)).ToList();
	}

	private static string ProcessPath(uint id, out string appId)
	{
		appId = null;
		IntPtr intPtr = OpenProcess(4096u, inherit: false, id);
		if (intPtr == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(32768);
			uint size = 32768u;
			string result = (QueryFullProcessImageName(intPtr, 0u, stringBuilder, ref size) ? stringBuilder.ToString() : null);
			uint size2 = 0u;
			GetApplicationUserModelId(intPtr, ref size2, null);
			if (size2 != 0 && size2 < 4096)
			{
				StringBuilder stringBuilder2 = new StringBuilder((int)size2);
				if (GetApplicationUserModelId(intPtr, ref size2, stringBuilder2) == 0)
				{
					appId = stringBuilder2.ToString();
				}
			}
			return result;
		}
		finally
		{
			CloseHandle(intPtr);
		}
	}

	public static WindowSnapshot Capture()
	{
		WindowSnapshot snapshot = new WindowSnapshot();
		int self = Process.GetCurrentProcess().Id;
		Dictionary<uint, string> paths = new Dictionary<uint, string>();
		Dictionary<uint, string> appIds = new Dictionary<uint, string>();
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			using (process)
			{
				try
				{
					if (process.Id != self)
					{
						string appId;
						string text = ProcessPath((uint)process.Id, out appId);
						paths[(uint)process.Id] = text;
						appIds[(uint)process.Id] = appId;
						snapshot.Processes.Add(new RunningProcess
						{
							Id = process.Id,
							Name = process.ProcessName,
							Executable = text
						});
					}
				}
				catch
				{
				}
			}
		}
		Native.EnumWindows(delegate(IntPtr h, IntPtr unused)
		{
			GetWindowThreadProcessId(h, out var pid);
			if (pid == self)
			{
				return true;
			}
			long num = GetWindowLongPtr(h, -20).ToInt64();
			if ((num & 0x80) != 0L || ((num & 0x40000) == 0L && GetWindow(h, 4u) != IntPtr.Zero))
			{
				return true;
			}
			StringBuilder stringBuilder = new StringBuilder(1024);
			if (GetWindowText(h, stringBuilder, stringBuilder.Capacity) == 0)
			{
				return true;
			}
			if (DwmGetWindowAttribute(h, 14, out var value, 4) == 0 && value != 0)
			{
				return true;
			}
			StringBuilder stringBuilder2 = new StringBuilder(256);
			GetClassName(h, stringBuilder2, stringBuilder2.Capacity);
			if (new string[4] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" }.Contains(stringBuilder2.ToString()))
			{
				return true;
			}
			if (!GetWindowRect(h, out var rect) || rect.Right - rect.Left < 80 || rect.Bottom - rect.Top < 40)
			{
				return true;
			}
			paths.TryGetValue(pid, out var value2);
			appIds.TryGetValue(pid, out var value3);
			RunningProcess runningProcess = snapshot.Processes.FirstOrDefault((RunningProcess p) => p.Id == pid);
			RunningWindow runningWindow = new RunningWindow
			{
				Handle = h.ToInt64(),
				ProcessId = (int)pid,
				Title = stringBuilder.ToString(),
				Executable = value2,
				AppId = value3,
				ProcessName = ((runningProcess == null) ? "" : runningProcess.Name),
				Visible = Native.IsWindowVisible(h)
			};
			if (runningWindow.Visible)
			{
				snapshot.Windows.Add(runningWindow);
			}
			else if (stringBuilder2.ToString().IndexOf("WeChat", StringComparison.OrdinalIgnoreCase) >= 0 || stringBuilder2.ToString().IndexOf("mmui", StringComparison.OrdinalIgnoreCase) >= 0 || runningWindow.Title == "微信")
			{
				snapshot.Hidden.Add(runningWindow);
			}
			return true;
		}, IntPtr.Zero);
		return snapshot;
	}

	public static bool IsRunning(Entry app)
	{
		string exe = Executable(app);
		return Latest.Processes.Any((RunningProcess p) => Family(exe) == Family(p.Executable ?? p.Name) || (exe.IndexOf('\\') < 0 && Path.GetFileNameWithoutExtension(exe).Equals(p.Name, StringComparison.OrdinalIgnoreCase)));
	}

	public static bool Activate(RunningWindow window)
	{
		IntPtr intPtr = new IntPtr(window.Handle);
		if (!IsWindow(intPtr))
		{
			return false;
		}
		IntPtr lastActivePopup = GetLastActivePopup(intPtr);
		if (lastActivePopup != IntPtr.Zero && lastActivePopup != intPtr && Native.IsWindowVisible(lastActivePopup))
		{
			intPtr = lastActivePopup;
		}
		if (IsIconic(intPtr))
		{
			ShowWindowAsync(intPtr, 9);
		}
		else if (!Native.IsWindowVisible(intPtr))
		{
			ShowWindowAsync(intPtr, 5);
		}
		return SetForegroundWindow(intPtr);
	}

	public static RunningWindow SelectWindow(Entry app, WindowSnapshot snapshot)
	{
		return snapshot.Windows.FirstOrDefault((RunningWindow window) => Matches(app, window)) ?? snapshot.Hidden.FirstOrDefault((RunningWindow window) => Matches(app, window));
	}

	public static void Open(Program p, Entry app)
	{
		Latest = Capture();
		RunningWindow runningWindow = SelectWindow(app, Latest);
		if (runningWindow != null && IsWindow(new IntPtr(runningWindow.Handle)))
		{
			if (Activate(runningWindow))
			{
				p.RecordLaunch(app);
			}
		}
		else
		{
			p.LaunchNew(app);
		}
	}

	public static void NoActivate(Window window)
	{
		window.ShowActivated = false;
		window.SourceInitialized += delegate
		{
			IntPtr handle = new WindowInteropHelper(window).Handle;
			SetWindowLongPtr(handle, -20, new IntPtr(GetWindowLongPtr(handle, -20).ToInt64() | 0x8000000));
		};
	}
}
