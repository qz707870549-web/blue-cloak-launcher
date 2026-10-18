using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Luma;

public static class DesktopPlacement
{
	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(System.Drawing.Point point, uint flags);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint horizontal, out uint vertical);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

	public static Rect Bounds(Rect workArea, System.Windows.Point anchor, bool centered, bool top, double scale, double width, double height)
	{
		double num = 12.0 * scale;
		double num2 = Math.Min(width * scale, Math.Max(1.0, workArea.Width - 2.0 * num));
		double num3 = Math.Min(height * scale, Math.Max(1.0, workArea.Height - 2.0 * num));
		double val = (centered ? (workArea.Left + (workArea.Width - num2) / 2.0) : anchor.X);
		val = Math.Max(workArea.Left + num, Math.Min(workArea.Right - num2 - num, val));
		return new Rect(val, top ? (workArea.Top + num) : (workArea.Bottom - num3 - num), num2, num3);
	}

	public static void Place(Library window, Config config, StartButtonBounds button)
	{
		if (window.WindowState != WindowState.Normal)
		{
			return;
		}
		window.PlacingWindow = true;
		try
		{
			System.Drawing.Point point = ((button == null) ? Cursor.Position : new System.Drawing.Point(button.Left, button.Top));
			Rectangle workingArea = Screen.FromPoint(point).WorkingArea;
			if (workingArea.Width < 100 || workingArea.Height < 100)
			{
				return;
			}
			double num = 1.0;
			try
			{
				if (GetDpiForMonitor(MonitorFromPoint(point, 2u), 0, out var horizontal, out var _) == 0 && horizontal != 0)
				{
					num = (double)horizontal / 96.0;
				}
			}
			catch
			{
				PresentationSource presentationSource = PresentationSource.FromVisual(window);
				if (presentationSource != null)
				{
					num = presentationSource.CompositionTarget.TransformToDevice.M11;
				}
			}
			System.Windows.Size size = WindowSizePolicy.Normalize(config.LibraryWidth, config.LibraryHeight);
			Rect rect = Bounds(new Rect(workingArea.Left, workingArea.Top, workingArea.Width, workingArea.Height), new System.Windows.Point(point.X, point.Y), button == null, button != null && button.Top < workingArea.Top, num, size.Width, size.Height);
			IntPtr handle = new WindowInteropHelper(window).Handle;
			SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(rect.Left), (int)Math.Round(rect.Top), 0, 0, 21u);
			window.MinWidth = Math.Min(500.0, rect.Width / num);
			window.MinHeight = Math.Min(420.0, rect.Height / num);
			window.Width = rect.Width / num;
			window.Height = rect.Height / num;
			window.InvalidateMeasure();
			window.UpdateLayout();
			SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(rect.Left), (int)Math.Round(rect.Top), 0, 0, 21u);
		}
		finally
		{
			window.PlacingWindow = false;
		}
	}
}
