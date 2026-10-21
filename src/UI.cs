using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Luma;

public static class UI
{
	public static Brush Ink = Brush("#211E29");

	public static Brush Muted = Brush("#71697C");

	public static Brush Accent = Brush("#7B5BD1");

	public static Brush Panel = Brush("#F7F8FA");

	private static Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>();

	public static void InvalidateIcons()
	{
		cache.Clear();
	}

	public static Brush Brush(string color)
	{
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
	}

	public static bool Visible(Program p, Entry a)
	{
		return p.State.IsVisible(a);
	}

	public static TextBlock Text(string text, double size, Brush color)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = Math.Max(12.0, size);
		textBlock.FontFamily = AirStyle.Font;
		if (size >= 20.0)
		{
			textBlock.FontWeight = FontWeights.SemiBold;
		}
		textBlock.Foreground = color;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		return textBlock;
	}

	public static ControlTemplate ButtonTemplate(double radius)
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.Name = "surface";
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
		frameworkElementFactory.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.PaddingProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new Binding("HorizontalContentAlignment")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		ControlTemplate obj = new ControlTemplate(typeof(Button))
		{
			VisualTree = frameworkElementFactory
		};
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82, "surface"));
		obj.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = UIElement.IsKeyboardFocusedProperty,
			Value = true
		};
		trigger2.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("Luma.Line"), "surface"));
		trigger2.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1.0), "surface"));
		obj.Triggers.Add(trigger2);
		Trigger trigger3 = new Trigger
		{
			Property = ButtonBase.IsPressedProperty,
			Value = true
		};
		trigger3.Setters.Add(new Setter(UIElement.OpacityProperty, 0.64, "surface"));
		obj.Triggers.Add(trigger3);
		Trigger trigger4 = new Trigger
		{
			Property = UIElement.IsEnabledProperty,
			Value = false
		};
		trigger4.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "surface"));
		obj.Triggers.Add(trigger4);
		return obj;
	}

	public static Button Button(string text, Action action)
	{
		Button button = new Button();
		button.Content = text;
		button.HorizontalContentAlignment = HorizontalAlignment.Center;
		button.Foreground = Ink;
		button.FontSize = 14.0;
		button.FontFamily = AirStyle.Font;
		button.Background = FluentTheme.Card;
		button.BorderBrush = FluentTheme.Line;
		button.BorderThickness = new Thickness(1.0);
		button.Padding = new Thickness(12.0, 10.0, 12.0, 10.0);
		button.Margin = new Thickness(3.0);
		button.Cursor = Cursors.Hand;
		button.Template = ButtonTemplate(11.0);
		switch (text)
		{
		case "确定":
		case "保存":
		case "完成":
		case "添加到网站分组":
			button.Background = Ink;
			button.Foreground = FluentTheme.Card;
			button.FontWeight = FontWeights.SemiBold;
			break;
		}
		button.Click += delegate
		{
			action();
		};
		return button;
	}

	public static Button Glyph(string glyph, string tooltip, Action action)
	{
		Button button = Button("", action);
		button.Content = AirStyle.Glyph(glyph, tooltip);
		button.FontFamily = new FontFamily("Segoe MDL2 Assets");
		button.FontSize = 16.0;
		button.Width = 36.0;
		button.Height = 36.0;
		button.Padding = new Thickness(0.0);
		button.Background = Brushes.Transparent;
		button.BorderThickness = new Thickness(0.0);
		button.ToolTip = tooltip;
		AutomationProperties.SetName(button, tooltip);
		return button;
	}

	public static Border Card(UIElement child, double radius)
	{
		return new Border
		{
			Child = child,
			CornerRadius = new CornerRadius(radius),
			Background = FluentTheme.Card,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(16.0),
			Effect = null
		};
	}

	public static void Window(Window w)
	{
		Glass.Configure(w);
		w.FontFamily = AirStyle.Font;
		w.Foreground = Ink;
		w.UseLayoutRounding = true;
		w.SnapsToDevicePixels = true;
		TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
		TextOptions.SetTextRenderingMode(w, TextRenderingMode.ClearType);
		TextOptions.SetTextHintingMode(w, TextHintingMode.Fixed);
		RenderOptions.SetClearTypeHint(w, ClearTypeHint.Enabled);
	}

	public static FrameworkElement Icon(Entry a, double size)
	{
		string key = a.CustomIcon ?? a.ImagePath ?? a.IconPath ?? a.Target ?? a.Id ?? a.Name ?? "unknown";
		if (!cache.TryGetValue(key, out var value))
		{
			value = IconLoader.Load(a);
			if (value != null)
			{
				cache[key] = value;
			}
		}
		if (value != null)
		{
			Image result = new Image
			{
				Source = value,
				Width = size,
				Height = size,
				Stretch = Stretch.Uniform
			};
			if (string.IsNullOrEmpty(a.CustomIcon) && (IsWhiteIcon(value) || (FluentTheme.Dark && IsDarkIcon(value))))
			{
				return new Border
				{
					Width = size,
					Height = size,
					CornerRadius = new CornerRadius(size * 0.23),
					Background = Brush((FluentTheme.Dark && IsDarkIcon(value)) ? "#F4F3F8" : "#64748A"),
					Child = new Image
					{
						Source = value,
						Width = size * 0.76,
						Height = size * 0.76,
						Stretch = Stretch.Uniform
					}
				};
			}
			return result;
		}
		return new Border
		{
			Width = size,
			Height = size,
			CornerRadius = new CornerRadius(size * 0.24),
			Background = Brush("#E7ECF5"),
			Child = new TextBlock
			{
				Text = (a.Website ? "\ue774" : "\ue71d"),
				FontFamily = new FontFamily("Segoe MDL2 Assets"),
				FontSize = size * 0.46,
				Foreground = Brush("#7D91AD"),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
	}

	private static bool IsWhiteIcon(ImageSource source)
	{
		try
		{
			if (!(source is BitmapSource source2))
			{
				return false;
			}
			FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(source2, PixelFormats.Bgra32, null, 0.0);
			int num = formatConvertedBitmap.PixelWidth * 4;
			byte[] array = new byte[num * formatConvertedBitmap.PixelHeight];
			formatConvertedBitmap.CopyPixels(array, num, 0);
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < array.Length; i += 4)
			{
				if (array[i + 3] >= 80)
				{
					num2++;
					if (array[i] > 220 && array[i + 1] > 220 && array[i + 2] > 220)
					{
						num3++;
					}
				}
			}
			return num2 > 0 && (double)num3 / (double)num2 > 0.85;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsDarkIcon(ImageSource source)
	{
		try
		{
			if (!(source is BitmapSource source2))
			{
				return false;
			}
			FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(source2, PixelFormats.Bgra32, null, 0.0);
			int num = formatConvertedBitmap.PixelWidth * 4;
			byte[] array = new byte[num * formatConvertedBitmap.PixelHeight];
			formatConvertedBitmap.CopyPixels(array, num, 0);
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < array.Length; i += 4)
			{
				if (array[i + 3] >= 80)
				{
					num2++;
					if (array[i] < 60 && array[i + 1] < 60 && array[i + 2] < 60)
					{
						num3++;
					}
				}
			}
			return num2 > 0 && (double)num3 / (double)num2 > 0.85;
		}
		catch
		{
			return false;
		}
	}

	private static string DisplayName(Program p, Entry a)
	{
		if (p.State.Apps.Count((Entry x) => x.Name.Equals(a.Name, StringComparison.CurrentCultureIgnoreCase)) < 2)
		{
			return a.Name;
		}
		try
		{
			string text = (a.Id ?? "").Split(new string[1] { " | " }, StringSplitOptions.None)[0];
			if (File.Exists(text))
			{
				FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(text);
				if (versionInfo.FileMajorPart > 0)
				{
					return a.Name + " " + versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart;
				}
			}
		}
		catch
		{
		}
		return a.Name;
	}

	public static Button AppButton(Program p, Entry a, bool label)
	{
		if (label)
		{
			return FluentCard(p, a);
		}
		double num = Math.Max(40.0, Math.Min(96.0, p.State.DockIconSize));
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(Icon(a, label ? 56.0 : num));
		if (!label && WindowController.ForApp(a).Count > 0)
		{
			stackPanel.Children.Add(new Border
			{
				Width = 4.0,
				Height = 4.0,
				Background = Accent,
				CornerRadius = new CornerRadius(2.0),
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
			});
		}
		if (label && a.TopPinned)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = "置顶",
				FontSize = 9.0,
				Foreground = Accent,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 3.0, 0.0, -5.0)
			});
		}
		if (label)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = DisplayName(p, a),
				Foreground = Ink,
				FontSize = 12.0,
				TextAlignment = TextAlignment.Center,
				TextTrimming = TextTrimming.CharacterEllipsis,
				Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
				Width = 104.0
			});
		}
		Button button = new Button
		{
			Content = stackPanel,
			Width = (label ? 112.0 : (num + 18.0)),
			Height = (label ? 124.0 : (num + 22.0)),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Margin = new Thickness((!label) ? 1 : 3),
			Padding = new Thickness(3.0),
			ToolTip = a.Name,
			Cursor = Cursors.Hand,
			Template = ButtonTemplate(17.0),
			RenderTransformOrigin = new Point(0.5, 0.5)
		};
		ScaleTransform scale = new ScaleTransform(1.0, 1.0);
		button.RenderTransform = scale;
		button.MouseEnter += delegate
		{
			if (FluentTheme.Motion)
			{
				scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(130.0)));
				scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(130.0)));
			}
		};
		button.MouseLeave += delegate
		{
			if (FluentTheme.Motion)
			{
				scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(130.0)));
				scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(130.0)));
			}
		};
		button.Click += delegate
		{
			p.Launch(a);
		};
		ContextMenu menu = new ContextMenu
		{
			FontFamily = AirStyle.Font,
			FontSize = 12.0
		};
		Action<string, Action> action = delegate(string text, Action action2)
		{
			MenuItem menuItem3 = new MenuItem
			{
				Header = text
			};
			menuItem3.Click += delegate
			{
				menu.IsOpen = false;
				action2();
			};
			menu.Items.Add(menuItem3);
		};
		action(a.Website ? "打开网站" : (a.FavoriteFolder ? "打开文件夹" : "启动新实例"), delegate
		{
			if (p.State.ReplaceStartMenu)
			{
				p.DismissLibrary();
			}
			p.LaunchNew(a);
		});
		action(a.TopPinned ? "取消置顶" : "置顶应用", delegate
		{
			a.TopPinned = !a.TopPinned;
			p.Refresh();
		});
		action("更换图标图片…", delegate
		{
			if (p.Library != null)
			{
				p.Library.ChooseIcon(a);
			}
		});
		if (!string.IsNullOrEmpty(a.CustomIcon))
		{
			action("恢复原图标", delegate
			{
				a.CustomIcon = null;
				InvalidateIcons();
				p.Refresh();
			});
		}
		if (label)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = "移动到分类"
			};
			foreach (string group in p.State.Groups)
			{
				string g = group;
				MenuItem menuItem2 = new MenuItem
				{
					Header = g,
					IsCheckable = true,
					IsChecked = (a.Group == g)
				};
				menuItem2.Click += delegate
				{
					if (a.Group != g)
					{
						a.Group = g;
						a.SortOrder = int.MaxValue;
					}
					p.Refresh();
				};
				menuItem.Items.Add(menuItem2);
			}
			menu.Items.Add(menuItem);
		}
		if (!a.Website)
		{
			action("打开所在位置", delegate
			{
				try
				{
					string text = a.IconPath ?? a.Target;
					if (Directory.Exists(text))
					{
						Process.Start("explorer.exe", "\"" + text + "\"");
					}
					else if (File.Exists(text))
					{
						Process.Start("explorer.exe", "/select,\"" + text + "\"");
					}
				}
				catch
				{
				}
			});
		}
		action("从应用库移除", delegate
		{
			p.RemoveEntry(a);
		});
		menu.Opened += delegate
		{
			if (p.Library != null)
			{
				p.Library.TrackContext(menu);
			}
		};
		menu.Closed += delegate
		{
			if (p.Library != null)
			{
				p.Library.EndContext(menu);
			}
		};
		button.ContextMenu = menu;
		return button;
	}

	public static string EntryName(Entry app)
	{
		if (!string.IsNullOrWhiteSpace(app.CustomName))
		{
			return app.CustomName;
		}
		return app.Name;
	}

	public static Button FluentCard(Program p, Entry app, bool forceGrid = false)
	{
		bool libraryListView = p.State.LibraryListView && !forceGrid;
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = (libraryListView ? new GridLength(64.0) : new GridLength(1.0, GridUnitType.Star))
		});
		if (libraryListView)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition());
		}
		FrameworkElement frameworkElement = Icon(app, libraryListView ? 40 : 52);
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.Margin = (libraryListView ? new Thickness(0.0) : new Thickness(0.0, 8.0, 0.0, 27.0));
		grid.Children.Add(frameworkElement);
		TextBlock textBlock = Text(EntryName(app), 13.0, Ink);
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		textBlock.ToolTip = EntryName(app);
		textBlock.HorizontalAlignment = ((!libraryListView) ? HorizontalAlignment.Center : HorizontalAlignment.Stretch);
		textBlock.VerticalAlignment = (libraryListView ? VerticalAlignment.Center : VerticalAlignment.Bottom);
		textBlock.Margin = (libraryListView ? new Thickness(6.0, 0.0, 30.0, 0.0) : new Thickness(2.0, 0.0, 2.0, 4.0));
		textBlock.MaxWidth = (libraryListView ? double.PositiveInfinity : 106.0);
		if (libraryListView)
		{
			Grid.SetColumn(textBlock, 1);
		}
		grid.Children.Add(textBlock);
		if (app.Favorite)
		{
			FrameworkElement frameworkElement2 = AirStyle.Icon("bookmark", Accent, 14.0);
			frameworkElement2.HorizontalAlignment = HorizontalAlignment.Left;
			frameworkElement2.VerticalAlignment = VerticalAlignment.Top;
			frameworkElement2.Margin = new Thickness(2.0);
			grid.Children.Add(frameworkElement2);
		}
		Button button = new Button
		{
			Content = grid,
			Tag = app,
			Width = (libraryListView ? double.NaN : 132.0),
			Height = (libraryListView ? 64 : 126),
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			Background = FluentTheme.Card,
			Foreground = Ink,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(0.5),
			Padding = new Thickness(10.0),
			Margin = new Thickness(0.0, 0.0, 16.0, 16.0),
			Template = ButtonTemplate(18.0),
			ToolTip = EntryName(app),
			RenderTransform = new TranslateTransform()
		};
		TranslateTransform lift = (TranslateTransform)button.RenderTransform;
		DropShadowEffect shadow = new DropShadowEffect
		{
			Color = (FluentTheme.Dark ? Colors.Black : Color.FromRgb(28, 54, 95)),
			Opacity = (FluentTheme.Dark ? 0.08 : 0.025),
			BlurRadius = 16.0,
			ShadowDepth = 3.0,
			RenderingBias = RenderingBias.Performance
		};
		button.Effect = shadow;
		AutomationProperties.SetName(button, EntryName(app));
		AutomationProperties.SetHelpText(button, "单击打开应用；拖动到左侧分组即可移动");
		ContextMenu menu = new ContextMenu
		{
			Background = FluentTheme.Card,
			Foreground = Ink,
			FontFamily = AirStyle.Font,
			FontSize = 13.0
		};
		Action<string, Action> action = delegate(string text, Action action2)
		{
			MenuItem menuItem3 = new MenuItem
			{
				Header = text
			};
			menuItem3.Click += delegate
			{
				menu.IsOpen = false;
				action2();
			};
			menu.Items.Add(menuItem3);
		};
		action(app.Website ? "打开网站" : (app.FavoriteFolder ? "打开文件夹" : "打开应用"), delegate
		{
			p.Launch(app);
		});
		if (!app.Website && !app.FavoriteFolder)
		{
			action("启动新实例", delegate
			{
				p.LaunchNew(app);
			});
			action("以管理员身份运行", delegate
			{
				try
				{
					Process.Start(new ProcessStartInfo(app.Target)
					{
						UseShellExecute = true,
						Verb = "runas"
					});
					p.RecordLaunch(app);
				}
				catch (Win32Exception ex)
				{
					if (ex.NativeErrorCode != 1223)
					{
						AirStyle.Notice(ex.Message, "启动应用");
					}
				}
			});
		}
		action(app.Favorite ? "取消收藏" : "添加到收藏", delegate
		{
			app.Favorite = !app.Favorite;
			p.Refresh();
		});
		action(app.TopPinned ? "取消置顶" : "置顶应用", delegate
		{
			app.TopPinned = !app.TopPinned;
			p.Refresh();
		});
		MenuItem menuItem = new MenuItem
		{
			Header = "移动到分类"
		};
		foreach (string group in p.State.Groups)
		{
			string name = group;
			MenuItem menuItem2 = new MenuItem
			{
				Header = name,
				IsCheckable = true,
				IsChecked = (app.Group == name)
			};
			menuItem2.Click += delegate
			{
				p.State.MoveEntryToGroup(app, name);
				p.Refresh();
			};
			menuItem.Items.Add(menuItem2);
		}
		menu.Items.Add(menuItem);
		action("重命名…", delegate
		{
			if (p.Library != null)
			{
				p.Library.RenameEntry(app);
			}
		});
		action("编辑快捷方式 / 标签…", delegate
		{
			if (p.Library != null)
			{
				p.Library.EditEntry(app);
			}
		});
		action("更换图标图片…", delegate
		{
			if (p.Library != null)
			{
				p.Library.ChooseIcon(app);
			}
		});
		if (!string.IsNullOrEmpty(app.CustomIcon))
		{
			action("恢复原图标", delegate
			{
				app.CustomIcon = null;
				InvalidateIcons();
				p.Refresh();
			});
		}
		if (!app.Website)
		{
			action("打开文件位置", delegate
			{
				try
				{
					string text = (app.FavoriteFolder ? app.Target : (app.IconPath ?? app.Target));
					Process.Start("explorer.exe", Directory.Exists(text) ? ("\"" + text + "\"") : ("/select,\"" + text + "\""));
				}
				catch (Exception ex)
				{
					AirStyle.Notice(ex.Message, "文件位置");
				}
			});
		}
		action("删除快捷入口", delegate
		{
			if (!app.FavoriteFolder && !app.Website && !p.State.ExcludedIds.Contains(app.Id))
			{
				p.State.ExcludedIds.Add(app.Id);
			}
			p.RemoveEntry(app);
		});
		menu.Opened += delegate
		{
			if (p.Library != null)
			{
				p.Library.TrackContext(menu);
			}
		};
		menu.Closed += delegate
		{
			if (p.Library != null)
			{
				p.Library.EndContext(menu);
			}
		};
		button.ContextMenu = menu;
		button.Click += delegate(object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource == button && p.Library != null && p.Library.CanOpenShortcut && !p.Library.HandleCommandAppClick(app))
			{
				p.Library.SelectCard(button);
				if (!menu.IsOpen)
				{
					p.Launch(app);
				}
			}
		};
		button.MouseEnter += delegate
		{
			if (FluentTheme.Motion)
			{
				lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-2.0, TimeSpan.FromMilliseconds(140.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
				shadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.05, TimeSpan.FromMilliseconds(140.0)));
			}

		};
		button.MouseLeave += delegate
		{
			TranslateTransform translateTransform = lift;
			if (FluentTheme.Motion)
			{
				translateTransform?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(140.0)));
				shadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(FluentTheme.Dark ? 0.12 : 0.05, TimeSpan.FromMilliseconds(140.0)));
			}

		};
		if (p.Library != null)
		{
			ReorderPanel.AttachExternalSource(button, app, p, p.Library);
			p.Library.AttachAppFolderDrop(button, app);
		}
		return button;
	}

	public static bool? ShowModal(Window window)
	{
		AirStyle.PrepareDialog(window);
		return window.ShowDialog();
	}
}
