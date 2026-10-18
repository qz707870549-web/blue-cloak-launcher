using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Luma;

public static class AirStyle
{
	public static readonly FontFamily Font = (File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/fonts/NotoSansSC-Regular.ttf")) ? new FontFamily(new Uri(AppDomain.CurrentDomain.BaseDirectory), "./assets/fonts/#Noto Sans SC") : new FontFamily("Microsoft YaHei UI"));

	private static readonly Dictionary<string, string> Symbols = new Dictionary<string, string>
	{
		{ "grid", "M5,3 H9 Q10,3 10,4 V9 Q10,10 9,10 H4 Q3,10 3,9 V4 Q3,3 5,3 M16,3 H20 Q21,3 21,4 V9 Q21,10 20,10 H15 Q14,10 14,9 V4 Q14,3 16,3 M5,14 H9 Q10,14 10,15 V20 Q10,21 9,21 H4 Q3,21 3,20 V15 Q3,14 5,14 M16,14 H20 Q21,14 21,15 V20 Q21,21 20,21 H15 Q14,21 14,20 V15 Q14,14 16,14" },
		{ "folder", "M3,7 Q3,5 5,5 H9 L11,7 H19 Q21,7 21,9 V19 Q21,21 19,21 H5 Q3,21 3,19 Z" },
		{ "bookmark", "M7,3 H17 Q19,3 19,5 V21 L12,17 5,21 V5 Q5,3 7,3 Z" },
		{ "settings", "M3,6 H8 M12,6 H21 M3,12 H13 M17,12 H21 M3,18 H6 M10,18 H21 M10,4 A2,2 0 1 0 10,8 A2,2 0 1 0 10,4 M15,10 A2,2 0 1 0 15,14 A2,2 0 1 0 15,10 M8,16 A2,2 0 1 0 8,20 A2,2 0 1 0 8,16" },
		{ "plus", "M12,5 V19 M5,12 H19" },
		{ "close", "M6,6 L18,18 M18,6 L6,18" },
		{ "minus", "M5,12 H19" },
		{ "expand", "M5,4 H19 Q20,4 20,5 V19 Q20,20 19,20 H5 Q4,20 4,19 V5 Q4,4 5,4" },
		{ "search", "M10.5,3 A7.5,7.5 0 1 0 10.5,18 A7.5,7.5 0 1 0 10.5,3 M16,16 L21,21" },
		{ "clock", "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,7 V12 L16,14" },
		{ "globe", "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M3,12 H21 M12,3 C6,8 6,16 12,21 C18,16 18,8 12,3" },
		{ "download", "M12,3 V15 M8,11 L12,15 16,11 M4,16 V19 Q4,21 6,21 H18 Q20,21 20,19 V16" },
		{ "refresh", "M4,9 A8.5,8.5 0 1 1 5,18 M3,4 V10 H9" },
		{ "sun", "M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M12,2 V4 M12,20 V22 M2,12 H4 M20,12 H22 M5,5 L6.5,6.5 M17.5,17.5 L19,19 M5,19 L6.5,17.5 M17.5,6.5 L19,5" },
		{ "more", "M5,12 H5.1 M12,12 H12.1 M19,12 H19.1" },
		{ "list", "M8,5 H21 M8,12 H21 M8,19 H21 M3,5 H3.1 M3,12 H3.1 M3,19 H3.1" },
		{ "lock", "M6,10 H18 Q20,10 20,12 V19 Q20,21 18,21 H6 Q4,21 4,19 V12 Q4,10 6,10 M8,10 V7 A4,4 0 0 1 16,7 V10 M12,14 V17" },
		{ "wifi", "M3,8 Q12,1 21,8 M6,12 Q12,7 18,12 M9,16 Q12,13 15,16 M12,20 H12.1" },
		{ "bluetooth", "M12,3 L18,8 6,17 M12,21 L18,16 6,7 M12,3 V21" },
		{ "pen", "M4,16 L16,4 Q18,2 20,4 Q22,6 20,8 L8,20 3,21 Z M14,6 L18,10" },
		{ "game", "M8,6 H16 Q20,6 21,12 L22,17 Q22,21 18,19 L15,16 H9 L6,19 Q2,21 2,17 L3,12 Q4,6 8,6 M6,10 V14 M4,12 H8 M16,11 H16.1 M19,14 H19.1" },
		{ "book", "M12,5 Q7,2 3,4 V20 Q7,18 12,21 Q17,18 21,20 V4 Q17,2 12,5 V21" },
		{ "monitor", "M5,3 H19 Q21,3 21,5 V16 Q21,18 19,18 H5 Q3,18 3,16 V5 Q3,3 5,3 M12,18 V22 M8,22 H16" },
		{ "check", "M5,12 L10,17 19,7" },
		{ "arrow", "M5,12 H20 M14,6 L20,12 14,18" },
		{ "chevron-right", "M9,5 L16,12 9,19" },
		{ "chevron-down", "M5,9 L12,16 19,9" },
		{ "arrow-up", "M12,20 V4 M6,10 L12,4 18,10" },
		{ "arrow-down", "M12,4 V20 M6,14 L12,20 18,14" },
		{ "arrow-left", "M20,12 H4 M10,6 L4,12 10,18" },
		{ "info", "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,11 V17 M12,7 H12.1" },
		{ "trash", "M3,6 H21 M8,6 V3 H16 V6 M5,6 L6,20 Q6,21 8,21 H16 Q18,21 18,20 L19,6 M10,10 V17 M14,10 V17" },
		{ "keyboard", "M4,5 H20 Q22,5 22,7 V18 Q22,20 20,20 H4 Q2,20 2,18 V7 Q2,5 4,5 M6,9 H6.1 M10,9 H10.1 M14,9 H14.1 M18,9 H18.1 M6,13 H6.1 M10,13 H10.1 M14,13 H14.1 M18,13 H18.1 M7,17 H17" }
	};

	public static FrameworkElement Icon(string key, Brush brush = null, double size = 18.0)
	{
		if (!Symbols.TryGetValue(key, out var value))
		{
			value = Symbols["folder"];
		}
		return new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse(value),
			Stroke = (brush ?? UI.Ink),
			StrokeThickness = 2.0,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			Width = size,
			Height = size,
			Stretch = Stretch.Uniform,
			VerticalAlignment = VerticalAlignment.Center,
			SnapsToDevicePixels = false
		};
	}

