using System.Windows;
using System.Windows.Controls;

namespace Luma;

internal static class GridPlacement
{
	public static T AtColumn<T>(this T element, int column) where T : UIElement
	{
		Grid.SetColumn(element, column);
		return element;
	}
}
