using System.Windows;

namespace Luma;

public static class NavigationIcons
{
	public static FrameworkElement Create(string name)
	{
		object key;
		switch (name)
		{
		default:
			key = "folder";
			break;
		case "偏好设置":
			key = "settings";
			break;
		case "网站":
			key = "globe";
			break;
		case "电脑":
		case "系统附带应用":
			key = "monitor";
			break;
		case "喜欢":
		case "收藏":
			key = "bookmark";
			break;
		case "学习":
			key = "book";
			break;
		case "游戏":
			key = "game";
			break;
		case "所有分组":
			key = "grid";
			break;
		}
		return AirStyle.Icon((string)key);
	}
}