	public static FrameworkElement Glyph(string glyph, string label = "")
	{
		string key;
		if (glyph != null)
		{
			int length = glyph.Length;
			if (length == 1)
			{
				switch (glyph[0])
				{
				case '\ue711':
				case '\ue8bb':
					break;
				case '\ue921':
					goto IL_016b;
				case '\ue922':
				case '\ue923':
					goto IL_0176;
				case '\ue721':
					goto IL_0181;
				case '\ue710':
					goto IL_018c;
				case '\ue713':
				case '\ue790':
					goto IL_0197;
				case '\ue708':
					goto IL_01a2;
				case '\ue774':
					goto IL_01ad;
				case '\ue72c':
					goto IL_01b8;
				case '\ue823':
					goto IL_01c3;
				case '\ue734':
					goto IL_01ce;
				case '\ue712':
					goto IL_01d9;
				case '\ue896':
					goto IL_01e4;
				case '\ue8fd':
					goto IL_01ef;
				case '\ue80a':
					goto IL_01f7;
				case '\ue72e':
				case '\ue785':
					goto IL_01ff;
				case '\ue701':
					goto IL_0207;
				case '\ue702':
					goto IL_020f;
				case '\ue765':
					goto IL_0217;
				default:
					goto IL_021f;
				}
				key = "close";
				goto IL_0275;
			}
		}
		goto IL_021f;
		IL_020f:
		key = "bluetooth";
		goto IL_0275;
		IL_021f:
		key = (label.Contains("收藏") ? "bookmark" : (label.Contains("设置") ? "settings" : (label.Contains("网站") ? "globe" : (label.Contains("编辑") ? "pen" : "folder"))));
		goto IL_0275;
		IL_01ef:
		key = "list";
		goto IL_0275;
		IL_016b:
		key = "minus";
		goto IL_0275;
		IL_0176:
		key = "expand";
		goto IL_0275;
		IL_01c3:
		key = "clock";
		goto IL_0275;
		IL_01e4:
		key = "download";
		goto IL_0275;
		IL_01f7:
		key = "grid";
		goto IL_0275;
		IL_01ad:
		key = "globe";
		goto IL_0275;
		IL_01ff:
		key = "lock";
		goto IL_0275;
		IL_01ce:
		key = "bookmark";
		goto IL_0275;
		IL_0217:
		key = "keyboard";
		goto IL_0275;
		IL_0181:
		key = "search";
		goto IL_0275;
		IL_01b8:
		key = "refresh";
		goto IL_0275;
		IL_01a2:
		key = "sun";
		goto IL_0275;
		IL_018c:
		key = "plus";
		goto IL_0275;
		IL_0275:
		return Icon(key);
		IL_01d9:
		key = "more";
		goto IL_0275;
		IL_0197:
		key = "settings";
		goto IL_0275;
		IL_0207:
		key = "wifi";
		goto IL_0275;
	}

