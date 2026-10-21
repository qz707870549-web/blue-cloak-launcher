using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace Luma;

public static class AirSettings
{
	public static void Open(Program program, Window owner, Action manage)
	{
		Window window = new Window
		{
			Title = "蓝斗篷启动 · 设置",
			Owner = owner,
			Width = 760.0,
			Height = 790.0,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			ResizeMode = ResizeMode.CanResize,
			MinWidth = 560.0,
			MinHeight = 480.0
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		TextBlock textBlock = UI.Text("按你的习惯调整工作空间。修改即时生效，应用与分类保持不变。", 13.0, UI.Muted);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 24.0);
		stackPanel.Children.Add(textBlock);
		StackPanel stackPanel2 = Section(stackPanel, "外观", "黑白灰界面，清晰的文字与操作状态。");
		stackPanel2.Children.Add(Label("界面主题"));
		UniformGrid modes = new UniformGrid
		{
			Columns = 3,
			Margin = new Thickness(0.0)
		};
		Action refreshModes = null;
		refreshModes = delegate
		{
			modes.Children.Clear();
			string[] array = new string[3] { "浅色", "深色", "系统" };
			foreach (string text in array)
			{
				string selected2 = text;
				Button button = UI.Button((text == "系统") ? "跟随系统" : text, delegate
				{
					program.State.ThemeMode = selected2;
					Apply(program);
					refreshModes();
				});
				button.MinWidth = 102.0;
				button.BorderThickness = new Thickness(0.0);
				button.Background = Brushes.Transparent;
				if (program.State.ThemeMode == text)
				{
					button.Background = FluentTheme.Card;
					button.Foreground = UI.Ink;
					button.Effect = new DropShadowEffect
					{
						BlurRadius = 3.0,
						ShadowDepth = 1.0,
						Opacity = 0.1
					};
				}
				modes.Children.Add(button);
			}
		};
		refreshModes();
		stackPanel2.Children.Add(new Border
		{
			Child = modes,
			Background = FluentTheme.Background,
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(4.0),
			Margin = new Thickness(0.0, 8.0, 0.0, 18.0)
		});
		stackPanel2.Children.Add(WorkspaceStylePicker(program));
		StackPanel stackPanel3 = Section(stackPanel, "应用库", "用熟悉的方式查找、分类和管理应用。");
		Toggle(stackPanel3, "默认使用列表视图", "关闭时使用图标卡片，名称过长时可悬停查看。", program.State.LibraryListView, delegate(bool v)
		{
			program.State.LibraryListView = v;
			program.Refresh();
		});
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		wrapPanel.Children.Add(UI.Button("管理分组", manage));
		wrapPanel.Children.Add(UI.Button("重新识别应用", program.Scan));
		wrapPanel.Children.Add(UI.Button("导入分组备份", delegate
		{
			BackupUI.Import(program, window);
		}));
		wrapPanel.Children.Add(UI.Button("导出本机分组备份", delegate { BackupUI.Export(program, window); }));
        stackPanel3.Children.Add(wrapPanel);
		StackPanel panel = Section(stackPanel, "窗口与交互", "轻量反馈，不打断当前操作。");
		Toggle(panel, "减少动态效果", "关闭弹层与卡片过渡；同时尊重系统动画偏好。", program.State.ReduceMotion, delegate(bool v)
		{
			program.State.ReduceMotion = v;
			program.Save();
		});
		Toggle(panel, "按 Win 键打开蓝斗篷启动", "桌面和游戏统一接管单按 Win 键；Win 组合键仍转发给系统。", program.State.WinKeyOpensLibrary, delegate(bool v)
		{
			program.State.WinKeyOpensLibrary = v;
			program.Save();
		});
		Toggle(panel, "点击开始按钮打开蓝斗篷启动", "点击开始按钮展开或收起，点击外部自动收起。", program.State.ReplaceStartMenu, delegate(bool v)
		{
			program.SetStartReplacement(v);
		}, !Program.QaMode);
		Toggle(panel, "显示工作空间", "关闭后直接显示应用分组，保留工作空间内容。", program.State.ShowWorkspace, delegate(bool v)
		{
			program.State.ShowWorkspace = v;
			program.Refresh();
		});
		Toggle(panel, "隐藏所有桌面图标", "隐藏桌面的应用、文件夹与系统图标；关闭即可恢复，不删除文件。", program.State.HideDesktopIcons, delegate(bool v)
		{
			if (!DesktopIcons.SetHidden(v))
			{
				throw new InvalidOperationException("未能切换 Windows 桌面图标，请确认资源管理器正常运行后重试。");
			}
			program.State.HideDesktopIcons = v;
			program.Save();
		});
		StackPanel stackPanel4 = Section(stackPanel, "数据与支持", "可导出单文件分组备份；导入前会自动留存当前配置。");
		WrapPanel wrapPanel3 = new WrapPanel();
		wrapPanel3.Children.Add(UI.Button("打开配置文件夹", delegate
		{
			Process.Start("explorer.exe", Program.DataDir);
		}));
		wrapPanel3.Children.Add(UI.Button("恢复已移除入口", delegate
		{
			program.RestoreEntries();
		}));
		Toggle(stackPanel4, "登录 Windows 后启动", "登录任务延迟 10 秒启动、失败重试，并保留备用启动项。", program.State.StartWithWindows ?? StartupRegistration.LegacyEnabled(), delegate(bool v)
		{
			string result = StartupRegistration.Apply(v);
			program.State.StartWithWindows = v;
			program.Save();
			AirStyle.Notice(result, "登录启动");
		}, !Program.QaMode);
		stackPanel4.Children.Add(wrapPanel3);
		TextBlock textBlock2 = UI.Text("蓝斗篷启动  1.3.21 公开版\n启动异常会记录在 data/startup.log。", 12.0, UI.Muted);
		textBlock2.Margin = new Thickness(4.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(textBlock2);
		window.Content = new ScrollViewer
		{
			Content = stackPanel,
			Background = FluentTheme.Background,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		UI.ShowModal(window);
	}

	private static void Apply(Program p)
	{
		FluentTheme.Apply(p.State.ThemeMode);
		p.AppearanceChanged();
		p.Refresh();
	}

	private static TextBlock Label(string text)
	{
		TextBlock textBlock = UI.Text(text, 14.0, UI.Ink);
		textBlock.FontWeight = FontWeights.SemiBold;
		return textBlock;
	}

	public static FrameworkElement WorkspaceStylePicker(Program program)
	{
		var panel = new StackPanel();
		panel.Children.Add(Label("工作空间分组样式"));
		var choices = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 8) };
		Action refresh = null;
		refresh = delegate {
			choices.Children.Clear();
			foreach (string id in new[] { "layered", "minimal" })
			{
				string style = id;
				string label = style == "minimal" ? "简约卡片" : "层叠文件夹";
				var button = UI.Button(label, delegate {
					program.State.WorkspaceFolderStyle = style;
					program.Refresh();
					refresh();
				});
				button.Tag = style;
				button.Background = program.State.WorkspaceFolderStyle == style ? FluentTheme.Selected : FluentTheme.Card;
				AutomationProperties.SetName(button, label);
				choices.Children.Add(button);
			}
		};
		refresh();
		panel.Children.Add(choices);
		return panel;
	}

