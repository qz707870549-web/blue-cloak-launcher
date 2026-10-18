using System.Windows.Media;

namespace Luma;

internal static class ColorExtensions
{
	public static Color ColorValue(this Brush brush)
	{
		return ((SolidColorBrush)brush).Color;
	}
}
