using System.Windows;

namespace Luma;

public static class WindowSizePolicy
{
	private static bool Valid(double value, double minimum)
	{
		if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum)
		{
			return value <= 10000.0;
		}
		return false;
	}

	public static Size Normalize(double width, double height)
	{
		return new Size(Valid(width, 500.0) ? width : 1440.0, Valid(height, 420.0) ? height : 920.0);
	}

	public static bool CanSave(Size size, WindowState state, bool ready, bool userSized, bool placing)
	{
		if (ready && userSized && !placing && state == WindowState.Normal && Valid(size.Width, 500.0))
		{
			return Valid(size.Height, 420.0);
		}
		return false;
	}

	public static bool ToggleCloses(bool visible, WindowState state)
	{
		if (visible)
		{
			return state != WindowState.Minimized;
		}
		return false;
	}

	public static WindowState RestoreState(WindowState current, WindowState previous)
	{
		if (current != WindowState.Minimized)
		{
			return current;
		}
		if (previous != WindowState.Maximized)
		{
			return WindowState.Normal;
		}
		return WindowState.Maximized;
	}
}