	public static void SetSelected(DependencyObject node)
	{
		if (node is TextBlock textBlock)
		{
			textBlock.Foreground = UI.Ink;
		}
		if (node is System.Windows.Shapes.Path path)
		{
			path.Stroke = UI.Ink;
		}
		foreach (DependencyObject item in Children(node))
		{
			SetSelected(item);
		}
	}

	private static IEnumerable<DependencyObject> Children(DependencyObject node)
	{
		return LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>();
	}

	public static void Normalize(DependencyObject node)
	{
		if (node is TextBlock textBlock && !textBlock.FontFamily.Source.Contains("Assets"))
		{
			textBlock.FontFamily = Font;
			if (textBlock.FontSize < 12.0)
			{
				textBlock.FontSize = 12.0;
			}
			if (textBlock.FontSize >= 20.0)
			{
				textBlock.FontWeight = FontWeights.SemiBold;
			}
		}
		Panel panel = node as Panel;
		Border border = node as Border;
		if (border?.Child is TextBlock textBlock2 && textBlock2.FontFamily.Source.Contains("Assets") && textBlock2.Text.Length == 1)
		{
			FrameworkElement frameworkElement = Glyph(textBlock2.Text);
			frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
			border.Child = frameworkElement;
		}
		ContentControl contentControl = node as ContentControl;
		if (contentControl?.Content is TextBlock textBlock3 && textBlock3.FontFamily.Source.Contains("Assets") && textBlock3.Text.Length == 1)
		{
			contentControl.Content = Glyph(textBlock3.Text);
		}
		if (panel != null)
		{
			for (int i = 0; i < panel.Children.Count; i++)
			{
				if (panel.Children[i] is TextBlock textBlock4 && textBlock4.FontFamily.Source.Contains("Assets") && textBlock4.Text.Length == 1)
				{
					FrameworkElement frameworkElement2 = Glyph(textBlock4.Text);
					frameworkElement2.Margin = textBlock4.Margin;
					frameworkElement2.HorizontalAlignment = textBlock4.HorizontalAlignment;
					Grid.SetColumn(frameworkElement2, Grid.GetColumn(textBlock4));
					Grid.SetRow(frameworkElement2, Grid.GetRow(textBlock4));
					panel.Children.RemoveAt(i);
					panel.Children.Insert(i, frameworkElement2);
				}
			}
		}
		foreach (DependencyObject item in Children(node).ToList())
		{
			Normalize(item);
		}
	}

