using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Luma;

public static class Glass
{
	private struct Margins
	{
		public int Left;

		public int Right;

		public int Top;

		public int Bottom;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);

	[DllImport("dwmapi.dll")]
	private static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref Margins margins);

	public static void Configure(Window window)
	{
		window.WindowStyle = WindowStyle.None;
		window.AllowsTransparency = false;
		window.Background = FluentTheme.Background;
		WindowChrome.SetWindowChrome(window, new WindowChrome
		{
			CaptionHeight = 0.0,
			ResizeBorderThickness = new Thickness(0.0),
			GlassFrameThickness = new Thickness(0.0),
			UseAeroCaptionButtons = false,
			CornerRadius = new CornerRadius(18.0)
		});
		window.SourceInitialized += delegate
		{
			IntPtr handle = new WindowInteropHelper(window).Handle;
			HwndSource hwndSource = HwndSource.FromHwnd(handle);
			if (hwndSource != null)
			{
				hwndSource.CompositionTarget.BackgroundColor = FluentTheme.Background.Color;
			}
			Margins margins = new Margins
			{
				Left = 0,
				Right = 0,
				Top = 0,
				Bottom = 0
			};
			DwmExtendFrameIntoClientArea(handle, ref margins);
			int value = 2;
			DwmSetWindowAttribute(handle, 33, ref value, 4);
			int value2 = 0;
			DwmSetWindowAttribute(handle, 20, ref value2, 4);
			Program program = Application.Current as Program;
			Apply(window, (program == null) ? new Config() : program.State);
		};
	}

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr handle);

	[DllImport("user32.dll")]
	private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

	public static void Round(Window window, double radius)
	{
		IntPtr handle = new WindowInteropHelper(window).Handle;
		if (!(handle == IntPtr.Zero) && !(window.ActualWidth <= 0.0) && !(window.ActualHeight <= 0.0))
		{
			double m = HwndSource.FromHwnd(handle).CompositionTarget.TransformToDevice.M11;
			double num = Math.Max(0.0, Math.Min(radius, Math.Min(window.ActualWidth, window.ActualHeight) / 2.0));
			IntPtr intPtr = CreateRoundRectRgn(0, 0, (int)(window.ActualWidth * m) + 1, (int)(window.ActualHeight * m) + 1, (int)(num * 2.0 * m), (int)(num * 2.0 * m));
			if (SetWindowRgn(handle, intPtr, redraw: true) == 0)
			{
				DeleteObject(intPtr);
			}
		}
	}

	public static void Apply(Window window, Config state)
	{
		IntPtr handle = new WindowInteropHelper(window).Handle;
		if (handle != IntPtr.Zero) ApplyLibrary(window, handle);
	}
	private static void ApplyLibrary(Window window, IntPtr hwnd)
	{
		try
		{
			int value = (FluentTheme.Dark ? 1 : 0);
			DwmSetWindowAttribute(hwnd, 20, ref value, 4);
			int value2 = 1;
			DwmSetWindowAttribute(hwnd, 38, ref value2, 4);
			Margins margins = default(Margins);
			DwmExtendFrameIntoClientArea(hwnd, ref margins);
		}
		catch (Exception exception)
		{
			CrashLog.Write("native-material-fallback", exception);
		}
		HwndSource hwndSource = HwndSource.FromHwnd(hwnd);
		if (hwndSource?.CompositionTarget != null)
		{
			hwndSource.CompositionTarget.BackgroundColor = FluentTheme.Background.Color;
		}
		window.Background = FluentTheme.Background;
		if (window.Content is Border border)
		{
			border.Background = ((window is Library) ? FluentTheme.Background : FluentTheme.Card);
			border.BorderBrush = FluentTheme.Line;
			border.CornerRadius = new CornerRadius(24.0);
		}
	}
}
