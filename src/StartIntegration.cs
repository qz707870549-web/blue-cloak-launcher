using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;

namespace Luma;

public sealed partial class StartIntegration : IDisposable
{
	private delegate IntPtr MouseCallback(int code, IntPtr message, IntPtr data);

	private delegate void EventCallback(IntPtr hook, uint eventId, IntPtr window, int objectId, int child, uint thread, uint time);

	private delegate bool ChildCallback(IntPtr window, IntPtr data);

	private struct MousePoint
	{
		public int X;

		public int Y;
	}

	private struct MouseData
	{
		public MousePoint Point;

		public uint Mouse;

		public uint Flags;

		public uint Time;

		public UIntPtr Extra;
	}

	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private readonly Program program;

	private readonly Thread thread;

	private readonly MouseCallback callback;

	private readonly EventCallback eventCallback;

	private readonly ManualResetEvent ready = new ManualResetEvent(initialState: false);

	private System.Threading.Timer refresh;

	private System.Windows.Forms.Timer watchdog;

	private IntPtr hook;

	private IntPtr eventHook;

	private uint hookThread;

	private volatile bool disposed;

	private volatile bool enabled;

	private volatile bool dirty = true;

	private bool captured;

	private int reading;

	private long lastRefresh;

	private StartButtonBounds pressed;

	private volatile StartButtonBounds[] buttons = new StartButtonBounds[0];

	public bool Available
	{
		get
		{
			if (hook != IntPtr.Zero)
			{
				return buttons.Length != 0;
			}
			return false;
		}
	}

	public bool Enabled
	{
		get
		{
			return enabled;
		}
		set
		{
			enabled = value;
			dirty = true;
		}
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetWindowsHookEx(int id, MouseCallback callback, IntPtr module, uint thread);

	[DllImport("user32.dll")]
	private static extern bool UnhookWindowsHookEx(IntPtr hook);

	[DllImport("user32.dll")]
	private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr GetModuleHandle(string name);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(IntPtr window, StringBuilder name, int count);

	[DllImport("user32.dll")]
	private static extern IntPtr WindowFromPoint(MousePoint point);

	[DllImport("user32.dll")]
	private static extern IntPtr GetAncestor(IntPtr window, uint flags);

	[DllImport("user32.dll")]
	private static extern bool EnumChildWindows(IntPtr parent, ChildCallback callback, IntPtr data);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

	[DllImport("user32.dll")]
	private static extern bool IsWindow(IntPtr window);

	[DllImport("user32.dll")]
	private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, EventCallback callback, uint process, uint thread, uint flags);