	public static void Enter(FrameworkElement element, double distance = 4.0)
	{
		if (FluentTheme.Motion)
		{
			element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.75, 1.0, TimeSpan.FromMilliseconds(180.0)));
			if (element.RenderTransform == Transform.Identity)
			{
				TranslateTransform translateTransform = (TranslateTransform)(element.RenderTransform = new TranslateTransform());
				translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(distance, 0.0, TimeSpan.FromMilliseconds(180.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
		}
	}

	public static void PrepareDialog(Window window)
	{
		if (window.Tag as string == "airi-dialog")
		{
			return;
		}
		window.Tag = "airi-dialog";
		UI.Window(window);
		window.FontFamily = Font;
		window.Foreground = UI.Ink;
		UIElement uIElement = window.Content as UIElement;
		window.Content = null;
		StackPanel stackPanel = (uIElement as StackPanel) ?? ((uIElement as ScrollViewer)?.Content as StackPanel);
		if (stackPanel != null && stackPanel.Children.Count > 0 && stackPanel.Children[0] is TextBlock textBlock && textBlock.Text == window.Title.Replace("Luma · ", ""))
		{
			stackPanel.Children.RemoveAt(0);
		}
		if (window.SizeToContent == SizeToContent.Manual && !double.IsNaN(window.Height))
		{
			window.Height = Math.Min(window.Height + 76.0, SystemParameters.WorkArea.Height - 32.0);
		}
		DockPanel dockPanel = new DockPanel();
		DockPanel dockPanel2 = new DockPanel
		{
			Margin = new Thickness(24.0, 16.0, 16.0, 16.0),
			LastChildFill = true
		};
		Button element = UI.Glyph("\ue711", "关闭" + window.Title, window.Close);
		DockPanel.SetDock(element, System.Windows.Controls.Dock.Right);
		dockPanel2.Children.Add(element);
		TextBlock textBlock2 = UI.Text(window.Title.Replace("Luma · ", ""), 18.0, UI.Ink);
		textBlock2.FontWeight = FontWeights.SemiBold;
		dockPanel2.Children.Add(textBlock2);
		dockPanel2.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is TextBlock && e.LeftButton == MouseButtonState.Pressed)
			{
				window.DragMove();
			}
		};
		Border element2 = new Border
		{
			Child = dockPanel2,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0)
		};
		DockPanel.SetDock(element2, System.Windows.Controls.Dock.Top);
		dockPanel.Children.Add(element2);
		if (uIElement != null)
		{
			dockPanel.Children.Add((uIElement is ScrollViewer) ? uIElement : new ScrollViewer
			{
				Content = uIElement,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
			});
		}
		window.Content = new Border
		{
			Child = dockPanel,
			Background = FluentTheme.Card,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(20.0),
			ClipToBounds = true
		};
		window.MaxHeight = Math.Max(420.0, SystemParameters.WorkArea.Height - 32.0);
		Normalize(window.Content as DependencyObject);
		window.Loaded += delegate
		{
			Enter((FrameworkElement)window.Content);
		};
	}

	public static void Notice(string message, string title = "Luma")
	{
		Window window = new Window
		{
			Title = title,
			Width = 440.0,
			SizeToContent = SizeToContent.Height,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			ResizeMode = ResizeMode.NoResize
		};
		Window window2 = Application.Current?.Windows.OfType<Window>().FirstOrDefault((Window w) => w.IsActive);
		if (window2 != null)
		{
			window.Owner = window2;
		}
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = message,
			TextWrapping = TextWrapping.Wrap,
			Foreground = UI.Ink,
			FontSize = 14.0,
			LineHeight = 24.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 20.0)
		});
		stackPanel.Children.Add(UI.Button("知道了", window.Close));
		window.Content = stackPanel;
		UI.ShowModal(window);
	}

	public static bool Confirm(string title, string message, string confirm)
	{
		Window window = new Window
		{
			Title = title,
			Width = 460.0,
			SizeToContent = SizeToContent.Height,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			ResizeMode = ResizeMode.NoResize
		};
		Window window2 = Application.Current?.Windows.OfType<Window>().FirstOrDefault((Window w) => w.IsActive);
		if (window2 != null)
		{
			window.Owner = window2;
		}
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = message,
			TextWrapping = TextWrapping.Wrap,
			Foreground = UI.Ink,
			FontSize = 14.0,
			LineHeight = 24.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 20.0)
		});
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		stackPanel2.Children.Add(UI.Button("取消", delegate
		{
			window.DialogResult = false;
		}));
		Button button = UI.Button(confirm, delegate
		{
			window.DialogResult = true;
		});
		button.Background = UI.Ink;
		button.Foreground = FluentTheme.Card;
		stackPanel2.Children.Add(button);
		stackPanel.Children.Add(stackPanel2);
		window.Content = stackPanel;
		return UI.ShowModal(window) == true;
	}

	public static void Resources(ResourceDictionary resources)
	{
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Luma.AiriTheme.xaml");
		if (stream != null)
		{
			ResourceDictionary item = (ResourceDictionary)XamlReader.Load(stream);
			resources.MergedDictionaries.Clear();
			resources.MergedDictionaries.Add(item);
		}
	}
}