	private static StackPanel Section(StackPanel target, string title, string description)
	{
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = Label(title);
		textBlock.FontSize = 17.0;
		stackPanel.Children.Add(textBlock);
		TextBlock textBlock2 = UI.Text(description, 12.0, UI.Muted);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.Margin = new Thickness(0.0, 6.0, 0.0, 18.0);
		stackPanel.Children.Add(textBlock2);
		Border border = UI.Card(stackPanel, 16.0);
		border.BorderThickness = new Thickness(1.0);
		border.Padding = new Thickness(20.0);
		border.Margin = new Thickness(0.0, 0.0, 0.0, 16.0);
		target.Children.Add(border);
		return stackPanel;
	}

	private static void Toggle(StackPanel panel, string title, string description, bool selected, Action<bool> change, bool enabled = true)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Label(title));
		TextBlock textBlock = UI.Text(description, 12.0, UI.Muted);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock);
		CheckBox check = new CheckBox
		{
			Content = stackPanel,
			IsChecked = selected,
			IsEnabled = enabled,
			Margin = new Thickness(0.0),
			Padding = new Thickness(0.0, 12.0, 0.0, 12.0),
			VerticalContentAlignment = VerticalAlignment.Center
		};
		check.SetResourceReference(FrameworkElement.StyleProperty, "MacSwitch");
		AutomationProperties.SetName(check, title);
		AutomationProperties.SetHelpText(check, description);
		check.Click += delegate
		{
			try
			{
				change(check.IsChecked == true);
			}
			catch (Exception ex)
			{
				check.IsChecked = selected;
				CrashLog.Write("setting-change", ex);
				AirStyle.Notice(ex.Message, "设置未完成");
			}
		};
		panel.Children.Add(new Border
		{
			Child = check,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(0.0, 0.0, 0.0, 0.5)
		});
	}

	private static void Import(Program p, Window owner)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "选择 Luma 配置备份",
			Filter = "Luma 配置|*.json;*.bak;*.tmp",
			CheckFileExists = true
		};
		if (openFileDialog.ShowDialog(owner) != true)
		{
			return;
		}
		try
		{
			p.ImportGroups(openFileDialog.FileName);
		}
		catch (Exception ex)
		{
			AirStyle.Notice("未导入，原配置保持不变。\n" + ex.Message, "恢复分类");
		}
	}
}