	[DllImport("user32.dll")]
	private static extern bool UnhookWinEvent(IntPtr hook);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr value, IntPtr data);

	public StartIntegration(Program owner)
	{
		program = owner;
		callback = OnMouse;
		keyboardCallback = OnKeyboard;
		eventCallback = OnEvent;
		enabled = owner.State.ReplaceStartMenu;
		thread = new Thread(Run)
		{
			IsBackground = true,
			Name = "Luma start button"
		};
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		ready.WaitOne(2000);
		TimerCallback timerCallback = delegate
		{
			RefreshButtons();
		};
		refresh = new System.Threading.Timer(timerCallback, null, 0, 250);
	}

	private void Run()
	{
		hookThread = GetCurrentThreadId();
		InstallHook();
		InstallKeyboardHook();
		eventHook = SetWinEventHook(32768u, 32779u, IntPtr.Zero, eventCallback, 0u, 0u, 2u);
		watchdog = new System.Windows.Forms.Timer
		{
			Interval = 4000
		};
		watchdog.Tick += delegate
		{
			InstallKeyboardHook();
			if (!captured)
			{
				InstallHook();
			}
		};
		watchdog.Start();
		ready.Set();
		try
		{
			System.Windows.Forms.Application.Run();
		}
		finally
		{
			if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
			keyboardHook = IntPtr.Zero;
			watchdog.Stop();
			watchdog.Dispose();
			if (hook != IntPtr.Zero)
			{
				UnhookWindowsHookEx(hook);
			}
			if (eventHook != IntPtr.Zero)
			{
				UnhookWinEvent(eventHook);
			}
			hook = IntPtr.Zero;
		}
	}

	private void InstallHook()
	{
		if (!disposed)
		{
			if (hook != IntPtr.Zero)
			{
				UnhookWindowsHookEx(hook);
			}
			hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0u);
		}
	}

	private void OnEvent(IntPtr registration, uint eventId, IntPtr window, int objectId, int child, uint owner, uint time)
	{
		if (window != IntPtr.Zero && (eventId == 32768 || eventId == 32769 || eventId == 32772 || eventId == 32779) && (TaskbarClass(ClassName(window)) || TaskbarClass(ClassName(GetAncestor(window, 2u)))))
		{
			dirty = true;
		}
	}

	private static string ClassName(IntPtr window)
	{
		StringBuilder stringBuilder = new StringBuilder(128);
		GetClassName(window, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	private static bool TaskbarClass(string name)
	{
		if (!(name == "Shell_TrayWnd"))
		{
			return name == "Shell_SecondaryTrayWnd";
		}
		return true;
	}

	private static bool NativeStartClass(string name)
	{
		if (!(name == "Start"))
		{
			return name == "StartButton";
		}
		return true;
	}

	public static bool IsStartButton(string automationId, string name)
	{
		if (!(automationId == "StartButton") && !(automationId == "Start") && !(name == "开始") && !(name == "Start"))
		{
			return name == "开始按钮";
		}
		return true;
	}

	private static StartButtonBounds NativeBounds(IntPtr window)
	{
		if (window == IntPtr.Zero || !IsWindow(window) || !Native.IsWindowVisible(window) || !GetWindowRect(window, out var rectangle) || rectangle.Right <= rectangle.Left || rectangle.Bottom <= rectangle.Top)
		{
			return null;
		}
		return new StartButtonBounds
		{
			Left = rectangle.Left,
			Top = rectangle.Top,
			Right = rectangle.Right,
			Bottom = rectangle.Bottom,
			Handle = window.ToInt64()
		};
	}

	public static StartButtonBounds[] Discover()
	{
		List<StartButtonBounds> result = new List<StartButtonBounds>();
		List<IntPtr> taskbars = new List<IntPtr>();
		Native.EnumWindows(delegate(IntPtr window, IntPtr ignored)
		{
			if (TaskbarClass(ClassName(window)) && Native.IsWindowVisible(window))
			{
				taskbars.Add(window);
			}
			return true;
		}, IntPtr.Zero);
		foreach (IntPtr item in taskbars)
		{
			int count = result.Count;
			EnumChildWindows(item, delegate(IntPtr window, IntPtr ignored)
			{
				if (NativeStartClass(ClassName(window)))
				{
					StartButtonBounds startButtonBounds = NativeBounds(window);
					if (startButtonBounds != null)
					{
						result.Add(startButtonBounds);
					}
				}
				return true;
			}, IntPtr.Zero);
			// XAML Start buttons can share a host HWND with the entire taskbar.
			// Their UIA rectangle is authoritative; never refresh it from that HWND.
			try
			{
				AutomationElement automationElement = AutomationElement.FromHandle(item);
				OrCondition orCondition = new OrCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"), new PropertyCondition(AutomationElement.AutomationIdProperty, "Start"), new PropertyCondition(AutomationElement.NameProperty, "开始"), new PropertyCondition(AutomationElement.NameProperty, "Start"), new PropertyCondition(AutomationElement.NameProperty, "开始按钮"));
				AutomationElement automationElement2 = automationElement.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), orCondition));
				if (automationElement2 != null)
				{
					AutomationElement.AutomationElementInformation current2 = automationElement2.Current;
					Rect boundingRectangle = current2.BoundingRectangle;
					if (!current2.IsOffscreen && !boundingRectangle.IsEmpty && boundingRectangle.Width > 0.0 && boundingRectangle.Height > 0.0)
					{
						var bounds = AutomationBounds(boundingRectangle);
						if (bounds != null)
						{
							result.RemoveRange(count, result.Count - count);
							result.Add(bounds);
						}
					}
				}
			}
			catch
			{
			}
		}
		return result.ToArray();
	}

	public static StartButtonBounds AutomationBounds(Rect rectangle)
	{
		if (rectangle.IsEmpty || double.IsNaN(rectangle.X) || double.IsInfinity(rectangle.X) ||
			double.IsNaN(rectangle.Y) || double.IsInfinity(rectangle.Y) ||
			rectangle.Width <= 0 || rectangle.Height <= 0 || rectangle.Width > 256 || rectangle.Height > 256) return null;
		return new StartButtonBounds {
			Left = (int)Math.Floor(rectangle.Left), Top = (int)Math.Floor(rectangle.Top),
			Right = (int)Math.Ceiling(rectangle.Right), Bottom = (int)Math.Ceiling(rectangle.Bottom),
			Handle = 0 // Preserve the UIA element rectangle even when its host HWND moves.
		};
	}

	private void RefreshButtons()
	{
		if (disposed || (uint)(Environment.TickCount - (int)lastRefresh) < (dirty ? 150 : 500) || Interlocked.Exchange(ref reading, 1) != 0)
		{
			return;
		}
		try
		{
			dirty = false;
			StartButtonBounds[] array = Discover();
			buttons = array;
			lastRefresh = Environment.TickCount;
		}
		catch
		{
		}
		finally
		{
			Interlocked.Exchange(ref reading, 0);
		}
	}

	private StartButtonBounds HitTest(MousePoint point)
	{
		StartButtonBounds[] array = buttons;
		foreach (StartButtonBounds startButtonBounds in array)
		{
			StartButtonBounds startButtonBounds2 = ((startButtonBounds.Handle != 0L) ? NativeBounds(new IntPtr(startButtonBounds.Handle)) : startButtonBounds);
			if (startButtonBounds2 != null && startButtonBounds2.Contains(point.X, point.Y) && TaskbarClass(ClassName(GetAncestor(WindowFromPoint(point), 2u))))
			{
				return startButtonBounds2;
			}
		}
		return null;
	}

	private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
	{
		try
		{
			if (code < 0 || disposed)
			{
				return CallNextHookEx(hook, code, message, data);
			}
			int num = message.ToInt32();
			if (num != 513 && num != 514 && num != 516 && num != 519)
			{
				if (num == 517)
				{
					dirty = true;
				}
				return CallNextHookEx(hook, code, message, data);
			}
			MouseData mouseData = (MouseData)Marshal.PtrToStructure(data, typeof(MouseData));
			if (num == 513 || num == 516 || num == 519) { fullscreenKeys.Cancel(); }
			if (num == 514 && captured)
			{
				captured = false;
				StartButtonBounds startButtonBounds = pressed;
				pressed = null;
				StartButtonBounds button = ((startButtonBounds != null && startButtonBounds.Handle != 0L) ? NativeBounds(new IntPtr(startButtonBounds.Handle)) : startButtonBounds);
				if (enabled && button != null && button.Contains(mouseData.Point.X, mouseData.Point.Y))
				{
					program.Dispatcher.BeginInvoke((Action)delegate
					{
						if (!disposed && enabled)
						{
							program.ToggleStartMenu(button);
						}
					});
				}
				return new IntPtr(1);
			}
			if (num == 513 && enabled)
			{
				StartButtonBounds startButtonBounds2 = HitTest(mouseData.Point);
				if (startButtonBounds2 != null)
				{
					pressed = startButtonBounds2;
					captured = true;
					return new IntPtr(1);
				}
			}
			if (num == 513 || num == 516 || num == 519)
			{
				int x = mouseData.Point.X;
				int y = mouseData.Point.Y;
				program.Dispatcher.BeginInvoke((Action)delegate
				{
					if (!disposed)
					{
						program.DismissOutsidePointer(x, y);
					}
				});
			}
		}
		catch
		{
			captured = false;
			pressed = null;
			dirty = true;
		}
		return CallNextHookEx(hook, code, message, data);
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			enabled = false;
			if (refresh != null)
			{
				refresh.Dispose();
			}
			if (hook != IntPtr.Zero)
			{
				UnhookWindowsHookEx(hook);
			}
			if (hookThread != 0)
			{
				PostThreadMessage(hookThread, 18u, UIntPtr.Zero, IntPtr.Zero);
			}
		}
	}
}
