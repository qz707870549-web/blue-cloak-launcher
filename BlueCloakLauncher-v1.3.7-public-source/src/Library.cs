using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Luma;

public partial class Library : Window
{
	private System.Windows.Controls.ContextMenu openContext;

	private int modalDepth;

	private bool resizing;

	private bool reordering;

	private bool pendingRender;

	private Program p;

	private System.Windows.Controls.TextBox search = new System.Windows.Controls.TextBox
	{
		FontSize = 14.0,
		Foreground = UI.Ink,
		CaretBrush = UI.Accent,
		Background = Brushes.Transparent,
		BorderThickness = new Thickness(0.0),
		VerticalContentAlignment = VerticalAlignment.Center,
		Padding = new Thickness(0.0, 10.0, 10.0, 10.0)
	};

	private StackPanel grid = new StackPanel();

	private ScrollViewer appScroll;

	private string lastGridScope = "";

	private int gridRevision;

	private System.Windows.Controls.Button themeButton;

	private System.Windows.Controls.Button gridViewButton;

	private System.Windows.Controls.Button listViewButton;

	private StackPanel navigation = new StackPanel();

	private TextBlock count = UI.Text("", 11.0, UI.Muted);

	private TextBlock heading = UI.Text("我的应用", 24.0, UI.Ink);

	private string selected = "@overview";

	public WindowState PreviousWindowState;

	private WindowState observedWindowState;

	private bool userSized;

	private int openingRevision;

	private Grid shell;

	private ColumnDefinition leftColumn;

	private ColumnDefinition rightColumn;

	private ScrollViewer rightPanel;

	private StackPanel recentPanel = new StackPanel();

	private TextBlock subtitle;

	private System.Windows.Controls.Button selectedCard;

	private System.Windows.Controls.Button maxButton;
	private System.Windows.Controls.Button workspaceHome;

	private List<System.Windows.Controls.Button> visibleCards = new List<System.Windows.Controls.Button>();

	private Point navigationStart;

	private string draggingGroup;

	private bool initialized;

	private long shortcutSuppressedUntil;

	private bool sectionAnimating;

	private readonly NavigationHistory commandHistory = new NavigationHistory("@overview");

	private readonly HashSet<Entry> commandEntries = new HashSet<Entry>();

	private readonly HashSet<string> commandGroups = new HashSet<string>();

	private System.Windows.Controls.Button commandBack;

	private System.Windows.Controls.Button commandForward;

	private System.Windows.Controls.Button commandSelect;

	private System.Windows.Controls.Button commandEdit;

	private System.Windows.Controls.Button commandRemove;

	private System.Windows.Controls.Button commandMove;

	private System.Windows.Controls.Button commandUndo;

	private TextBlock commandSummary;

	private TextBlock commandGuide;

	private StackPanel commandHeader;

	private Border commandSearchFrame;

	private Action<Config> commandUndoAction;

	private readonly Dictionary<string, System.Windows.Controls.Button> collectionCards = new Dictionary<string, System.Windows.Controls.Button>();

	private string selectedGroupMac;

	private Entry selectedEntryMac;

	private bool macChromeReady;

	private TextBlock breadcrumbMac;

	private readonly Dictionary<string, System.Windows.Controls.Button> folderButtonsMac = new Dictionary<string, System.Windows.Controls.Button>();

	public bool CanOpenShortcut
	{
		get
		{
			if (!reordering)
			{
				return Stopwatch.GetTimestamp() >= shortcutSuppressedUntil;
			}
			return false;
		}
	}

	public bool IsInteracting
	{
		get
		{
			if (!OpeningWindow && (openContext == null || !openContext.IsOpen) && modalDepth <= 0 && !resizing)
			{
				return reordering;
			}
			return true;
		}
	}

	public bool PlacingWindow { get; set; }

	public bool OpeningWindow { get; private set; }

	public bool IsUserResizing => resizing;

	public bool IsSelecting { get; private set; }

	private string EditableCommandGroup
	{
		get
		{
			if (commandGroups.Count != 1)
			{
				if (IsSelecting || (!p.State.VisibleGroups().Contains(selected) && CurrentAppFolder == null))
				{
					return null;
				}
				return selected;
			}
			return commandGroups.First();
		}
	}

	private List<AppFolder> CommandFolders => (from f in commandGroups.Select(p.State.FindAppFolder)
		where f != null
		select f).ToList();

	private AppFolder CurrentAppFolder => p.State.FindAppFolder(selected);

	private bool OverviewMac
	{
		get
		{
			if (selected == "@overview")
			{
				return string.IsNullOrWhiteSpace(search.Text);
			}
			return false;
		}
	}

	public void SuppressShortcutClick()
	{
		shortcutSuppressedUntil = Stopwatch.GetTimestamp() + (long)((double)Stopwatch.Frequency * 0.5);
	}

	public void SetReordering(bool value)
	{
		reordering = value;
		if (!value)
		{
			if (pendingRender)
			{
				pendingRender = false;
				Render();
			}
			p.AutoDismissLibrary(this);
		}
	}

	public void TrackContext(System.Windows.Controls.ContextMenu menu)
	{
		openContext = menu;
	}

	public void EndContext(System.Windows.Controls.ContextMenu menu)
	{
		if (openContext == menu)
		{
			openContext = null;
		}
		p.AutoDismissLibrary(this);
	}

	private void SaveSize()
	{
		Size size = new Size(base.ActualWidth, base.ActualHeight);
		if (WindowSizePolicy.CanSave(size, base.WindowState, base.IsLoaded && base.IsVisible, userSized, PlacingWindow || OpeningWindow))
		{
			p.State.LibraryWidth = size.Width;
			p.State.LibraryHeight = size.Height;
			p.Save();
		}
	}

	private void Modal(Action action)
	{
		modalDepth++;
		try
		{
			action();
		}
		finally
		{
			modalDepth--;
			p.AutoDismissLibrary(this);
		}
	}

	public void FocusSearch()
	{
		search.Focus();
	}

	public void ChooseIcon(Entry app)
	{
		Modal(delegate
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
			{
				Title = "选择图标图片",
				Filter = "图标图片|*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif",
				CheckFileExists = true
			};
			if (openFileDialog.ShowDialog(this) != true)
			{
				return;
			}
			try
			{
				app.CustomIcon = AppCustomization.StoreIcon(openFileDialog.FileName, System.IO.Path.Combine(Program.DataDir, "custom-icons"));
				UI.InvalidateIcons();
				p.Refresh();
			}
			catch (Exception ex)
			{
				AirStyle.Notice(ex.Message, "更换图标");
			}
		});
	}

	private void FavoriteFolder()
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = "选择要收藏的文件夹（只创建快捷入口）"
		};
		if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
		{
			return;
		}
		string path = folderBrowserDialog.SelectedPath;
		Entry entry = p.State.Apps.FirstOrDefault((Entry a) => a.FavoriteFolder && string.Equals(a.Target, path, StringComparison.OrdinalIgnoreCase));
		if (entry != null)
		{
			AirStyle.Notice("这个文件夹已经在「" + entry.Group + "」中。", "文件夹收藏");
			return;
		}
		Window chooser = new Window
		{
			Owner = this,
			Topmost = true,
			Title = "收藏到分组",
			Width = 360.0,
			Height = 220.0,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			Background = UI.Panel,
			ResizeMode = ResizeMode.NoResize
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(22.0)
		};
		stackPanel.Children.Add(UI.Text("选择收藏分组", 17.0, UI.Ink));
		System.Windows.Controls.ComboBox comboBox = new System.Windows.Controls.ComboBox
		{
			ItemsSource = p.State.Groups,
			SelectedItem = ((selected == "全部") ? "未分类" : selected),
			Margin = new Thickness(0.0, 18.0, 0.0, 15.0),
			Padding = new Thickness(8.0)
		};
		stackPanel.Children.Add(comboBox);
		stackPanel.Children.Add(UI.Button("收藏文件夹", delegate
		{
			chooser.DialogResult = true;
		}));
		chooser.Content = stackPanel;
		if (UI.ShowModal(chooser) == true)
		{
			p.State.Apps.Add(new Entry
			{
				Id = "folder:" + path.ToLowerInvariant(),
				Name = new DirectoryInfo(path).Name,
				Target = path,
				IconPath = path,
				Group = Convert.ToString(comboBox.SelectedItem),
				FavoriteFolder = true
			});
			p.Refresh();
		}
	}

	private void AddWebsite()
	{
		Window window = new Window
		{
			Owner = this,
			Topmost = true,
			Title = "添加网站",
			Width = 460.0,
			SizeToContent = SizeToContent.Height,
			ResizeMode = ResizeMode.NoResize,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = UI.Panel,
			FontFamily = AirStyle.Font
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		stackPanel.Children.Add(UI.Text("添加网站", 22.0, UI.Ink));
		Border preview = new Border
		{
			Width = 44.0,
			Height = 44.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 16.0, 0.0, 12.0),
			Child = UI.Icon(new Entry
			{
				Website = true,
				Target = "website-preview"
			}, 40.0)
		};
		stackPanel.Children.Add(preview);
		stackPanel.Children.Add(UI.Text("网址", 12.0, UI.Muted));
		System.Windows.Controls.TextBox address = new System.Windows.Controls.TextBox
		{
			Padding = new Thickness(10.0),
			FontSize = 13.0,
			Margin = new Thickness(0.0, 6.0, 0.0, 12.0)
		};
		AutomationProperties.SetName(address, "网站网址");
		stackPanel.Children.Add(address);
		stackPanel.Children.Add(UI.Text("名称", 12.0, UI.Muted));
		System.Windows.Controls.TextBox name = new System.Windows.Controls.TextBox
		{
			Padding = new Thickness(10.0),
			FontSize = 13.0,
			Margin = new Thickness(0.0, 6.0, 0.0, 12.0)
		};
		AutomationProperties.SetName(name, "网站名称");
		stackPanel.Children.Add(name);
		TextBlock status = UI.Text("将放入新建的「网站」分组，可右键移动到其他分组。", 11.0, UI.Muted);
		status.TextWrapping = TextWrapping.Wrap;
		stackPanel.Children.Add(status);
		string imagePath = null;
		string fetchedAddress = null;
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		System.Windows.Controls.Button fetch = null;
		System.Windows.Controls.Button save = null;
		fetch = UI.Button("获取图标", async delegate
		{
			if (!Websites.TryUrl(address.Text, out var url))
			{
				status.Text = "请输入有效的 http 或 https 网址。";
				return;
			}
			fetch.IsEnabled = false;
			save.IsEnabled = false;
			status.Text = "正在获取网站图标…";
			try
			{
				WebsiteInfo websiteInfo = await Task.Run(() => Websites.Fetch(url));
				if (window.IsVisible)
				{
					fetchedAddress = url.AbsoluteUri;
					imagePath = websiteInfo.IconPath;
					if (string.IsNullOrWhiteSpace(name.Text))
					{
						name.Text = websiteInfo.Name;
					}
					preview.Child = UI.Icon(new Entry
					{
						Website = true,
						ImagePath = imagePath,
						Target = url.AbsoluteUri
					}, 40.0);
					status.Text = ((imagePath == null) ? "暂未获取到图标（网络或网站限制），可继续添加。" : "图标已获取。");
				}
			}
			finally
			{
				fetch.IsEnabled = true;
				save.IsEnabled = true;
			}
		});
		save = UI.Button("添加到网站分组", delegate
		{
			if (!Websites.TryUrl(address.Text, out var url))
			{
				status.Text = "请输入有效的 http 或 https 网址。";
			}
			else if (p.State.Apps.Any((Entry app) => app.Website && string.Equals(app.Target, url.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
			{
				status.Text = "这个网站已经添加过了。";
			}
			else
			{
				if (!p.State.Groups.Contains("网站"))
				{
					p.State.Groups.Add("网站");
				}
				p.State.HiddenGroups.Remove("网站");
				p.State.Apps.Add(new Entry
				{
					Id = "website:" + url.AbsoluteUri,
					Target = url.AbsoluteUri,
					Name = (string.IsNullOrWhiteSpace(name.Text) ? url.Host : name.Text.Trim()),
					Group = "网站",
					Website = true,
					ImagePath = ((fetchedAddress == url.AbsoluteUri) ? imagePath : null)
				});
				selected = "网站";
				p.Refresh();
				window.DialogResult = true;
			}
		});
		stackPanel2.Children.Add(fetch);
		stackPanel2.Children.Add(save);
		stackPanel.Children.Add(stackPanel2);
		window.Content = stackPanel;
		UI.ShowModal(window);
	}

	private void NewGroup()
	{
		string text = Prompt("新建分类", "分类名称", "");
		if (!string.IsNullOrWhiteSpace(text))
		{
			text = text.Trim();
			if (text != "全部" && !p.State.Groups.Contains(text))
			{
				p.State.Groups.Add(text);
				p.Refresh();
			}
		}
	}

	public static string Prompt(string title, string caption, string initial)
	{
		Window w = new Window
		{
			Owner = (System.Windows.Application.Current as Program).Library,
			Topmost = true,
			Title = title,
			Width = 380.0,
			Height = 200.0,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			ResizeMode = ResizeMode.NoResize,
			Background = UI.Panel,
			FontFamily = AirStyle.Font
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(22.0)
		};
		stackPanel.Children.Add(UI.Text(caption, 14.0, UI.Ink));
		System.Windows.Controls.TextBox textBox = new System.Windows.Controls.TextBox
		{
			Text = initial,
			Margin = new Thickness(0.0, 12.0, 0.0, 8.0),
			Padding = new Thickness(8.0),
			FontSize = 14.0
		};
		stackPanel.Children.Add(textBox);
		System.Windows.Controls.Button button = UI.Button("完成", delegate
		{
			w.DialogResult = true;
		});
		button.IsDefault = true;
		stackPanel.Children.Add(button);
		w.Content = stackPanel;
		if (UI.ShowModal(w) != true)
		{
			return null;
		}
		return textBox.Text;
	}

	private void ScanFolder()
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = "选择软件文件夹（向下扫描 4 层）"
		};
		if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			if (!p.State.Folders.Contains(folderBrowserDialog.SelectedPath))
			{
				p.State.Folders.Add(folderBrowserDialog.SelectedPath);
			}
			p.Scan();
		}
	}

	private void Manage()
	{
		Window obj = new Window
		{
			Owner = this,
			Topmost = true,
			Title = "分组排序与显示",
			Width = 620.0,
			Height = 540.0,
			Background = UI.Panel,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			FontFamily = AirStyle.Font
		};
		StackPanel body = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		Action render = null;
		render = delegate
		{
			body.Children.Clear();
			body.Children.Add(new TextBlock
			{
				Text = "勾选显示；隐藏后可在设置中重新打开。上下箭头调整顺序。",
				Foreground = UI.Muted,
				Margin = new Thickness(0.0, 10.0, 0.0, 20.0)
			});
			foreach (string item in p.State.Groups.ToList())
			{
				string name = item;
				DockPanel dockPanel = new DockPanel();
				StackPanel stackPanel = new StackPanel
				{
					Orientation = System.Windows.Controls.Orientation.Horizontal
				};
				int[] array = new int[2] { -1, 1 };
				foreach (int num in array)
				{
					int move = num;
					System.Windows.Controls.Button button = UI.Button((move < 0) ? "↑" : "↓", delegate
					{
						int num2 = p.State.Groups.IndexOf(name);
						int num3 = num2 + move;
						if (num3 >= 0 && num3 < p.State.Groups.Count)
						{
							p.State.Groups.RemoveAt(num2);
							p.State.Groups.Insert(num3, name);
							p.Refresh();
							render();
						}
					});
					button.IsEnabled = p.State.Groups.IndexOf(name) + move >= 0 && p.State.Groups.IndexOf(name) + move < p.State.Groups.Count;
					stackPanel.Children.Add(button);
				}
				if (name != "未分类" && name != "系统附带应用" && name != "软件配套工具")
				{
					stackPanel.Children.Add(UI.Button("改名", delegate
					{
						string text = Prompt("重命名分组", "新名称", name);
						if (!string.IsNullOrWhiteSpace(text) && !(text.Trim() == "全部") && !p.State.Groups.Contains(text.Trim()))
						{
							text = text.Trim();
							p.State.Groups[p.State.Groups.IndexOf(name)] = text;
							foreach (Entry item2 in p.State.Apps.Where((Entry a) => a.Group == name))
							{
								item2.Group = text;
							}
							if (p.State.HiddenGroups.Remove(name))
							{
								p.State.HiddenGroups.Add(text);
							}
							p.Refresh();
							render();
						}
					}));
					stackPanel.Children.Add(UI.Button("删除", delegate
					{
						if (AirStyle.Confirm("删除分类", "分类内的应用将移至「未分类」，不会删除软件或文件。", "删除分类"))
						{
							foreach (Entry item3 in p.State.Apps.Where((Entry a) => a.Group == name))
							{
								item3.Group = "未分类";
							}
							p.State.Groups.Remove(name);
							p.State.HiddenGroups.Remove(name);
							p.Refresh();
							render();
						}
					}));
				}
				DockPanel.SetDock(stackPanel, System.Windows.Controls.Dock.Right);
				dockPanel.Children.Add(stackPanel);
				System.Windows.Controls.CheckBox visible = new System.Windows.Controls.CheckBox
				{
					Content = name,
					IsChecked = !p.State.HiddenGroups.Contains(name),
					VerticalAlignment = VerticalAlignment.Center,
					FontSize = 13.0
				};
				visible.Click += delegate
				{
					p.State.HiddenGroups.Remove(name);
					if (visible.IsChecked != true)
					{
						p.State.HiddenGroups.Add(name);
					}
					p.Refresh();
				};
				dockPanel.Children.Add(visible);
				body.Children.Add(dockPanel);
			}
		};
		render();
		obj.Content = new ScrollViewer
		{
			Content = body,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		UI.ShowModal(obj);
	}

	private void Settings()
	{
		AirSettings.Open(p, this, delegate
		{
			Modal(Manage);
		});
	}

	private void LegacySettings()
	{
		Window w = new Window
		{
			Owner = this,
			Topmost = true,
			Title = "蓝斗篷启动 · 设置",
			Width = 510.0,
			Height = 720.0,
			Background = UI.Panel,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			FontFamily = AirStyle.Font
		};
		StackPanel s = new StackPanel
		{
			Margin = new Thickness(28.0)
		};
		s.Children.Add(UI.Text("让桌面轻一点", 24.0, UI.Ink));
		TextBlock textBlock = UI.Text("选择你希望留在桌面上的内容。", 12.0, UI.Muted);
		textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 20.0);
		s.Children.Add(textBlock);
		s.Children.Add(UI.Button("分组排序、隐藏与恢复显示", Manage));
		s.Children.Add(UI.Button("恢复已移除的应用入口", delegate
		{
			p.State.ExcludedIds.Clear();
			p.Save();
			p.Scan();
		}));
		s.Children.Add(UI.Button("恢复 / 导入原有分组…", delegate
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
			{
				Title = "选择原来的 Luma 配置或备份",
				InitialDirectory = Program.DataDir,
				FileName = "settings.json",
				Filter = "Luma 配置及备份|*.json;*.bak;*.tmp|所有文件|*.*",
				CheckFileExists = true
			};
			if (openFileDialog.ShowDialog(w) != true)
			{
				return;
			}
			try
			{
				p.ImportGroups(openFileDialog.FileName);
			}
			catch (Exception ex)
			{
				AirStyle.Notice("未导入，原配置保持不变：" + ex.Message, "恢复分组");
			}
		}));
		s.Children.Add(UI.Button("显示 / 隐藏原有桌面图标", p.ToggleDesktop));
		Action<string, bool, Action<bool>> action = delegate(string label, bool value, Action<bool> action2)
		{
			System.Windows.Controls.CheckBox c = new System.Windows.Controls.CheckBox
			{
				Content = label,
				IsChecked = value,
				Foreground = UI.Ink,
				FontSize = 13.0,
				Margin = new Thickness(5.0, 12.0, 5.0, 12.0)
			};
			c.Click += delegate
			{
				action2(c.IsChecked == true);
			};
			s.Children.Add(c);
		};
		action("点击任务栏开始图标，打开我的分组", p.State.ReplaceStartMenu, delegate(bool value)
		{
			p.SetStartReplacement(value);
		});
		s.Children.Add(UI.Text("关闭开关或退出程序恢复系统点击行为。Win 键与系统快捷键保持原样。", 11.0, UI.Muted));
		action("显示 Windows 系统附带应用", p.State.ShowSystemApps, delegate(bool v)
		{
			p.State.ShowSystemApps = v;
			if (v)
			{
				p.State.HiddenGroups.Remove("系统附带应用");
			}
			selected = "全部";
			p.Refresh();
		});
		action("显示软件配套工具与后台组件", p.State.ShowUtilities, delegate(bool v)
		{
			p.State.ShowUtilities = v;
			if (v)
			{
				p.State.HiddenGroups.Remove("软件配套工具");
			}
			selected = "全部";
			p.Refresh();
		});
		action("登录 Windows 时启动", p.State.StartWithWindows ?? StartupRegistration.LegacyEnabled(), delegate(bool v)
		{
			string result = StartupRegistration.Apply(v);
			p.State.StartWithWindows = v;
			p.Save();
			AirStyle.Notice(result, "登录启动");
		});
		TextBlock textBlock2 = UI.Text("便携软件可通过应用库右下角的文件夹按钮补充。\n隐藏的系统应用与配套工具可随时重新显示。", 11.0, UI.Muted);
		textBlock2.LineHeight = 22.0;
		textBlock2.Margin = new Thickness(4.0, 14.0, 0.0, 12.0);
		s.Children.Add(textBlock2);
		s.Children.Add(UI.Button("打开配置文件夹", delegate
		{
			Process.Start("explorer.exe", Program.DataDir);
		}));
		w.Content = new ScrollViewer
		{
			Content = s,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		UI.ShowModal(w);
	}

	public int BeginOpening()
	{
		OpeningWindow = true;
		return ++openingRevision;
	}

	public bool IsOpening(int revision)
	{
		if (OpeningWindow)
		{
			return revision == openingRevision;
		}
		return false;
	}

	public void EndOpening(int revision)
	{
		if (revision == openingRevision)
		{
			OpeningWindow = false;
		}
	}

	public void RefreshRestoredLayout()
	{
		if (base.IsVisible && base.WindowState != WindowState.Minimized)
		{
			InvalidateMeasure();
			if (base.Content is FrameworkElement frameworkElement)
			{
				frameworkElement.InvalidateMeasure();
				frameworkElement.InvalidateVisual();
			}
			UpdateLayout();
			InvalidateVisual();
			Glass.Apply(this, p.State);
		}
	}

	public Library(Program program)
	{
		p = program;
		FluentTheme.Apply(p.State.ThemeMode);
		UI.Window(this);
		base.Title = "蓝斗篷启动";
		base.Topmost = true;
		base.ShowInTaskbar = true;
		base.Width = p.State.LibraryWidth;
		base.Height = p.State.LibraryHeight;
		base.MinWidth = 500.0;
		base.MinHeight = 420.0;
		base.ResizeMode = ResizeMode.CanResize;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		shell = new Grid();
		leftColumn = new ColumnDefinition
		{
			Width = new GridLength(220.0)
		};
		rightColumn = new ColumnDefinition
		{
			Width = new GridLength(250.0)
		};
		shell.ColumnDefinitions.Add(leftColumn);
		shell.ColumnDefinitions.Add(new ColumnDefinition());
		shell.ColumnDefinitions.Add(rightColumn);
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(12.0, 20.0, 12.0, 16.0)
		};
		Icon = Branding.Avatar();
		var brandRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(3, 0, 0, 18) };
		brandRow.Children.Add(new Image { Source = Branding.Avatar(), Width = 36, Height = 36, Margin = new Thickness(0, 0, 8, 0) });
		var brandName = UI.Text(Branding.Name, 14, UI.Ink);
		brandName.FontWeight = FontWeights.SemiBold;
		brandName.VerticalAlignment = VerticalAlignment.Center;
		brandRow.Children.Add(brandName);
		DockPanel.SetDock(brandRow, System.Windows.Controls.Dock.Top);
		dockPanel.Children.Add(brandRow);
		StackPanel stackPanel = new StackPanel();
		UIElementCollection children = stackPanel.Children;
		Action action = delegate
		{
			Modal(Settings);
		};
		children.Add(SideButton("\ue713", "设置", action));
		stackPanel.Children.Add(SideButton("\ue790", "管理分类", delegate
		{
			Modal(Manage);
		}));
		System.Windows.Controls.Button button = null;
		button = (themeButton = SideButton("\ue708", "主题 · " + p.State.ThemeMode, delegate
		{
			string[] array = new string[3] { "系统", "浅色", "深色" };
			p.State.ThemeMode = array[(Array.IndexOf(array, p.State.ThemeMode) + 1) % 3];
			FluentTheme.Apply(p.State.ThemeMode);
			p.Save();
			RefreshThemeLabel();
			Render();
			p.AppearanceChanged();
		}));
		button.ToolTip = "点击切换：跟随系统 / 浅色 / 深色";
		stackPanel.Children.Add(button);
		DockPanel.SetDock(stackPanel, System.Windows.Controls.Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = navigation,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		});
		shell.Children.Add(new Border
		{
			Child = dockPanel,
			Background = FluentTheme.Sidebar,
			CornerRadius = new CornerRadius(24.0),
			Margin = new Thickness(12.0, 12.0, 0.0, 12.0),
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(1.0)
		});
		DockPanel dockPanel2 = new DockPanel
		{
			Margin = new Thickness(24.0, 16.0, 18.0, 12.0)
		};
		Grid.SetColumn(dockPanel2, 1);
		shell.Children.Add(dockPanel2);
		StackPanel stackPanel2 = new StackPanel();
		DockPanel dockPanel3 = new DockPanel
		{
			Height = 40.0
		};
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right
		};
		stackPanel3.Children.Add(UI.Glyph("\ue921", "最小化", delegate
		{
			SystemCommands.MinimizeWindow(this);
		}));
		maxButton = UI.Glyph("\ue922", "最大化 / 还原", ToggleMaximize);
		stackPanel3.Children.Add(maxButton);
		stackPanel3.Children.Add(UI.Glyph("\ue8bb", "收起到托盘", p.DismissLibrary));
		DockPanel.SetDock(stackPanel3, System.Windows.Controls.Dock.Right);
		dockPanel3.Children.Add(stackPanel3);
		heading.FontSize = 24.0;
		heading.FontWeight = FontWeights.SemiBold;
		heading.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs input)
		{
			if (input.ClickCount == 2)
			{
				ToggleMaximize();
			}
			else if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				DragMove();
			}
		};
		dockPanel3.Children.Add(heading);
		stackPanel2.Children.Add(dockPanel3);
		dockPanel3.Children.Remove(stackPanel3);
		stackPanel3.VerticalAlignment = VerticalAlignment.Top;
		stackPanel3.Margin = new Thickness(0.0, 10.0, 12.0, 0.0);
		Grid.SetColumnSpan(stackPanel3, 3);
		System.Windows.Controls.Panel.SetZIndex(stackPanel3, 10);
		shell.Children.Add(stackPanel3);
		subtitle = UI.Text("", 12.0, UI.Muted);
		subtitle.Margin = new Thickness(0.0, 0.0, 0.0, 16.0);
		stackPanel2.Children.Add(subtitle);
		DockPanel dockPanel4 = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 20.0)
		};
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0)
		};
		gridViewButton = UI.Glyph("\ue80a", "网格视图", delegate
		{
			p.State.LibraryListView = false;
			p.Refresh();
		});
		stackPanel4.Children.Add(gridViewButton);
		listViewButton = UI.Glyph("\ue8fd", "列表视图", delegate
		{
			p.State.LibraryListView = true;
			if (selected == "@overview")
			{
				NavigateMac("全部");
			}
			selectedEntryMac = null;
			p.Refresh();
		});
		stackPanel4.Children.Add(listViewButton);
		DockPanel.SetDock(stackPanel4, System.Windows.Controls.Dock.Right);
		dockPanel4.Children.Add(stackPanel4);
		DockPanel dockPanel5 = new DockPanel();
		TextBlock textBlock = UI.Text("\ue721", 17.0, UI.Muted);
		textBlock.FontFamily = new FontFamily("Segoe MDL2 Assets");
		textBlock.Margin = new Thickness(14.0, 0.0, 12.0, 0.0);
		dockPanel5.Children.Add(textBlock);
		TextBlock textBlock2 = UI.Text("Ctrl K", 11.0, UI.Muted);
		textBlock2.Margin = new Thickness(8.0);
		DockPanel.SetDock(textBlock2, System.Windows.Controls.Dock.Right);
		dockPanel5.Children.Add(textBlock2);
		Grid grid = new Grid
		{
			ClipToBounds = true
		};
		grid.Children.Add(search);
		TextBlock hint = UI.Text("搜索应用、文件夹、网站或标签…", 13.0, UI.Muted);
		hint.IsHitTestVisible = false;
		hint.TextTrimming = TextTrimming.CharacterEllipsis;
		grid.Children.Add(hint);
		search.FontSize = 13.0;
		search.TextChanged += delegate
		{
			hint.Visibility = ((!string.IsNullOrEmpty(search.Text)) ? Visibility.Collapsed : Visibility.Visible);
			selectedEntryMac = null;
			RenderGrid();
			RenderInspectorMac();
		};
		dockPanel5.Children.Add(grid);
		dockPanel4.Children.Add(new Border
		{
			Child = dockPanel5,
			Background = FluentTheme.Card,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Height = 44.0
		});
		stackPanel2.Children.Add(dockPanel4);
		DockPanel.SetDock(stackPanel2, System.Windows.Controls.Dock.Top);
		dockPanel2.Children.Add(stackPanel2);
		DockPanel dockPanel6 = new DockPanel
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		StackPanel stackPanel5 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		stackPanel5.Children.Add(UI.Glyph("\ue710", "添加应用", delegate
		{
			Modal(AddApplication);
		}));
		stackPanel5.Children.Add(UI.Glyph("\ue774", "添加网站", delegate
		{
			Modal(AddWebsite);
		}));
		stackPanel5.Children.Add(UI.Glyph("\ue8b7", "收藏文件夹", delegate
		{
			Modal(FavoriteFolder);
		}));
		stackPanel5.Children.Add(UI.Glyph("\ue72c", "重新发现应用", p.Scan));
		DockPanel.SetDock(stackPanel5, System.Windows.Controls.Dock.Right);
		dockPanel6.Children.Add(stackPanel5);
		dockPanel6.Children.Add(count);
		DockPanel.SetDock(dockPanel6, System.Windows.Controls.Dock.Bottom);
		dockPanel2.Children.Add(dockPanel6);
		appScroll = new ScrollViewer
		{
			Content = this.grid,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		dockPanel2.Children.Add(appScroll);
		rightPanel = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Margin = new Thickness(0.0, 72.0, 16.0, 16.0)
		};
		Grid.SetColumn(rightPanel, 2);
		shell.Children.Add(rightPanel);
		base.Content = new Border
		{
			Child = shell,
			CornerRadius = new CornerRadius(16.0),
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(1.0),
			Background = FluentTheme.Background
		};
		WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
		windowChrome.CaptionHeight = 0.0;
		windowChrome.ResizeBorderThickness = new Thickness(6.0);
		windowChrome.CornerRadius = new CornerRadius(16.0);
		base.SourceInitialized += delegate
		{
			HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(FluentChromeMessages);
		};
		base.StateChanged += delegate
		{
			WindowState num = observedWindowState;
			observedWindowState = base.WindowState;
			if (base.WindowState != WindowState.Minimized)
			{
				PreviousWindowState = base.WindowState;
			}
			if (num == WindowState.Minimized && base.WindowState != WindowState.Minimized && !OpeningWindow)
			{
				base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshRestoredLayout));
			}
		};
		base.IsVisibleChanged += delegate
		{
			CrashLog.Write("visibility-" + base.IsVisible);
		};
		base.Closing += delegate
		{
			CrashLog.Write("library-closing");
		};
		base.Deactivated += delegate
		{
			CrashLog.Write("library-deactivated");
			p.AutoDismissLibrary(this);
		};
		base.SizeChanged += delegate
		{
			UpdateResponsive(base.ActualWidth);
		};
		base.PreviewKeyDown += Keys;
		SystemEvents.UserPreferenceChanged += SystemThemeChanged;
		base.Closed += delegate
		{
			SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
		};
		initialized = true;
		ConfigureMacChrome();
		UpdateResponsive(base.Width);
		Render();
		AirStyle.Normalize((DependencyObject)base.Content);
	}

	private void ToggleMaximize()
	{
		if (base.WindowState == WindowState.Maximized)
		{
			SystemCommands.RestoreWindow(this);
		}
		else
		{
			SystemCommands.MaximizeWindow(this);
		}
	}

	private IntPtr FluentChromeMessages(IntPtr hwnd, int message, IntPtr parameter, IntPtr data, ref bool handled)
	{
		if (message == 561)
		{
			resizing = true;
			userSized = false;
		}
		if (message == 532 && resizing && !PlacingWindow)
		{
			userSized = true;
		}
		if (message == 562)
		{
			resizing = false;
			SaveSize();
			userSized = false;
		}
		if (message == 132 && maxButton.IsLoaded && base.WindowState != WindowState.Minimized)
		{
			long num = data.ToInt64();
			Point point = new Point((short)(num & 0xFFFF), (short)((num >> 16) & 0xFFFF));
			Point point2 = maxButton.PointFromScreen(point);
			if (new Rect(0.0, 0.0, maxButton.ActualWidth, maxButton.ActualHeight).Contains(point2))
			{
				handled = true;
				return new IntPtr(9);
			}
		}
		if ((message == 161 || message == 162) && parameter.ToInt32() == 9)
		{
			handled = true;
			if (message == 162)
			{
				ToggleMaximize();
			}
		}
		return IntPtr.Zero;
	}

	public void UpdateResponsive(double width)
	{
		if (rightPanel == null)
		{
			return;
		}
		bool flag = false;
		rightPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		rightColumn.Width = new GridLength(flag ? 264 : 0);
		leftColumn.Width = new GridLength((width < 680.0) ? 170 : ((width < 940.0) ? 196 : 228));
		if (OverviewMac)
		{
			UniformGrid uniformGrid = grid.Children.OfType<UniformGrid>().FirstOrDefault();
			if (uniformGrid != null)
			{
				uniformGrid.Columns = Math.Max(2, Math.Min(5, (int)((width - leftColumn.Width.Value - 64.0) / 250.0)));
			}
		}
		if (commandSearchFrame != null)
		{
			commandSearchFrame.Width = ((width < 1000.0) ? 160 : 248);
		}
	}

	private void RefreshThemeLabel()
	{
		if (themeButton?.Content is DockPanel dockPanel)
		{
			TextBlock textBlock = dockPanel.Children.OfType<TextBlock>().LastOrDefault();
			if (textBlock != null)
			{
				textBlock.Text = "主题 · " + p.State.ThemeMode;
			}
		}
	}

	private void SystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		if (p.State.ThemeMode == "系统")
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				FluentTheme.Apply("系统");
				Render();
				p.AppearanceChanged();
			});
		}
	}

	private System.Windows.Controls.Button SideButton(string glyph, string text, Action action)
	{
		DockPanel dockPanel = new DockPanel();
		UI.Text(glyph, 17.0, UI.Ink);
		FrameworkElement frameworkElement = AirStyle.Glyph(glyph, text);
		frameworkElement.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		dockPanel.Children.Add(frameworkElement);
		TextBlock textBlock = UI.Text(text, 13.0, UI.Ink);
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		dockPanel.Children.Add(textBlock);
		System.Windows.Controls.Button button = UI.Button("", action);
		button.Content = dockPanel;
		button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
		button.Background = Brushes.Transparent;
		button.Padding = new Thickness(12.0, 10.0, 12.0, 10.0);
		button.Margin = new Thickness(0.0, 2.0, 0.0, 2.0);
		button.ToolTip = text;
		button.BorderThickness = new Thickness(0.0);
		return button;
	}

	public void Render()
	{
		if (!initialized)
		{
			return;
		}
		if (reordering)
		{
			pendingRender = true;
			return;
		}
		if (selected != "@overview" && selected != "全部" && selected != "@favorites" && selected != "@recent" && !p.State.VisibleGroups().Contains(selected) && CurrentAppFolder == null)
		{
			selected = "全部";
		}
		navigation.Children.Clear();
		if (!p.State.ShowWorkspace && selected == "@overview") selected = "全部";
		if (p.State.ShowWorkspace) AddNavigation("@overview", "工作空间", "\ue8b7", p.State.VisibleGroups().Count());
		AddNavigation("全部", "所有应用", "\ue80a", p.State.Apps.Count(p.State.IsVisible));
		foreach (string item in p.State.VisibleGroups())
		{
			string name = item;
			System.Windows.Controls.Button b = AddNavigation(name, name, "\ue8b7", p.State.Apps.Count((Entry a) => p.State.IsVisible(a) && p.State.LibraryGroup(a) == name));
			b.AllowDrop = true;
			b.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs input)
			{
				navigationStart = input.GetPosition(this);
				draggingGroup = name;
			};
			b.PreviewMouseMove += delegate(object sender, System.Windows.Input.MouseEventArgs input)
			{
				Point position = input.GetPosition(this);
				if (input.LeftButton == MouseButtonState.Pressed && draggingGroup == name && (Math.Abs(position.X - navigationStart.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(position.Y - navigationStart.Y) > SystemParameters.MinimumVerticalDragDistance))
				{
					input.Handled = true;
					modalDepth++;
					try
					{
						DragDrop.DoDragDrop(b, new System.Windows.DataObject("Luma.Group", name), System.Windows.DragDropEffects.Move);
					}
					finally
					{
						modalDepth--;
						draggingGroup = null;
					}
				}
			};
			b.DragOver += delegate(object sender, System.Windows.DragEventArgs input)
			{
				Entry entry = ReorderPanel.DraggedEntry(input.Data);
				bool flag = ((entry == null) ? input.Data.GetDataPresent("Luma.Group") : (p.State.LibraryGroup(entry) != name || !string.IsNullOrEmpty(entry.FolderId)));
				input.Effects = (flag ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None);
				if (flag)
				{
					b.Background = FluentTheme.Selected;
					b.BorderBrush = UI.Accent;
					b.BorderThickness = new Thickness(1.0);
				}
				input.Handled = true;
			};
			b.DragLeave += delegate
			{
				PaintMacNavigation();
			};
			b.Drop += delegate(object sender, System.Windows.DragEventArgs input)
			{
				if (ReorderPanel.DraggedEntry(input.Data) != null)
				{
					input.Effects = (ReorderPanel.MoveDraggedEntry(input.Data, p, name) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None);
					input.Handled = true;
					PaintMacNavigation();
				}
				else
				{
					string moving = input.Data.GetData("Luma.Group") as string;
					if (p.State.MoveGroup(moving, name))
					{
						p.Save();
						base.Dispatcher.BeginInvoke(new Action(Render));
					}
					input.Handled = true;
				}
			};
		}
		navigation.Children.Add(SideButton("\ue710", "新建分类", delegate
		{
			Modal(NewGroup);
		}));
		navigation.Children.Add(new Border
		{
			Height = 1.0,
			Background = FluentTheme.Line,
			Margin = new Thickness(8.0, 16.0, 8.0, 12.0)
		});
		AddNavigation("@favorites", "收藏", "\ue734", p.State.Apps.Count((Entry a) => a.Favorite && p.State.IsVisible(a)));
		AddNavigation("@recent", "最近使用", "\ue823", p.State.Apps.Count((Entry a) => a.LastLaunchTicks > 0 && p.State.IsVisible(a)));
		navigation.Children.Add(SideButton("\ue896", "下载", OpenDownloads));
		heading.Text = ((selected == "全部") ? "我的应用" : ((selected == "@favorites") ? "收藏" : ((selected == "@recent") ? "最近使用" : selected)));
		subtitle.Text = p.State.Apps.Count(p.State.IsVisible) + " 个快捷入口 · " + p.State.VisibleGroups().Count() + " 个分类";
		RenderGrid();
		RenderInspectorMac();
		RefreshThemeLabel();
		PaintMacNavigation();
		gridViewButton.Background = ((OverviewMac || !p.State.LibraryListView) ? FluentTheme.Card : Brushes.Transparent);
		listViewButton.Background = ((!OverviewMac && p.State.LibraryListView) ? FluentTheme.Card : Brushes.Transparent);
		gridViewButton.BorderBrush = FluentTheme.Line;
		gridViewButton.BorderThickness = ((OverviewMac || !p.State.LibraryListView) ? new Thickness(1.0) : new Thickness(0.0));
		listViewButton.BorderBrush = FluentTheme.Line;
		listViewButton.BorderThickness = ((!OverviewMac && p.State.LibraryListView) ? new Thickness(1.0) : new Thickness(0.0));
	}

	private System.Windows.Controls.Button AddNavigation(string id, string label, string glyph, int quantity)
	{
		System.Windows.Controls.Button button = SideButton(glyph, label, delegate
		{
			NavigateMac(id);
			if (FluentTheme.Motion)
			{
				grid.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.55, 1.0, TimeSpan.FromMilliseconds(200.0)));
			}
		});
		DockPanel dockPanel = button.Content as DockPanel;
		if (id == "全部" || p.State.Groups.Contains(id))
		{
			FrameworkElement frameworkElement = NavigationIcons.Create((id == "全部") ? "所有分组" : label);
			frameworkElement.Width = 19.0;
			frameworkElement.Margin = new Thickness(0.0, 0.0, 9.0, 0.0);
			dockPanel.Children.RemoveAt(0);
			dockPanel.Children.Insert(0, frameworkElement);
		}
		TextBlock textBlock = UI.Text(quantity.ToString(), 11.0, UI.Muted);
		textBlock.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		DockPanel.SetDock(textBlock, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Insert(0, textBlock);
		if (selected == id)
		{
			button.Tag = "selected-navigation";
			button.Background = FluentTheme.Selected;
			AirStyle.SetSelected(button);
			if (FluentTheme.Motion)
			{
				button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.7, 1.0, TimeSpan.FromMilliseconds(120.0)));
			}
		}
		navigation.Children.Add(button);
		return button;
	}

	public static bool MatchesSearch(Entry a, string query)
	{
		if (!string.IsNullOrWhiteSpace(query))
		{
			return (UI.EntryName(a) + " " + a.Name + " " + a.Tags).IndexOf(query.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
		}
		return true;
	}

	private void RenderGrid()
	{
		if (!initialized)
		{
			return;
		}
		if (OverviewMac)
		{
			RenderOverviewMac();
		}
		else
		{
			if (RenderAppFolderScope())
			{
				return;
			}
			string text = selected + "|" + search.Text;
			double scroll = ((!(text == lastGridScope)) ? 0.0 : (appScroll?.VerticalOffset ?? 0.0));
			lastGridScope = text;
			int revision = ++gridRevision;
			grid.Children.Clear();
			collectionCards.Clear();
			folderButtonsMac.Clear();
			visibleCards.Clear();
			selectedCard = null;
			IEnumerable<Entry> enumerable = p.State.Apps.Where((Entry a) => p.State.IsVisible(a) && MatchesSearch(a, search.Text));
			if (selected == "@favorites")
			{
				enumerable = enumerable.Where((Entry a) => a.Favorite);
			}
			else if (selected == "@recent")
			{
				enumerable = enumerable.Where((Entry a) => a.LastLaunchTicks > 0);
			}
			else if (selected != "全部" && selected != "@overview")
			{
				enumerable = enumerable.Where((Entry a) => p.State.LibraryGroup(a) == selected);
			}
			IEnumerable<Entry> source;
			if (!(selected == "@recent"))
			{
				source = p.State.OrderedEntries(enumerable);
			}
			else
			{
				IEnumerable<Entry> enumerable2 = enumerable.OrderByDescending((Entry a) => a.LastLaunchTicks);
				source = enumerable2;
			}
			List<Entry> list = source.ToList();
			if (selected == "@favorites" || selected == "@recent")
			{
				AddSection(heading.Text, list, canOpen: false);
			}
			else
			{
				HashSet<Entry> topEntries = ((selected == "全部" && string.IsNullOrWhiteSpace(search.Text)) ? AddPinnedArea() : new HashSet<Entry>());
				foreach (string group in from g in p.State.VisibleGroups()
					where selected == "全部" || selected == "@overview" || selected == g
					select g)
				{
					Func<Entry, bool> predicate = (Entry a) => p.State.LibraryGroup(a) == group && !topEntries.Contains(a);
					List<Entry> list2 = list.Where(predicate).ToList();
					if (string.IsNullOrWhiteSpace(search.Text))
					{
						AddAppFolderSection(p.State.AppFolders.Where((AppFolder f) => f.Group == group), group + " · 文件夹");
						list2 = list2.Where((Entry a) => string.IsNullOrEmpty(a.FolderId)).ToList();
					}
					if (list2.Count > 0 || (selected == group && !p.State.AppFolders.Any((AppFolder f) => f.Group == group)))
					{
						AddSection(group, list2, canOpen: true);
					}
				}
			}
			if (list.Count == 0)
			{
				grid.Children.Add(new TextBlock
				{
					Text = (string.IsNullOrWhiteSpace(search.Text) ? "这里还没有快捷入口\n可以添加应用、网站或收藏文件夹。" : "没有找到匹配的项目\n试试应用名称或自定义标签。"),
					Foreground = UI.Muted,
					FontSize = 15.0,
					LineHeight = 28.0,
					Margin = new Thickness(12.0, 40.0, 12.0, 40.0)
				});
			}
			count.Text = list.Count + " 个项目 · 单击打开应用";
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
			{
				if (revision == gridRevision)
				{
					appScroll?.ScrollToVerticalOffset(scroll);
				}
			});
			UpdateCommandBar();
		}
	}

	private HashSet<Entry> AddPinnedArea()
	{
		var entries = p.State.OrderedEntries(p.State.Apps.Where(a => p.State.IsVisible(a) && a.TopPinned)).ToList();
		if (entries.Count > 0) AddSection("置顶应用", entries, false);
		return new HashSet<Entry>(entries);
	}
	private void AddSection(string name, List<Entry> members, bool canOpen, string description = null)
	{
		StackPanel stackPanel = new StackPanel
		{
			Tag = name,
			Margin = new Thickness(0.0, 0.0, 0.0, 20.0)
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		if (canOpen && selected != name)
		{
			System.Windows.Controls.Button button = UI.Button("查看全部", delegate
			{
				OpenGroupMac(name);
			});
			button.Background = Brushes.Transparent;
			button.BorderThickness = new Thickness(0.0);
			button.Foreground = UI.Muted;
			button.Padding = new Thickness(6.0);
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal
			};
			stackPanel2.Children.Add(UI.Text("查看全部", 12.0, UI.Muted));
			FrameworkElement frameworkElement = AirStyle.Icon("chevron-right", UI.Muted, 12.0);
			frameworkElement.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
			stackPanel2.Children.Add(frameworkElement);
			button.Content = stackPanel2;
			DockPanel.SetDock(button, System.Windows.Controls.Dock.Right);
			dockPanel.Children.Add(button);
		}
		FrameworkElement frameworkElement2 = AirStyle.Icon("chevron-right", UI.Muted, 14.0);
		RotateTransform disclosureRotation = new RotateTransform((!p.State.CollapsedGroups.Contains(name)) ? 90 : 0);
		frameworkElement2.RenderTransformOrigin = new Point(0.5, 0.5);
		frameworkElement2.RenderTransform = disclosureRotation;
		System.Windows.Controls.Button button2 = UI.Button(name, delegate
		{
			if (FluentTheme.Motion)
			{
				disclosureRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(p.State.CollapsedGroups.Contains(name) ? 90 : 0, TimeSpan.FromMilliseconds(140.0)));
			}
			ToggleSection(name);
		});
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		frameworkElement2.Margin = new Thickness(0.0, 0.0, 9.0, 0.0);
		stackPanel3.Children.Add(frameworkElement2);
		TextBlock textBlock = UI.Text(name, 15.0, UI.Ink);
		textBlock.FontWeight = FontWeights.SemiBold;
		stackPanel3.Children.Add(textBlock);
		TextBlock textBlock2 = UI.Text(members.Count.ToString(), 12.0, UI.Muted);
		textBlock2.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		stackPanel3.Children.Add(textBlock2);
		button2.Content = stackPanel3;
		AutomationProperties.SetName(button2, (p.State.CollapsedGroups.Contains(name) ? "展开 " : "收起 ") + name);
		AutomationProperties.SetHelpText(button2, "点击展开或收起分组");
		button2.Background = Brushes.Transparent;
		button2.BorderThickness = new Thickness(0.0);
		button2.FontSize = 16.0;
		button2.FontWeight = FontWeights.SemiBold;
		button2.Padding = new Thickness(0.0, 6.0, 0.0, 6.0);
		button2.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
		dockPanel.Children.Add(button2);
		stackPanel.Children.Add(dockPanel);
		if (description != null)
		{
			TextBlock textBlock3 = UI.Text(description, 12.0, UI.Muted);
			textBlock3.TextWrapping = TextWrapping.Wrap;
			textBlock3.Margin = new Thickness(23.0, 0.0, 0.0, (members.Count > 0) ? 14 : 0);
			stackPanel.Children.Add(textBlock3);
		}
		if (!p.State.CollapsedGroups.Contains(name) || !string.IsNullOrWhiteSpace(search.Text))
		{
			if (p.State.LibraryListView && name != "置顶应用")
			{
				StackPanel stackPanel4 = new StackPanel();
				foreach (Entry member in members)
				{
					System.Windows.Controls.Button button3 = UI.AppButton(p, member, label: true);
					if (!canOpen && name == "置顶应用")
					{
						CompactCommandCard(button3, member);
					}
					visibleCards.Add(button3);
					stackPanel4.Children.Add(button3);
				}
				stackPanel.Children.Add(stackPanel4);
			}
			else
			{
				WrapPanel wrapPanel = (canOpen ? new ReorderPanel(p, this) : new WrapPanel());
				foreach (Entry member2 in members)
				{
					if (wrapPanel is ReorderPanel reorderPanel)
					{
						reorderPanel.AddApp(member2);
						continue;
					}
					System.Windows.Controls.Button button4 = name == "置顶应用" ? UI.FluentCard(p, member2, true) : UI.AppButton(p, member2, label: true);
					wrapPanel.Children.Add(button4);
				}
				visibleCards.AddRange(wrapPanel.Children.OfType<System.Windows.Controls.Button>());
				stackPanel.Children.Add(wrapPanel);
			}
		}
		grid.Children.Add(stackPanel);
	}

	public void SelectCard(System.Windows.Controls.Button card)
	{
		if (selectedCard != null)
		{
			selectedCard.Background = FluentTheme.Card;
			selectedCard.BorderBrush = FluentTheme.Line;
		}
		selectedCard = card;
		card.Background = FluentTheme.Selected;
		card.BorderBrush = UI.Accent;
		card.Focus();
		selectedEntryMac = card.Tag as Entry;
		RenderInspectorMac();
	}

	private void ToggleSection(string name)
	{
		if (sectionAnimating)
		{
			return;
		}
		bool flag = p.State.CollapsedGroups.Contains(name);
		if (!FluentTheme.Motion || flag)
		{
			if (!p.State.CollapsedGroups.Remove(name))
			{
				p.State.CollapsedGroups.Add(name);
			}
			p.Save();
			RenderGrid();
			if (FluentTheme.Motion && flag)
			{
				grid.Children.OfType<StackPanel>().FirstOrDefault((StackPanel item) => object.Equals(item.Tag, name))?.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
			return;
		}
		StackPanel stackPanel = grid.Children.OfType<StackPanel>().FirstOrDefault((StackPanel item) => object.Equals(item.Tag, name));
		FrameworkElement frameworkElement = ((stackPanel == null || stackPanel.Children.Count < 2) ? null : (stackPanel.Children[1] as FrameworkElement));
		if (frameworkElement == null)
		{
			p.State.CollapsedGroups.Add(name);
			p.Save();
			RenderGrid();
			return;
		}
		sectionAnimating = true;
		frameworkElement.ClipToBounds = true;
		DoubleAnimation doubleAnimation = new DoubleAnimation(frameworkElement.ActualHeight, 0.0, TimeSpan.FromMilliseconds(200.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		doubleAnimation.Completed += delegate
		{
			sectionAnimating = false;
			p.State.CollapsedGroups.Add(name);
			p.Save();
			RenderGrid();
		};
		frameworkElement.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
	}

	public bool DismissFromEscape()
	{
		// Owned editors and active drag/resize operations retain their own Escape handling.
		if (modalDepth > 0 || reordering || resizing) return false;
		if (openContext != null) { openContext.IsOpen = false; openContext = null; }
		if (IsSelecting) SetCommandSelection(false);
		p.DismissLibrary();
		return true;
	}

	private void Keys(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.Escape && DismissFromEscape())
		{
			e.Handled = true;
			return;
		}
		if (HandleCommandKey(e))
		{
			return;
		}
		bool flag = search.IsKeyboardFocusWithin || visibleCards.Any((System.Windows.Controls.Button card) => card.IsKeyboardFocused);
		if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
		{
			FocusSearch();
			e.Handled = true;
		}
		else if (e.Key == Key.Return && flag && visibleCards.Count > 0)
		{
			if ((selectedCard ?? visibleCards[0]).Tag is Entry a)
			{
				p.Launch(a);
				e.Handled = true;
			}
		}
		else if ((e.Key == Key.Down || e.Key == Key.Up) && flag && visibleCards.Count > 0)
		{
			int num = ((selectedCard == null) ? (-1) : visibleCards.IndexOf(selectedCard));
			num = Math.Max(0, Math.Min(visibleCards.Count - 1, num + ((e.Key == Key.Down) ? 1 : (-1))));
			SelectCard(visibleCards[num]);
			visibleCards[num].BringIntoView();
			e.Handled = true;
		}
	}

	private Border PanelCard(UIElement content)
	{
		return new Border
		{
			Child = content,
			Background = FluentTheme.Card,
			CornerRadius = new CornerRadius(16.0),
			Padding = new Thickness(16.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 16.0),
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(1.0)
		};
	}

	private void RenderRight()
	{
		StackPanel stackPanel = new StackPanel();
		int hour = DateTime.Now.Hour;
		StackPanel stackPanel2 = new StackPanel();
		TextBlock textBlock = UI.Text((hour < 12) ? "早上好" : ((hour < 18) ? "下午好" : "晚上好"), 24.0, UI.Ink);
		textBlock.FontWeight = FontWeights.SemiBold;
		stackPanel2.Children.Add(textBlock);
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "让常用的工具，更贴近你。",
			Foreground = UI.Muted,
			FontSize = 12.0,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 8.0, 0.0, 16.0)
		});
		Border element = new Border
		{
			Height = 3.0,
			Width = 32.0,
			Background = UI.Accent,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
			CornerRadius = new CornerRadius(2.0),
			Margin = new Thickness(0.0, 4.0, 0.0, 2.0)
		};
		stackPanel2.Children.Add(element);
		Border border = PanelCard(stackPanel2);
		border.Background = FluentTheme.Card;
		stackPanel.Children.Add(border);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.Children.Add(Stat(p.State.Apps.Count(p.State.IsVisible), "快捷入口"));
		StackPanel element2 = Stat(p.State.VisibleGroups().Count(), "可见分类");
		Grid.SetColumn(element2, 1);
		grid.Children.Add(element2);
		stackPanel.Children.Add(PanelCard(grid));
		StackPanel stackPanel3 = new StackPanel();
		System.Windows.Controls.Button button = UI.Button("＋  添加应用", delegate
		{
			Modal(AddApplication);
		});
		button.Foreground = Brushes.White;
		button.Background = UI.Ink;
		button.Foreground = FluentTheme.Card;
		button.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		stackPanel3.Children.Add(button);
		stackPanel3.Children.Add(ActionLink("\ue8b7", "从文件夹导入", delegate
		{
			Modal(ScanFolder);
		}));
		stackPanel3.Children.Add(ActionLink("\ue80a", "从开始菜单导入", p.ImportStartMenu));
		stackPanel3.Children.Add(ActionLink("\ue774", "添加网站", delegate
		{
			Modal(AddWebsite);
		}));
		stackPanel3.Children.Add(ActionLink("\ue710", "新建分类", delegate
		{
			Modal(NewGroup);
		}));
		stackPanel.Children.Add(PanelCard(stackPanel3));
		recentPanel = new StackPanel();
		stackPanel.Children.Add(PanelCard(recentPanel));
		rightPanel.Content = stackPanel;
		UpdateRecent();
	}

	private StackPanel Stat(int value, string label)
	{
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = UI.Text(value.ToString(), 23.0, UI.Ink);
		textBlock.FontWeight = FontWeights.SemiBold;
		stackPanel.Children.Add(textBlock);
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			Foreground = UI.Muted,
			FontSize = 11.0,
			Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
		});
		return stackPanel;
	}

	private System.Windows.Controls.Button ActionLink(string glyph, string label, Action action)
	{
		System.Windows.Controls.Button button = SideButton(glyph, label, action);
		button.Padding = new Thickness(0.0, 7.0, 0.0, 7.0);
		button.Margin = new Thickness(0.0);
		return button;
	}

	private FrameworkElement Landscape()
	{
		DrawingGroup drawingGroup = new DrawingGroup();
		using (DrawingContext drawingContext = drawingGroup.Open())
		{
			drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(byte.MaxValue, 222, 151)), null, new Point(162.0, 18.0), 13.0, 13.0);
			drawingContext.DrawGeometry(new SolidColorBrush(Color.FromRgb(173, 193, 219)), null, Geometry.Parse("M0,72 L42,20 78,56 119,11 208,74 Z"));
			drawingContext.DrawGeometry(new SolidColorBrush(Color.FromRgb(199, 215, 210)), null, Geometry.Parse("M0,80 L60,48 93,66 159,40 208,60 208,86 0,86 Z"));
			drawingContext.DrawGeometry(new SolidColorBrush(Color.FromRgb(221, 225, 195)), null, Geometry.Parse("M0,86 C60,49 135,97 208,58 L208,90 0,90 Z"));
		}
		return new Image
		{
			Source = new DrawingImage(drawingGroup),
			Height = 90.0,
			Stretch = Stretch.Fill,
			Opacity = (FluentTheme.Dark ? 0.55 : 0.85)
		};
	}

	public void UpdateRecent()
	{
		base.Dispatcher.BeginInvoke(new Action(Render));
	}

	private void LegacyUpdateRecent()
	{
		if (recentPanel == null)
		{
			return;
		}
		recentPanel.Children.Clear();
		DockPanel dockPanel = new DockPanel();
		System.Windows.Controls.Button button = UI.Button("清空", delegate
		{
			foreach (Entry app2 in p.State.Apps)
			{
				app2.LastLaunchTicks = 0L;
			}
			p.Save();
			UpdateRecent();
			if (selected == "@recent")
			{
				RenderGrid();
			}
		});
		button.Padding = new Thickness(6.0, 3.0, 6.0, 3.0);
		button.Background = FluentTheme.Selected;
		DockPanel.SetDock(button, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(button);
		dockPanel.Children.Add(UI.Text("最近使用", 14.0, UI.Ink));
		recentPanel.Children.Add(dockPanel);
		List<Entry> list = (from a in p.State.Apps
			where a.LastLaunchTicks > 0 && p.State.IsVisible(a)
			orderby a.LastLaunchTicks descending
			select a).Take(5).ToList();
		if (list.Count == 0)
		{
			recentPanel.Children.Add(new TextBlock
			{
				Text = "启动应用后会显示在这里",
				Foreground = UI.Muted,
				FontSize = 12.0,
				Margin = new Thickness(0.0, 16.0, 0.0, 8.0)
			});
		}
		foreach (Entry app in list)
		{
			DockPanel dockPanel2 = new DockPanel
			{
				Margin = new Thickness(0.0, 5.0, 0.0, 5.0)
			};
			FrameworkElement frameworkElement = UI.Icon(app, 26.0);
			frameworkElement.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			dockPanel2.Children.Add(frameworkElement);
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(new TextBlock
			{
				Text = UI.EntryName(app),
				FontSize = 12.0,
				Foreground = UI.Ink,
				TextTrimming = TextTrimming.CharacterEllipsis
			});
			stackPanel.Children.Add(UI.Text(RecentTime(app.LastLaunchTicks), 10.0, UI.Muted));
			dockPanel2.Children.Add(stackPanel);
			Action action = delegate
			{
				p.Launch(app);
			};
			System.Windows.Controls.Button button2 = UI.Button("", action);
			button2.Content = dockPanel2;
			button2.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
			button2.Padding = new Thickness(0.0);
			button2.Background = Brushes.Transparent;
			recentPanel.Children.Add(button2);
		}
	}

	public static string RecentTime(long ticks)
	{
		if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks)
		{
			return "";
		}
		TimeSpan timeSpan = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
		if (!(timeSpan.TotalMinutes < 1.0))
		{
			if (!(timeSpan.TotalHours < 1.0))
			{
				if (!(timeSpan.TotalDays < 1.0))
				{
					return (int)timeSpan.TotalDays + " 天前";
				}
				return (int)timeSpan.TotalHours + " 小时前";
			}
			return (int)timeSpan.TotalMinutes + " 分钟前";
		}
		return "刚刚";
	}

	private void OpenDownloads()
	{
		try
		{
			string text = KnownDownloads();
			Process.Start("explorer.exe", "\"" + text + "\"");
		}
		catch (Exception ex)
		{
			AirStyle.Notice(ex.Message, "下载文件夹");
		}
	}

	private static string KnownDownloads()
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\User Shell Folders");
		string text = ((registryKey == null) ? null : (registryKey.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") as string));
		return string.IsNullOrEmpty(text) ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : Environment.ExpandEnvironmentVariables(text);
	}

	private void AddApplication()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "添加应用或快捷方式",
			Filter = "应用与快捷方式|*.exe;*.lnk;*.appref-ms;*.cmd;*.bat|所有文件|*.*",
			CheckFileExists = true
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			string target = openFileDialog.FileName;
			Entry entry = p.State.Apps.FirstOrDefault((Entry a) => string.Equals(a.Target, target, StringComparison.OrdinalIgnoreCase));
			if (entry != null)
			{
				selected = entry.Group;
				Render();
				return;
			}
			p.State.Apps.Add(new Entry
			{
				Id = "manual:" + target.ToLowerInvariant(),
				Name = System.IO.Path.GetFileNameWithoutExtension(target),
				Target = target,
				IconPath = target,
				Group = (p.State.Groups.Contains(selected) ? selected : "未分类"),
				Manual = true
			});
			p.Refresh();
		}
	}

	public void RenameEntry(Entry app)
	{
		Modal(delegate
		{
			string text = Prompt("重命名快捷入口", "显示名称", UI.EntryName(app));
			if (!string.IsNullOrWhiteSpace(text))
			{
				app.CustomName = text.Trim();
				p.Refresh();
			}
		});
	}

	public void EditEntry(Entry app)
	{
		Modal(delegate
		{
			Window window = new Window
			{
				Owner = this,
				Title = "编辑快捷入口",
				Width = 460.0,
				SizeToContent = SizeToContent.Height,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Background = UI.Panel,
				Foreground = UI.Ink
			};
			StackPanel stackPanel = new StackPanel
			{
				Margin = new Thickness(24.0)
			};
			stackPanel.Children.Add(UI.Text("显示名称", 13.0, UI.Ink));
			System.Windows.Controls.TextBox name = new System.Windows.Controls.TextBox
			{
				Text = UI.EntryName(app),
				Margin = new Thickness(0.0, 8.0, 0.0, 16.0),
				Padding = new Thickness(10.0)
			};
			stackPanel.Children.Add(name);
			stackPanel.Children.Add(UI.Text("目标路径 / 网址", 13.0, UI.Ink));
			System.Windows.Controls.TextBox target = new System.Windows.Controls.TextBox
			{
				Text = app.Target,
				Margin = new Thickness(0.0, 8.0, 0.0, 16.0),
				Padding = new Thickness(10.0)
			};
			stackPanel.Children.Add(target);
			stackPanel.Children.Add(UI.Text("标签（用于搜索，可用空格分隔）", 13.0, UI.Ink));
			System.Windows.Controls.TextBox tags = new System.Windows.Controls.TextBox
			{
				Text = app.Tags,
				Margin = new Thickness(0.0, 8.0, 0.0, 16.0),
				Padding = new Thickness(10.0)
			};
			stackPanel.Children.Add(tags);
			stackPanel.Children.Add(UI.Button("保存", delegate
			{
				if (!string.IsNullOrWhiteSpace(name.Text) && !string.IsNullOrWhiteSpace(target.Text))
				{
					if (app.Website && !Websites.TryUrl(target.Text, out var _))
					{
						AirStyle.Notice("请输入 http 或 https 网址。");
					}
					else
					{
						app.CustomName = name.Text.Trim();
						if (app.Target != target.Text.Trim())
						{
							app.Target = ((app.Website && Websites.TryUrl(target.Text, out var address2)) ? address2.AbsoluteUri : target.Text.Trim());
							app.CustomTarget = app.Target;
						}
						app.Tags = tags.Text.Trim();
						p.Refresh();
						window.DialogResult = true;
					}
				}
			}));
			window.Content = stackPanel;
			UI.ShowModal(window);
		});
	}

	private System.Windows.Controls.Button CommandButton(string icon, string label, Action action, bool primary = false)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		stackPanel.Children.Add(AirStyle.Icon(icon, primary ? FluentTheme.Card : UI.Ink, 16.0));
		if (label != "")
		{
			TextBlock textBlock = UI.Text(label, 13.0, primary ? FluentTheme.Card : UI.Ink);
			textBlock.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
			stackPanel.Children.Add(textBlock);
		}
		System.Windows.Controls.Button button = UI.Button("", action);
		button.Content = stackPanel;
		button.Padding = new Thickness(10.0, 8.0, 10.0, 8.0);
		button.Height = 36.0;
		button.Background = (primary ? UI.Ink : Brushes.Transparent);
		button.BorderThickness = new Thickness(0.0);
		button.ToolTip = label;
		AutomationProperties.SetName(button, label);
		ToolTipService.SetShowOnDisabled(button, value: true);
		return button;
	}

	private static void DetachCommandElement(FrameworkElement element)
	{
		if (element.Parent is System.Windows.Controls.Panel panel)
		{
			panel.Children.Remove(element);
		}
		else if (element.Parent is Border border)
		{
			border.Child = null;
		}
	}

	private void ConfigureCommandBar(StackPanel top, DockPanel center)
	{
		commandHeader = top;
		StackPanel stackPanel = top.Children.OfType<StackPanel>().FirstOrDefault();
		commandSearchFrame = FindElementsMac<Border>(center).FirstOrDefault((Border b) => b.Child is DockPanel root && FindElementsMac<System.Windows.Controls.TextBox>(root).Contains(search));
		if (stackPanel != null)
		{
			DetachCommandElement(stackPanel);
		}
		if (commandSearchFrame != null)
		{
			DetachCommandElement(commandSearchFrame);
		}
		DetachCommandElement(gridViewButton);
		DetachCommandElement(listViewButton);
		top.Children.Clear();
		heading.Visibility = Visibility.Collapsed;
		subtitle.Visibility = Visibility.Collapsed;
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		Border element = new Border
		{
			Child = commandSearchFrame,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			Margin = new Thickness(12.0, 0.0, 132.0, 0.0)
		};
		DockPanel.SetDock(element, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(element);
		if (commandSearchFrame != null)
		{
			commandSearchFrame.Width = 248.0;
			commandSearchFrame.Height = 36.0;
			commandSearchFrame.CornerRadius = new CornerRadius(10.0);
		}
		if (breadcrumbMac != null)
		{
			breadcrumbMac.FontSize = 14.0;
			breadcrumbMac.FontWeight = FontWeights.SemiBold;
			breadcrumbMac.Foreground = UI.Ink;
		}
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		commandBack = CommandButton("arrow-left", "返回", delegate
		{
			NavigateCommandHistory(forward: false);
		});
		commandBack.Content = AirStyle.Icon("arrow-left", UI.Ink, 17.0);
		commandBack.Width = 34.0;
		commandBack.ToolTip = "返回 · Alt+←";
		stackPanel2.Children.Add(commandBack);
		commandForward = CommandButton("arrow", "前进", delegate
		{
			NavigateCommandHistory(forward: true);
		});
		commandForward.Content = AirStyle.Icon("arrow", UI.Ink, 17.0);
		commandForward.Width = 34.0;
		commandForward.ToolTip = "前进 · Alt+→";
		stackPanel2.Children.Add(commandForward);
		stackPanel2.Children.Add(new Border
		{
			Width = 1.0,
			Height = 17.0,
			Background = FluentTheme.Line,
			Margin = new Thickness(10.0, 0.0, 10.0, 0.0)
		});
		if (stackPanel != null)
		{
			stackPanel.Margin = new Thickness(0.0);
			stackPanel2.Children.Add(stackPanel);
		}
		dockPanel.Children.Add(stackPanel2);
		top.Children.Add(dockPanel);
		DockPanel dockPanel2 = new DockPanel
		{
			LastChildFill = true
		};
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right
		};
		commandSummary = UI.Text("", 12.0, UI.Muted);
		commandSummary.Margin = new Thickness(8.0, 0.0, 12.0, 0.0);
		stackPanel3.Children.Add(commandSummary);
		stackPanel3.Children.Add(gridViewButton);
		stackPanel3.Children.Add(listViewButton);
		System.Windows.Controls.Button element2 = CommandButton("info", "信息", OpenCommandInfo);
		stackPanel3.Children.Add(element2);
		DockPanel.SetDock(stackPanel3, System.Windows.Controls.Dock.Right);
		dockPanel2.Children.Add(stackPanel3);
		WrapPanel wrapPanel = new WrapPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		System.Windows.Controls.Button add = null;
		add = CommandButton("plus", "添加", delegate
		{
			OpenCommandAdd(add);
		});
		wrapPanel.Children.Add(add);
		commandEdit = CommandButton("pen", "编辑", EditCommandSelection);
		wrapPanel.Children.Add(commandEdit);
		commandMove = CommandButton("folder", "移动到", MoveCommandSelection);
		wrapPanel.Children.Add(commandMove);
		commandRemove = CommandButton("trash", "移除", RemoveCommandSelection);
		wrapPanel.Children.Add(commandRemove);
		wrapPanel.Children.Add(new Border
		{
			Width = 1.0,
			Height = 18.0,
			Background = FluentTheme.Line,
			Margin = new Thickness(8.0, 0.0, 8.0, 0.0)
		});
		commandSelect = CommandButton("check", "选择", delegate
		{
			SetCommandSelection(!IsSelecting);
		});
		wrapPanel.Children.Add(commandSelect);
		System.Windows.Controls.Button more = null;
		more = CommandButton("more", "更多", delegate
		{
			OpenCommandMore(more);
		});
		wrapPanel.Children.Insert(4, more);
		dockPanel2.Children.Add(wrapPanel);
		top.Children.Add(dockPanel2);
		commandGuide = UI.Text("", 12.0, UI.Muted);
		commandGuide.Margin = new Thickness(3.0, 9.0, 0.0, 14.0);
		top.Children.Add(commandGuide);
		top.Children.Add(new Border
		{
			Height = 1.0,
			Background = FluentTheme.Line,
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		});
		if (count.Parent is DockPanel dockPanel3)
		{
			StackPanel stackPanel4 = dockPanel3.Children.OfType<StackPanel>().FirstOrDefault();
			if (stackPanel4 != null)
			{
				dockPanel3.Children.Remove(stackPanel4);
			}
			commandUndo = CommandButton("refresh", "撤销", UndoCommand);
			commandUndo.Visibility = Visibility.Collapsed;
			DockPanel.SetDock(commandUndo, System.Windows.Controls.Dock.Right);
			dockPanel3.Children.Insert(0, commandUndo);
		}
		top.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is System.Windows.Controls.Panel && e.LeftButton == MouseButtonState.Pressed)
			{
				DragMove();
			}
		};
	}

	private void NavigateCommandHistory(bool forward)
	{
		commandHistory.Remember(search.Text, appScroll?.VerticalOffset ?? 0.0);
		NavigationLocation navigationLocation = (forward ? commandHistory.Forward() : commandHistory.Back());
		ApplyCommandRoute(navigationLocation.Scope, navigationLocation.Query, navigationLocation.Offset);
	}

	private void ApplyCommandRoute(string scope, string query, double offset)
	{
		object obj;
		if (p.State.FindAppFolder(scope) == null)
		{
			switch (scope)
			{
			case "@overview":
			case "全部":
			case "@favorites":
			case "@recent":
				goto IL_007b;
			}
			if (!p.State.VisibleGroups().Contains(scope))
			{
				obj = "@overview";
				goto IL_007c;
			}
		}
		goto IL_007b;
		IL_007c:
		selected = (string)obj;
		IsSelecting = false;
		commandEntries.Clear();
		commandGroups.Clear();
		selectedEntryMac = null;
		search.Text = query ?? "";
		Render();
		base.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
		{
			appScroll?.ScrollToVerticalOffset(offset);
		});
		AirStyle.Enter(grid, 3.0);
		return;
		IL_007b:
		obj = scope;
		goto IL_007c;
	}

	private void SetCommandSelection(bool value)
	{
		IsSelecting = value;
		commandEntries.Clear();
		commandGroups.Clear();
		selectedEntryMac = null;
		Render();
	}

	internal bool HandleCommandAppClick(Entry entry)
	{
		if (!IsSelecting && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
		{
			return false;
		}
		IsSelecting = true;
		commandGroups.Clear();
		if (!commandEntries.Add(entry))
		{
			commandEntries.Remove(entry);
		}
		selectedEntryMac = ((commandEntries.Count == 1) ? commandEntries.First() : null);
		UpdateCommandBar();
		RenderInspectorMac();
		return true;
	}

	private void HandleCommandGroupClick(string group)
	{
		if (!IsSelecting)
		{
			OpenGroupMac(group);
			return;
		}
		commandEntries.Clear();
		if (!commandGroups.Add(group))
		{
			commandGroups.Remove(group);
		}
		selectedGroupMac = group;
		UpdateCommandBar();
		RenderInspectorMac();
	}

	private void UpdateCommandBar()
	{
		if (commandBack != null)
		{
			commandEntries.RemoveWhere((Entry e) => !p.State.Apps.Contains(e));
			commandGroups.RemoveWhere((string g) => !CommandGroupExists(g));
			commandBack.IsEnabled = commandHistory.CanBack;
			commandForward.IsEnabled = commandHistory.CanForward;
			int num = commandEntries.Count + commandGroups.Count;
			commandSummary.Text = ((num > 0) ? ("已选 " + num + " 项") : ((CurrentAppFolder != null) ? (p.State.FolderMembers(CurrentAppFolder).Count((Entry a) => p.State.IsVisible(a) && MatchesSearch(a, search.Text)) + " 个应用") : (OverviewMac ? (p.State.VisibleGroups().Count() + " 个分组") : (p.State.Apps.Count((Entry a) => p.State.IsVisible(a) && MatchesSearch(a, search.Text) && (selected == "全部" || selected == "@overview" || (selected == "@favorites" && a.Favorite) || (selected == "@recent" && a.LastLaunchTicks > 0) || p.State.LibraryGroup(a) == selected)) + " 个项目"))));
			commandEdit.IsEnabled = commandEntries.Count == 1 || (commandGroups.Count <= 1 && CanEditCommandGroup(EditableCommandGroup));
			commandMove.IsEnabled = commandEntries.Count > 0 || CommandFolders.Count > 0 || (!IsSelecting && CurrentAppFolder != null);
			commandRemove.IsEnabled = commandEntries.Count > 0 || commandGroups.Any(CanEditCommandGroup) || CanEditCommandGroup(EditableCommandGroup);
			commandSelect.Background = (IsSelecting ? FluentTheme.Selected : Brushes.Transparent);
			TextBlock textBlock = FindElementsMac<TextBlock>(commandSelect).FirstOrDefault();
			if (textBlock != null)
			{
				textBlock.Text = (IsSelecting ? "完成" : "选择");
			}
			commandSelect.ToolTip = (IsSelecting ? "完成选择 · Esc" : "选择多个项目");
			commandGuide.Text = (IsSelecting ? "点击项目进行选择 · Ctrl+A 全选 · Esc 退出选择；移除入口不会删除软件。" : "单击打开 · 拖到图标中央合并文件夹，拖到边缘排序 · 选择后可批量整理。");
			commandEdit.ToolTip = (commandEdit.IsEnabled ? "编辑选中的入口或当前分组" : "先点击“选择”，再选中一个项目");
			commandMove.ToolTip = "将选中的应用移到另一个分组";
			commandRemove.ToolTip = (commandRemove.IsEnabled ? "移除快捷入口或分组，不删除原软件" : "选择项目后可移除");
			RefreshCommandSelectionVisuals();
			RefreshCollectionSelection();
		}
	}

	private void RefreshCommandSelectionVisuals()
	{
		foreach (System.Windows.Controls.Button visibleCard in visibleCards)
		{
			if (!(visibleCard.Tag is Entry item))
			{
				continue;
			}
			bool flag = commandEntries.Contains(item);
			if (IsSelecting)
			{
				visibleCard.Background = (flag ? FluentTheme.Selected : FluentTheme.Card);
				visibleCard.BorderBrush = (flag ? UI.Accent : FluentTheme.Line);
			}
			if (!(visibleCard.Content is Grid grid))
			{
				continue;
			}
			foreach (Border item2 in (from e in grid.Children.OfType<Border>()
				where e.Tag as string == "command-selection"
				select e).ToList())
			{
				grid.Children.Remove(item2);
			}
			foreach (System.Windows.Controls.Button item3 in grid.Children.OfType<System.Windows.Controls.Button>())
			{
				item3.Visibility = (IsSelecting ? Visibility.Collapsed : Visibility.Visible);
			}
			if (IsSelecting)
			{
				Border element = CommandSelectionBadge(flag);
				grid.Children.Add(element);
			}
		}
		foreach (KeyValuePair<string, System.Windows.Controls.Button> item4 in folderButtonsMac)
		{
			item4.Value.BorderBrush = UI.Accent;
			item4.Value.BorderThickness = ((IsSelecting && commandGroups.Contains(item4.Key)) ? new Thickness(2.0) : new Thickness(0.0));
			if (!(item4.Value.Content is Viewbox { Child: Grid child }))
			{
				continue;
			}
			foreach (Border item5 in (from e in child.Children.OfType<Border>()
				where e.Tag as string == "command-selection"
				select e).ToList())
			{
				child.Children.Remove(item5);
			}
			if (IsSelecting)
			{
				child.Children.Add(CommandSelectionBadge(commandGroups.Contains(item4.Key)));
			}
		}
	}

	private Border CommandSelectionBadge(bool selectedValue)
	{
		Border obj = new Border
		{
			Tag = "command-selection",
			Width = 21.0,
			Height = 21.0,
			CornerRadius = new CornerRadius(11.0),
			BorderBrush = (selectedValue ? UI.Accent : FluentTheme.Line),
			BorderThickness = new Thickness(1.0),
			Background = (selectedValue ? UI.Accent : FluentTheme.Card),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(3.0),
			IsHitTestVisible = false,
			Child = (selectedValue ? AirStyle.Icon("check", Brushes.White, 12.0) : null)
		};
		Grid.SetColumnSpan(obj, 3);
		return obj;
	}

	private void CompactCommandCard(System.Windows.Controls.Button card, Entry entry)
	{
		if (!(card.Content is Grid grid))
		{
			return;
		}
		grid.ColumnDefinitions.Clear();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(44.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		FrameworkElement frameworkElement = grid.Children.OfType<FrameworkElement>().FirstOrDefault();
		if (frameworkElement != null)
		{
			frameworkElement.Width = 34.0;
			frameworkElement.Height = 34.0;
			frameworkElement.Margin = new Thickness(0.0);
			frameworkElement.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
			frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		}
		TextBlock textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
		if (textBlock != null)
		{
			Grid.SetColumn(textBlock, 1);
			textBlock.MaxWidth = 130.0;
			textBlock.Margin = new Thickness(0.0, 0.0, 15.0, 0.0);
			textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
			textBlock.VerticalAlignment = VerticalAlignment.Center;
		}
		foreach (System.Windows.Controls.Button item in grid.Children.OfType<System.Windows.Controls.Button>())
		{
			Grid.SetColumn(item, 1);
		}
		card.Width = (p.State.LibraryListView ? double.NaN : 196.0);
		card.Height = 64.0;
		card.Padding = new Thickness(12.0, 8.0, 12.0, 8.0);
		card.Margin = new Thickness(0.0, 0.0, 10.0, 10.0);
		card.Template = UI.ButtonTemplate(13.0);
	}

	private void OpenCommandAdd(System.Windows.Controls.Button anchor)
	{
		System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu
		{
			PlacementTarget = anchor,
			Placement = PlacementMode.Bottom,
			MinWidth = 200.0,
			FontFamily = AirStyle.Font
		};
		Action<string, string, Action> obj = delegate(string icon, string label, Action action)
		{
			System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
			{
				Header = label,
				Icon = AirStyle.Icon(icon, UI.Ink, 16.0)
			};
			menuItem.Click += delegate
			{
				Modal(action);
			};
			menu.Items.Add(menuItem);
		};
		obj("plus", "添加应用", AddApplication);
		obj("globe", "添加网站", AddWebsite);
		obj("bookmark", "收藏文件夹", FavoriteFolder);
		menu.Items.Add(new Separator());
		obj("folder", "新建分组", NewGroup);
		TrackContext(menu);
		menu.Closed += delegate
		{
			EndContext(menu);
		};
		menu.IsOpen = true;
	}

	private void EditCommandSelection()
	{
		if (EditCollectionCommand())
		{
			return;
		}
		if (commandEntries.Count == 1)
		{
			EditEntry(commandEntries.First());
			return;
		}
		string group = EditableCommandGroup;
		if (!LibraryEditing.CanEditGroup(group))
		{
			return;
		}
		Modal(delegate
		{
			string text = Prompt("重命名分组", "分组名称", group);
			if (!string.IsNullOrWhiteSpace(text) && !(text.Trim() == group))
			{
				if (!LibraryEditing.RenameGroup(p.State, group, text))
				{
					AirStyle.Notice("名称不可用，请使用不重复的分组名称。");
				}
				else
				{
					string renamed = text.Trim();
					commandUndoAction = delegate(Config state)
					{
						LibraryEditing.RenameGroup(state, renamed, group);
					};
					if (selected == group)
					{
						selected = renamed;
					}
					FinishCommandChange("分组已重命名");
				}
			}
		});
	}

	private void MoveCommandSelection()
	{
		if (MoveCollectionsCommand() || commandEntries.Count == 0)
		{
			return;
		}
		List<Entry> targets = commandEntries.ToList();
		Window window = new Window
		{
			Owner = this,
			Title = "移动到分组",
			Width = 420.0,
			SizeToContent = SizeToContent.Height,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			ResizeMode = ResizeMode.NoResize
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(24.0)
		};
		stackPanel.Children.Add(UI.Text("选择目标分组 · " + targets.Count + " 个应用", 14.0, UI.Ink));
		foreach (string item in p.State.VisibleGroups())
		{
			string target = item;
			System.Windows.Controls.Button button = UI.Button(item, delegate
			{
				commandUndoAction = LibraryEditing.MoveEntries(p.State, targets, target);
				if (commandUndoAction != null)
				{
					FinishCommandChange("已移到「" + target + "」");
				}
				window.DialogResult = true;
			});
			button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
			stackPanel.Children.Add(button);
		}
		window.Content = stackPanel;
		Modal(delegate
		{
			UI.ShowModal(window);
		});
	}

	private void RemoveCommandSelection()
	{
		if (RemoveCollectionsCommand())
		{
			return;
		}
		List<Entry> apps = commandEntries.ToList();
		List<string> groups = ((commandGroups.Count > 0) ? commandGroups.Where(CanEditCommandGroup).ToList() : (CanEditCommandGroup(EditableCommandGroup) ? new List<string> { EditableCommandGroup } : new List<string>()));
		if (apps.Count == 0 && groups.Count == 0)
		{
			return;
		}
		string message = ((apps.Count > 0) ? ("将移除 " + apps.Count + " 个快捷入口，不会删除软件或原文件。可立即撤销，也可在基础设置中恢复。") : ("将移除 " + groups.Count + " 个分组，里面的应用会回到「未分类」。不会删除任何软件，可立即撤销。"));
		Modal(delegate
		{
			if (AirStyle.Confirm("确认移除", message, "移除"))
			{
				commandUndoAction = ((apps.Count > 0) ? LibraryEditing.RemoveEntries(p.State, apps) : LibraryEditing.RemoveGroups(p.State, groups));
				if (!p.State.Groups.Contains(selected) && !selected.StartsWith("@") && selected != "全部")
				{
					selected = "@overview";
				}
				FinishCommandChange("已移除，可撤销");
			}
		});
	}

	private void FinishCommandChange(string message)
	{
		IsSelecting = false;
		commandEntries.Clear();
		commandGroups.Clear();
		selectedEntryMac = null;
		p.Refresh();
		UpdateCommandBar();
		if (commandUndo != null)
		{
			commandUndo.Visibility = ((commandUndoAction == null) ? Visibility.Collapsed : Visibility.Visible);
			commandUndo.ToolTip = "撤销上一次整理 · Ctrl+Z";
		}
		count.Text = message;
	}

	private void UndoCommand()
	{
		if (commandUndoAction != null)
		{
			Action<Config> action = commandUndoAction;
			commandUndoAction = null;
			action(p.State);
			FinishCommandChange("已撤销上一次整理");
		}
	}

	private void OpenCommandInfo()
	{
		RenderInspectorMac();
		if (!(rightPanel.Content is FrameworkElement frameworkElement))
		{
			return;
		}
		rightPanel.Content = null;
		frameworkElement.Margin = new Thickness(24.0);
		Window window = new Window
		{
			Owner = this,
			Title = "信息",
			Width = 420.0,
			SizeToContent = SizeToContent.Height,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			ResizeMode = ResizeMode.NoResize,
			Content = frameworkElement
		};
		try
		{
			Modal(delegate
			{
				UI.ShowModal(window);
			});
		}
		finally
		{
			window.Content = null;
			RenderInspectorMac();
		}
	}

	private bool HandleCommandKey(System.Windows.Input.KeyEventArgs e)
	{
		bool flag = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;
		if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.None && (e.SystemKey == Key.Left || e.Key == Key.Left))
		{
			if (commandHistory.CanBack)
			{
				NavigateCommandHistory(forward: false);
			}
			e.Handled = true;
			return true;
		}
		if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.None && (e.SystemKey == Key.Right || e.Key == Key.Right))
		{
			if (commandHistory.CanForward)
			{
				NavigateCommandHistory(forward: true);
			}
			e.Handled = true;
			return true;
		}
		if (e.Key == Key.Escape && IsSelecting)
		{
			SetCommandSelection(value: false);
			e.Handled = true;
			return true;
		}
		if (flag)
		{
			return false;
		}
		if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
		{
			IsSelecting = true;
			commandEntries.Clear();
			commandGroups.Clear();
			if (OverviewMac)
			{
				foreach (string item2 in folderButtonsMac.Keys.Where(LibraryEditing.CanEditGroup))
				{
					commandGroups.Add(item2);
				}
			}
			else
			{
				foreach (System.Windows.Controls.Button visibleCard in visibleCards)
				{
					if (visibleCard.Tag is Entry item)
					{
						commandEntries.Add(item);
					}
				}
			}
			UpdateCommandBar();
			e.Handled = true;
			return true;
		}
		if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None && commandUndoAction != null)
		{
			UndoCommand();
			e.Handled = true;
			return true;
		}
		if (e.Key == Key.Delete && (commandEntries.Count > 0 || commandGroups.Count > 0))
		{
			System.Windows.Controls.Button button = commandRemove;
			if (button != null && button.IsEnabled)
			{
				RemoveCommandSelection();
				e.Handled = true;
				return true;
			}
		}
		if (e.Key == Key.F2)
		{
			System.Windows.Controls.Button button2 = commandEdit;
			if (button2 != null && button2.IsEnabled)
			{
				EditCommandSelection();
				e.Handled = true;
				return true;
			}
		}
		if (IsSelecting && (e.Key == Key.Return || e.Key == Key.Space) && Keyboard.FocusedElement is System.Windows.Controls.Button button3)
		{
			if (button3.Tag is Entry entry)
			{
				HandleCommandAppClick(entry);
			}
			else
			{
				if (!(button3.Tag is string text) || !CommandGroupExists(text))
				{
					return false;
				}
				HandleCommandGroupClick(text);
			}
			e.Handled = true;
			return true;
		}
		return false;
	}

	private bool CommandGroupExists(string scope)
	{
		if (!p.State.Groups.Contains(scope))
		{
			return p.State.FindAppFolder(scope) != null;
		}
		return true;
	}

	private bool CanEditCommandGroup(string scope)
	{
		if (p.State.FindAppFolder(scope) == null)
		{
			if (p.State.Groups.Contains(scope))
			{
				return LibraryEditing.CanEditGroup(scope);
			}
			return false;
		}
		return true;
	}

	private bool RenderAppFolderScope()
	{
		collectionCards.Clear();
		AppFolder folder = CurrentAppFolder;
		if (folder == null)
		{
			return false;
		}
		double offset = ((!(lastGridScope == selected)) ? 0.0 : (appScroll?.VerticalOffset ?? 0.0));
		lastGridScope = selected;
		gridRevision++;
		grid.Children.Clear();
		visibleCards.Clear();
		folderButtonsMac.Clear();
		System.Windows.Controls.Button button = UI.Button("返回「" + folder.Group + "」", delegate
		{
			NavigateMac(folder.Group);
		});
		button.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		button.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
		grid.Children.Add(button);
		List<Entry> list = p.State.OrderedEntries(from a in p.State.FolderMembers(folder)
			where p.State.IsVisible(a) && MatchesSearch(a, search.Text)
			select a).ToList();
		AddSection(folder.Name, list, canOpen: false, "拖入应用可继续添加；移动到所属分组可将应用移出文件夹。");
		if (list.Count == 0)
		{
			grid.Children.Add(UI.Text("文件夹为空，可拖入应用。", 14.0, UI.Muted));
		}
		count.Text = list.Count + " 个应用 · 仅整理快捷入口";
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			appScroll?.ScrollToVerticalOffset(offset);
		});
		UpdateCommandBar();
		return true;
	}

	private void AddAppFolderSection(IEnumerable<AppFolder> folders, string title)
	{
		List<AppFolder> list = folders.Where((AppFolder f) => p.State.VisibleGroups().Contains(f.Group)).ToList();
		if (list.Count == 0)
		{
			return;
		}
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 22.0)
		};
		TextBlock textBlock = UI.Text(title + "  " + list.Count, 14.0, UI.Ink);
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 13.0);
		stackPanel.Children.Add(textBlock);
		WrapPanel wrapPanel = new WrapPanel();
		foreach (AppFolder item in list)
		{
			string scope = "@folder:" + item.Id;
			List<Entry> list2 = p.State.FolderMembers(item).Where(p.State.IsVisible).ToList();
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(58.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			UniformGrid uniformGrid = new UniformGrid
			{
				Columns = 2,
				Rows = 2,
				Width = 36.0,
				Height = 36.0,
				Margin = new Thickness(0.0)
			};
			foreach (Entry item2 in list2.Take(4))
			{
				FrameworkElement frameworkElement = UI.Icon(item2, 18.0);
				frameworkElement.Margin = new Thickness(2.0);
				uniformGrid.Children.Add(frameworkElement);
			}
			Border element = new Border
			{
				Child = uniformGrid,
				Background = FluentTheme.Background,
				CornerRadius = new CornerRadius(10.0),
				Padding = new Thickness(4.0),
				Width = 46.0,
				Height = 46.0,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left
			};
			grid.Children.Add(element);
			StackPanel stackPanel2 = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			TextBlock textBlock2 = UI.Text(item.Name, 13.0, UI.Ink);
			textBlock2.FontWeight = FontWeights.SemiBold;
			textBlock2.TextTrimming = TextTrimming.CharacterEllipsis;
			stackPanel2.Children.Add(textBlock2);
			stackPanel2.Children.Add(UI.Text(list2.Count + " 个应用" + (item.InWorkspace ? " · 工作空间" : ""), 11.0, UI.Muted));
			Grid.SetColumn(stackPanel2, 1);
			grid.Children.Add(stackPanel2);
			System.Windows.Controls.Button button = null;
			button = UI.Button("", delegate
			{
				HandleCommandGroupClick(scope);
			});
			button.Content = grid;
			button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
			button.Tag = scope;
			button.Width = (p.State.LibraryListView ? Math.Max(260.0, base.ActualWidth - leftColumn.Width.Value - 100.0) : 235.0);
			button.Height = 84.0;
			button.Padding = new Thickness(12.0);
			button.Margin = new Thickness(0.0, 0.0, 12.0, 12.0);
			button.Template = UI.ButtonTemplate(14.0);
			button.ToolTip = "单击打开文件夹；拖入应用可添加";
			AutomationProperties.SetName(button, "文件夹 " + item.Name + "，" + list2.Count + " 个应用");
			AttachCollectionDrop(button, item);
			collectionCards[scope] = button;
			wrapPanel.Children.Add(button);
		}
		stackPanel.Children.Add(wrapPanel);
		this.grid.Children.Add(stackPanel);
	}

	private void RefreshCollectionSelection()
	{
		foreach (KeyValuePair<string, System.Windows.Controls.Button> collectionCard in collectionCards)
		{
			bool flag = IsSelecting && commandGroups.Contains(collectionCard.Key);
			collectionCard.Value.Background = (flag ? FluentTheme.Selected : FluentTheme.Card);
			collectionCard.Value.BorderBrush = (flag ? UI.Accent : FluentTheme.Line);
			if (!(collectionCard.Value.Content is Grid grid))
			{
				continue;
			}
			foreach (Border item in (from b in grid.Children.OfType<Border>()
				where b.Tag as string == "command-selection"
				select b).ToList())
			{
				grid.Children.Remove(item);
			}
			if (IsSelecting)
			{
				grid.Children.Add(CommandSelectionBadge(flag));
			}
		}
	}

	internal void AttachAppFolderDrop(System.Windows.Controls.Button button, Entry target)
	{
		button.AllowDrop = true;
		button.DragOver += delegate(object s, System.Windows.DragEventArgs e)
		{
			Entry entry = ReorderPanel.DraggedEntry(e.Data);
			Point position = e.GetPosition(button);
			bool flag = position.X > button.ActualWidth * 0.23 && position.X < button.ActualWidth * 0.77 && position.Y > button.ActualHeight * 0.12 && position.Y < button.ActualHeight * 0.88;
			if (entry != null && entry != target && flag && CurrentAppFolder == null && !(p.State.LibraryGroup(entry) != p.State.LibraryGroup(target)))
			{
				e.Handled = true;
				e.Effects = System.Windows.DragDropEffects.Move;
				button.BorderBrush = UI.Accent;
				button.Background = FluentTheme.Selected;
				button.ToolTip = "松开以合并为文件夹";
			}
		};
		button.DragLeave += delegate
		{
			button.BorderBrush = FluentTheme.Line;
			button.Background = FluentTheme.Card;
		};
		button.Drop += delegate(object s, System.Windows.DragEventArgs e)
		{
			Entry moving = ReorderPanel.DraggedEntry(e.Data);
			Point position = e.GetPosition(button);
			if (moving != null && moving != target && !(position.Y < button.ActualHeight * 0.12) && !(position.Y > button.ActualHeight * 0.88) && !(position.X < button.ActualWidth * 0.23) && !(position.X > button.ActualWidth * 0.77) && CurrentAppFolder == null)
			{
				string oldMoving = moving.FolderId;
				string oldTarget = target.FolderId;
				string oldMovingGroup = moving.Group;
				string oldTargetGroup = target.Group;
				AppFolder folder = p.State.CombineEntries(moving, target);
				if (folder != null)
				{
					commandUndoAction = delegate(Config state)
					{
						moving.FolderId = oldMoving;
						target.FolderId = oldTarget;
						moving.Group = oldMovingGroup;
						target.Group = oldTargetGroup;
						if (string.IsNullOrEmpty(oldTarget))
						{
							state.AppFolders.RemoveAll((AppFolder f) => f.Id == folder.Id);
						}
					};
					ReorderPanel.CommitCurrent();
					SuppressShortcutClick();
					FinishCommandChange("已创建文件夹，可通过顶部编辑名称");
					e.Effects = System.Windows.DragDropEffects.Move;
					e.Handled = true;
				}
			}
		};
	}

	private void AttachCollectionDrop(System.Windows.Controls.Button button, AppFolder folder)
	{
		button.AllowDrop = true;
		button.DragOver += delegate(object s, System.Windows.DragEventArgs e)
		{
			Entry entry = ReorderPanel.DraggedEntry(e.Data);
			if (entry != null && !(entry.FolderId == folder.Id))
			{
				e.Handled = true;
				e.Effects = System.Windows.DragDropEffects.Move;
				button.BorderBrush = UI.Accent;
				button.Background = FluentTheme.Selected;
			}
		};
		button.DragLeave += delegate
		{
			button.BorderBrush = FluentTheme.Line;
			button.Background = FluentTheme.Card;
		};
		button.Drop += delegate(object s, System.Windows.DragEventArgs e)
		{
			Entry app = ReorderPanel.DraggedEntry(e.Data);
			if (app != null)
			{
				string oldGroup = app.Group;
				string oldFolder = app.FolderId;
				int order = app.SortOrder;
				if (p.State.PutInFolder(app, folder))
				{
					commandUndoAction = delegate
					{
						app.Group = oldGroup;
						app.FolderId = oldFolder;
						app.SortOrder = order;
					};
					ReorderPanel.CommitCurrent();
					SuppressShortcutClick();
					FinishCommandChange("已加入「" + folder.Name + "」");
					e.Handled = true;
					e.Effects = System.Windows.DragDropEffects.Move;
				}
			}
		};
	}

	private bool EditCollectionCommand()
	{
		AppFolder folder = p.State.FindAppFolder(EditableCommandGroup);
		if (folder == null)
		{
			return false;
		}
		Modal(delegate
		{
			string text = Prompt("重命名文件夹", "文件夹名称", folder.Name);
			if (!string.IsNullOrWhiteSpace(text) && !(text.Trim() == folder.Name))
			{
				string old = folder.Name;
				folder.Name = text.Trim();
				commandUndoAction = delegate
				{
					folder.Name = old;
				};
				FinishCommandChange("文件夹已重命名");
			}
		});
		return true;
	}

	private bool MoveCollectionsCommand()
	{
		List<AppFolder> folders = CommandFolders;
		if (folders.Count == 0 && CurrentAppFolder != null && !IsSelecting)
		{
			folders.Add(CurrentAppFolder);
		}
		if (folders.Count == 0)
		{
			return false;
		}
		System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu
		{
			FontFamily = AirStyle.Font
		};
		foreach (string item in p.State.VisibleGroups())
		{
			string destination = item;
			System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
			{
				Header = item
			};
			menuItem.Click += delegate
			{
				Dictionary<AppFolder, string> before = folders.ToDictionary((AppFolder f) => f, (AppFolder f) => f.Group);
				foreach (AppFolder item2 in folders)
				{
					p.State.MoveAppFolder(item2, destination);
				}
				commandUndoAction = delegate(Config state)
				{
					foreach (KeyValuePair<AppFolder, string> item3 in before)
					{
						state.MoveAppFolder(item3.Key, item3.Value);
					}
				};
				FinishCommandChange("文件夹已移到「" + destination + "」");
			};
			menu.Items.Add(menuItem);
		}
		menu.PlacementTarget = commandMove;
		TrackContext(menu);
		menu.Closed += delegate
		{
			EndContext(menu);
		};
		menu.IsOpen = true;
		return true;
	}

	private bool RemoveCollectionsCommand()
	{
		List<AppFolder> folders = CommandFolders;
		if (folders.Count == 0 && CurrentAppFolder != null && !IsSelecting)
		{
			folders.Add(CurrentAppFolder);
		}
		if (folders.Count == 0)
		{
			return false;
		}
		Modal(delegate
		{
			if (AirStyle.Confirm("解散文件夹", "应用会回到所属分组，不删除原文件。可撤销。", "解散"))
			{
				string text = folders[0].Group;
				commandUndoAction = p.State.RemoveAppFolders(folders);
				if (CurrentAppFolder == null && selected.StartsWith("@folder:"))
				{
					selected = text;
				}
				FinishCommandChange("文件夹已解散");
			}
		});
		return true;
	}

	private void OpenCommandMore(System.Windows.Controls.Button anchor)
	{
		System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu
		{
			PlacementTarget = anchor,
			FontFamily = AirStyle.Font,
			MinWidth = 205.0
		};
		Action<string, bool, Action> obj = delegate(string label, bool enabled, Action action)
		{
			System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
			{
				Header = label,
				IsEnabled = enabled
			};
			menuItem.Click += delegate
			{
				action();
			};
			menu.Items.Add(menuItem);
		};
		List<Entry> apps = commandEntries.ToList();
		List<AppFolder> folders = CommandFolders;
		if (folders.Count == 0 && CurrentAppFolder != null && !IsSelecting)
		{
			folders.Add(CurrentAppFolder);
		}
		obj((apps.Count > 0 && apps.All((Entry a) => a.Favorite)) ? "取消收藏" : "添加到收藏", apps.Count > 0, delegate
		{
			Dictionary<Entry, bool> before = apps.ToDictionary((Entry a) => a, (Entry a) => a.Favorite);
			bool flag = !apps.All((Entry a) => a.Favorite);
			foreach (Entry item in apps)
			{
				item.Favorite = flag;
			}
			commandUndoAction = delegate
			{
				foreach (KeyValuePair<Entry, bool> item2 in before)
				{
					item2.Key.Favorite = item2.Value;
				}
			};
			FinishCommandChange(flag ? "已收藏" : "已取消收藏");
		});
		obj((apps.Count > 0 && apps.All((Entry a) => a.TopPinned)) ? "取消置顶" : "置顶应用", apps.Count > 0, delegate
		{
			Dictionary<Entry, bool> before = apps.ToDictionary((Entry a) => a, (Entry a) => a.TopPinned);
			bool flag = !apps.All((Entry a) => a.TopPinned);
			foreach (Entry item3 in apps)
			{
				item3.TopPinned = flag;
			}
			commandUndoAction = delegate
			{
				foreach (KeyValuePair<Entry, bool> item4 in before)
				{
					item4.Key.TopPinned = item4.Value;
				}
			};
			FinishCommandChange(flag ? "已置顶" : "已取消置顶");
		});
		obj((folders.Count > 0 && folders.All((AppFolder f) => f.InWorkspace)) ? "从工作空间移除" : "添加文件夹到工作空间", folders.Count > 0, delegate
		{
			Dictionary<AppFolder, bool> before = folders.ToDictionary((AppFolder f) => f, (AppFolder f) => f.InWorkspace);
			bool flag = !folders.All((AppFolder f) => f.InWorkspace);
			foreach (AppFolder item5 in folders)
			{
				item5.InWorkspace = flag;
			}
			commandUndoAction = delegate
			{
				foreach (KeyValuePair<AppFolder, bool> item6 in before)
				{
					item6.Key.InWorkspace = item6.Value;
				}
			};
			FinishCommandChange(flag ? "已添加到工作空间" : "已从工作空间移除");
		});
		menu.Items.Add(new Separator());
		obj("管理分类", arg2: true, delegate
		{
			Modal(Manage);
		});
		TrackContext(menu);
		menu.Closed += delegate
		{
			EndContext(menu);
		};
		menu.IsOpen = true;
	}

	private static Color MacColor(string value)
	{
		return (Color)ColorConverter.ConvertFromString(value);
	}

	private static Brush MacGradient(string top, string bottom)
	{
		return new LinearGradientBrush(MacColor(top), MacColor(bottom), 90.0);
	}

	private void ConfigureMacChrome()
	{
		if (macChromeReady)
		{
			return;
		}
		macChromeReady = true;
		Border border = shell.Children.OfType<Border>().FirstOrDefault();
		if (border != null)
		{
			border.Margin = new Thickness(0.0);
			border.CornerRadius = new CornerRadius(0.0);
			border.Background = FluentTheme.Sidebar;
			border.BorderThickness = new Thickness(0.0, 0.0, 1.0, 0.0);
		}
		DockPanel dockPanel = shell.Children.OfType<DockPanel>().FirstOrDefault((DockPanel e) => Grid.GetColumn(e) == 1);
		if (dockPanel != null)
		{
			dockPanel.Margin = new Thickness(32.0, 24.0, 28.0, 18.0);
		}
		StackPanel stackPanel = (heading.Parent as DockPanel)?.Parent as StackPanel;
		if (stackPanel != null)
		{
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal,
				Margin = new Thickness(-5.0, -5.0, 0.0, 10.0)
			};
			System.Windows.Controls.Button button = workspaceHome = UI.Button("工作空间", delegate
			{
				NavigateMac(p.State.ShowWorkspace ? "@overview" : "全部");
			});
			button.FontSize = 12.0;
			button.Padding = new Thickness(5.0, 6.0, 5.0, 6.0);
			button.Background = Brushes.Transparent;
			button.BorderThickness = new Thickness(0.0);
			button.Foreground = UI.Muted;
			stackPanel2.Children.Add(button);
			FrameworkElement frameworkElement = AirStyle.Icon("chevron-right", UI.Muted, 10.0);
			frameworkElement.Margin = new Thickness(3.0, 0.0, 3.0, 0.0);
			stackPanel2.Children.Add(frameworkElement);
			breadcrumbMac = UI.Text("分组与应用", 12.0, UI.Muted);
			StackPanel stackPanel3 = new StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal
			};
			stackPanel3.Children.Add(breadcrumbMac);
			FrameworkElement frameworkElement2 = AirStyle.Icon("chevron-down", UI.Muted, 10.0);
			frameworkElement2.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
			stackPanel3.Children.Add(frameworkElement2);
			System.Windows.Controls.Button scope = null;
			scope = UI.Button("", delegate
			{
				OpenScopeMenuMac(scope);
			});
			scope.Content = stackPanel3;
			scope.Padding = new Thickness(5.0, 6.0, 5.0, 6.0);
			scope.Background = Brushes.Transparent;
			scope.BorderThickness = new Thickness(0.0);
			scope.ToolTip = "切换分组或视图";
			AutomationProperties.SetName(scope, "切换当前位置");
			stackPanel2.Children.Add(scope);
			stackPanel.Children.Insert(0, stackPanel2);
		}
		heading.FontSize = 25.0;
		heading.FontWeight = FontWeights.SemiBold;
		foreach (Border item in from b in FindElementsMac<Border>(shell)
			where b.Child is TextBlock textBlock && textBlock.Text == "L"
			select b)
		{
			item.Background = UI.Ink;
			((TextBlock)item.Child).Foreground = FluentTheme.Card;
			item.CornerRadius = new CornerRadius(11.0);
		}
		shell.Children.Remove(rightPanel);
		rightPanel.Margin = new Thickness(24.0, 72.0, 24.0, 24.0);
		Border element = new Border
		{
			Child = rightPanel,
			Visibility = Visibility.Collapsed,
			Background = FluentTheme.Card,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(1.0, 0.0, 0.0, 0.0)
		};
		Grid.SetColumn(element, 2);
		shell.Children.Add(element);
		foreach (Border item2 in from b in FindElementsMac<Border>(dockPanel)
			where b.Child is DockPanel root && FindElementsMac<System.Windows.Controls.TextBox>(root).Contains(search)
			select b)
		{
			item2.Height = 40.0;
			item2.Width = 350.0;
			item2.MaxWidth = 410.0;
			item2.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
			item2.CornerRadius = new CornerRadius(11.0);
		}
		selectedGroupMac = p.State.VisibleGroups().FirstOrDefault((string g) => g != "未分类" && p.State.Apps.Any((Entry a) => p.State.IsVisible(a) && p.State.LibraryGroup(a) == g));
		if (stackPanel != null)
		{
			ConfigureCommandBar(stackPanel, dockPanel);
		}
	}

	private static IEnumerable<T> FindElementsMac<T>(DependencyObject root) where T : DependencyObject
	{
		if (root == null)
		{
			yield break;
		}
		if (root is T val)
		{
			yield return val;
		}
		foreach (DependencyObject item in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
		{
			foreach (T item2 in FindElementsMac<T>(item))
			{
				yield return item2;
			}
		}
	}

	private void NavigateMac(string scope)
	{
		commandHistory.Remember(search.Text, appScroll?.VerticalOffset ?? 0.0);
		commandHistory.Visit(scope);
		ApplyCommandRoute(scope, "", 0.0);
	}

	private void OpenScopeMenuMac(System.Windows.Controls.Button anchor)
	{
		System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu
		{
			PlacementTarget = anchor,
			Placement = PlacementMode.Bottom,
			FontFamily = AirStyle.Font,
			MinWidth = 190.0
		};
		Action<string, string> action = delegate(string id, string label)
		{
			System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
			{
				Header = label,
				IsCheckable = true,
				IsChecked = (selected == id)
			};
			menuItem.Click += delegate
			{
				NavigateMac(id);
			};
			menu.Items.Add(menuItem);
		};
		if (p.State.ShowWorkspace) action("@overview", "分组与应用");
		action("全部", "所有应用");
		action("@favorites", "收藏");
		action("@recent", "最近使用");
		menu.Items.Add(new Separator());
		foreach (string item in p.State.VisibleGroups())
		{
			action(item, item);
		}
		TrackContext(menu);
		menu.Closed += delegate
		{
			EndContext(menu);
		};
		menu.IsOpen = true;
	}

	private void PaintMacNavigation()
	{
		foreach (System.Windows.Controls.Button item in navigation.Children.OfType<System.Windows.Controls.Button>())
		{
			bool flag = item.Tag as string == "selected-navigation";
			item.BorderThickness = (flag ? new Thickness(1.0) : new Thickness(0.0));
			item.BorderBrush = FluentTheme.Line;
			item.Background = (flag ? MacGradient(FluentTheme.Dark ? "#303034" : "#FFFFFF", FluentTheme.Dark ? "#242428" : "#F0F0F3") : Brushes.Transparent);
			foreach (TextBlock item2 in FindElementsMac<TextBlock>(item))
			{
				item2.Foreground = (flag ? UI.Ink : UI.Muted);
			}
			foreach (System.Windows.Shapes.Path item3 in FindElementsMac<System.Windows.Shapes.Path>(item))
			{
				item3.Stroke = (flag ? UI.Ink : UI.Muted);
			}
			if (flag)
			{
				item.Effect = new DropShadowEffect
				{
					Color = Colors.Black,
					BlurRadius = 3.0,
					ShadowDepth = 1.0,
					Opacity = (FluentTheme.Dark ? 0.16 : 0.04)
				};
			}
		}
	}

	private void RenderOverviewMac()
	{
		double offset = ((!(lastGridScope == "@overview")) ? 0.0 : (appScroll?.VerticalOffset ?? 0.0));
		lastGridScope = "@overview";
		gridRevision++;
		grid.Children.Clear();
		folderButtonsMac.Clear();
		visibleCards.Clear();
		collectionCards.Clear();
		selectedEntryMac = null;
		AddPinnedArea();
		AddAppFolderSection(p.State.AppFolders.Where((AppFolder f) => f.InWorkspace), "工作空间文件夹");
		List<string> list = p.State.VisibleGroups().ToList();
		UniformGrid uniformGrid = new UniformGrid
		{
			Columns = Math.Max(2, Math.Min(5, (int)((((base.ActualWidth > 0.0) ? base.ActualWidth : base.Width) - leftColumn.Width.Value - 64.0) / 250.0))),
			Margin = new Thickness(-9.0, 4.0, -9.0, 0.0)
		};
		foreach (string item in list)
		{
			System.Windows.Controls.Button button = FolderCardMac(item);
			folderButtonsMac[item] = button;
			uniformGrid.Children.Add(button);
		}
		grid.Children.Add(uniformGrid);
		heading.Text = "我的工作空间";
		subtitle.Text = "让常用的应用，各就其位。";
		count.Text = list.Count + " 个分组 · 单击打开分组或应用";
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			appScroll?.ScrollToVerticalOffset(offset);
		});
		UpdateCommandBar();
	}

	private Viewbox FolderArtMac(string group)
	{
		bool flag = IsSelecting && commandGroups.Contains(group);
		List<Entry> list = p.State.Apps.Where((Entry a) => p.State.IsVisible(a) && p.State.LibraryGroup(a) == group).ToList();
		Grid grid = new Grid
		{
			Width = 224.0,
			Height = 206.0,
			ClipToBounds = false
		};
		Border element = new Border
		{
			CornerRadius = new CornerRadius(27.0),
			Background = (flag ? MacGradient("#85B3FB", "#6494EB") : MacGradient(FluentTheme.Dark ? "#32343B" : "#E6E7EB", FluentTheme.Dark ? "#25262D" : "#F4F4F7")),
			BorderBrush = (flag ? UI.Brush("#8CB8FF") : FluentTheme.Line),
			BorderThickness = new Thickness(0.5)
		};
		grid.Children.Add(element);
		Canvas canvas = new Canvas
		{
			IsHitTestVisible = false
		};
		canvas.Children.Add(new Border
		{
			Width = 174.0,
			Height = 132.0,
			CornerRadius = new CornerRadius(13.0),
			Background = (flag ? UI.Brush("#CFE0FF") : UI.Brush(FluentTheme.Dark ? "#41434B" : "#F9F9FA")),
			RenderTransform = new RotateTransform(-4.0, 80.0, 70.0),
			Margin = new Thickness(24.0, 17.0, 0.0, 0.0)
		});
		canvas.Children.Add(new Border
		{
			Width = 181.0,
			Height = 132.0,
			CornerRadius = new CornerRadius(13.0),
			Background = (flag ? UI.Brush("#E1EBFF") : UI.Brush(FluentTheme.Dark ? "#555762" : "#FFFFFF")),
			RenderTransform = new RotateTransform(3.0, 80.0, 70.0),
			Margin = new Thickness(18.0, 28.0, 0.0, 0.0)
		});
		StackPanel stackPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			Margin = new Thickness(28.0, 39.0, 0.0, 0.0)
		};
		foreach (Entry item in list.Take(3))
		{
			FrameworkElement frameworkElement = UI.Icon(item, 21.0);
			frameworkElement.Margin = new Thickness(0.0, 0.0, 9.0, 0.0);
			stackPanel.Children.Add(frameworkElement);
		}
		canvas.Children.Add(stackPanel);
		grid.Children.Add(canvas);
		System.Windows.Shapes.Path element2 = new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse("M0,66 Q0,52 17,52 H73 Q85,52 94,62 L108,73 H207 Q224,73 224,90 V188 Q224,206 206,206 H18 Q0,206 0,188 Z"),
			Fill = (flag ? MacGradient("#BCD6FF", "#6A9CEF") : MacGradient(FluentTheme.Dark ? "#393B44" : "#FFFFFF", FluentTheme.Dark ? "#24262D" : "#EFF0F4")),
			Stroke = (flag ? UI.Brush("#A9CBFF") : UI.Brush(FluentTheme.Dark ? "#41434B" : "#F6F6F8")),
			StrokeThickness = 0.7
		};
		grid.Children.Add(element2);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(22.0, 109.0, 16.0, 0.0),
			IsHitTestVisible = false
		};
		Brush color = (flag ? Brushes.White : UI.Ink);
		TextBlock textBlock = UI.Text(group, 18.0, color);
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel2.Children.Add(textBlock);
		TextBlock textBlock2 = UI.Text(list.Count + " 个应用", 13.0, flag ? UI.Brush("#F4F8FF") : UI.Muted);
		textBlock2.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock2);
		int num = list.Count((Entry a) => a.Favorite);
		TextBlock textBlock3 = UI.Text((list.Count == 0) ? "等待添加" : ((num > 0) ? (num + " 个收藏") : "应用与快捷入口"), 11.0, flag ? UI.Brush("#EEF5FF") : UI.Muted);
		textBlock3.Margin = new Thickness(0.0, 17.0, 0.0, 0.0);
		stackPanel2.Children.Add(textBlock3);
		grid.Children.Add(stackPanel2);
		return new Viewbox
		{
			Child = grid,
			Stretch = Stretch.Uniform,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
		};
	}

	private System.Windows.Controls.Button FolderCardMac(string group)
	{
		int num = p.State.Apps.Count((Entry a) => p.State.IsVisible(a) && p.State.LibraryGroup(a) == group);
		Viewbox content = FolderArtMac(group);
		System.Windows.Controls.Button button = UI.Button("", delegate
		{
			HandleCommandGroupClick(group);
		});
		button.Content = content;
		button.Padding = new Thickness(0.0);
		button.BorderThickness = new Thickness(0.0);
		button.Background = Brushes.Transparent;
		button.Margin = new Thickness(9.0, 9.0, 9.0, 17.0);
		button.Tag = group;
		button.Height = 228.0;
		button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
		button.ToolTip = group + " · " + num + " 个应用 · 单击打开";
		button.Effect = new DropShadowEffect
		{
			Color = Colors.Black,
			BlurRadius = 12.0,
			ShadowDepth = 3.0,
			Opacity = (FluentTheme.Dark ? 0.12 : 0.035)
		};
		AutomationProperties.SetName(button, "分组 " + group + "，" + num + " 个应用");
		AutomationProperties.SetHelpText(button, "单击打开分组");
		TranslateTransform lift = new TranslateTransform();
		button.RenderTransform = lift;
		button.MouseEnter += delegate
		{
			if (FluentTheme.Motion)
			{
				lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-3.0, TimeSpan.FromMilliseconds(170.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
		};
		button.MouseLeave += delegate
		{
			if (FluentTheme.Motion)
			{
				lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(170.0)));
			}
		};
		return button;
	}

	private void OpenGroupMac(string group)
	{
		selectedGroupMac = group;
		NavigateMac(group);
	}

	private void RenderInspectorMac()
	{
		if (rightPanel == null)
		{
			return;
		}
		UpdateCommandBar();
		if (breadcrumbMac != null)
		{
			workspaceHome.Content = p.State.ShowWorkspace ? "工作空间" : "所有应用";
			breadcrumbMac.Text = ((CurrentAppFolder != null) ? (CurrentAppFolder.Group + " / " + CurrentAppFolder.Name) : (OverviewMac ? "分组与应用" : ((selected == "全部") ? "所有应用" : ((selected == "@favorites") ? "收藏" : ((selected == "@recent") ? "最近使用" : ((selected == "@overview") ? "搜索" : selected))))));
		}
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = UI.Text("信息", 18.0, UI.Ink);
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 24.0);
		stackPanel.Children.Add(textBlock);
		AppFolder appFolder = ((commandGroups.Count == 1) ? p.State.FindAppFolder(commandGroups.First()) : ((selectedEntryMac == null && commandEntries.Count == 0) ? CurrentAppFolder : null));
		string group = ((appFolder != null) ? appFolder.Group : ((selectedEntryMac != null) ? p.State.LibraryGroup(selectedEntryMac) : ((commandGroups.Count == 1) ? commandGroups.First() : ((selected.StartsWith("@") || selected == "全部") ? null : selected))));
		List<Entry> list = ((appFolder != null) ? (from a in p.State.FolderMembers(appFolder)
			where p.State.IsVisible(a) && MatchesSearch(a, search.Text)
			select a).ToList() : ((commandEntries.Count > 0) ? commandEntries.ToList() : p.State.Apps.Where((Entry a) => p.State.IsVisible(a) && (group == null || p.State.LibraryGroup(a) == group) && (commandGroups.Count < 2 || commandGroups.Contains(p.State.LibraryGroup(a))) && MatchesSearch(a, search.Text) && (selected != "@favorites" || a.Favorite) && (selected != "@recent" || a.LastLaunchTicks > 0)).ToList()));
		TextBlock textBlock2 = UI.Text((appFolder != null) ? appFolder.Name : ((selectedEntryMac != null) ? UI.EntryName(selectedEntryMac) : ((commandEntries.Count > 1) ? ("已选 " + commandEntries.Count + " 个应用") : ((commandGroups.Count > 1) ? ("已选 " + commandGroups.Count + " 个分组") : (group ?? ((selected == "@favorites") ? "我的收藏" : ((selected == "@recent") ? "最近使用" : (OverviewMac ? "分组与应用" : "全部应用"))))))), 16.0, UI.Ink);
		textBlock2.FontWeight = FontWeights.SemiBold;
		textBlock2.TextWrapping = TextWrapping.Wrap;
		stackPanel.Children.Add(textBlock2);
		TextBlock textBlock3 = UI.Text((appFolder != null) ? "应用文件夹 · 虚拟集合" : ((selectedEntryMac != null) ? "应用快捷入口" : ((group != null) ? "应用分组" : "工作空间概览")), 12.0, UI.Muted);
		textBlock3.Margin = new Thickness(0.0, 6.0, 0.0, 24.0);
		stackPanel.Children.Add(textBlock3);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		StackPanel element = MacMetric(list.Count, "快捷入口");
		grid.Children.Add(element);
		StackPanel element2 = MacMetric(list.Count((Entry a) => a.Favorite), "已收藏");
		Grid.SetColumn(element2, 1);
		grid.Children.Add(element2);
		Border element3 = new Border
		{
			Child = grid,
			Background = FluentTheme.Background,
			BorderBrush = FluentTheme.Line,
			BorderThickness = new Thickness(0.5),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(14.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 28.0)
		};
		stackPanel.Children.Add(element3);
		TextBlock textBlock4 = UI.Text("属性", 14.0, UI.Ink);
		textBlock4.FontWeight = FontWeights.SemiBold;
		textBlock4.Margin = new Thickness(0.0, 0.0, 0.0, 16.0);
		stackPanel.Children.Add(textBlock4);
		object value;
		if (appFolder == null)
		{
			Entry entry = selectedEntryMac;
			if (entry == null || !entry.Website)
			{
				Entry entry2 = selectedEntryMac;
				value = ((entry2 != null && entry2.FavoriteFolder) ? "文件夹" : ((selectedEntryMac != null) ? "应用" : ((group != null) ? "分组" : "应用集合")));
			}
			else
			{
				value = "网站";
			}
		}
		else
		{
			value = "应用文件夹";
		}
		PropertyMac(stackPanel, "类型", (string)value);
		PropertyMac(stackPanel, "所属分组", group ?? "全部");
		if (appFolder != null)
		{
			PropertyMac(stackPanel, "工作空间", appFolder.InWorkspace ? "已添加" : "未添加");
		}
		PropertyMac(stackPanel, "显示方式", p.State.LibraryListView ? "列表" : "网格");
		if (selectedEntryMac != null)
		{
			PropertyMac(stackPanel, "打开次数", selectedEntryMac.LaunchCount + " 次");
			PropertyMac(stackPanel, "置顶方式", selectedEntryMac.TopPinned ? "手动置顶" : "未置顶");
		}
		long num = selectedEntryMac?.LastLaunchTicks ?? list.Select((Entry a) => a.LastLaunchTicks).DefaultIfEmpty(0L).Max();
		PropertyMac(stackPanel, "最近使用", (num > 0) ? new DateTime(num, DateTimeKind.Utc).ToLocalTime().ToString("MM/dd HH:mm") : "尚未使用");
		Border element4 = new Border
		{
			Height = 1.0,
			Background = FluentTheme.Line,
			Margin = new Thickness(0.0, 14.0, 0.0, 22.0)
		};
		stackPanel.Children.Add(element4);
		TextBlock textBlock5 = UI.Text("编辑、添加与整理操作已统一放在顶部操作栏。", 12.0, UI.Muted);
		textBlock5.TextWrapping = TextWrapping.Wrap;
		stackPanel.Children.Add(textBlock5);
		rightPanel.Content = stackPanel;
	}

	private StackPanel MacMetric(int value, string label)
	{
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = UI.Text(value.ToString(), 24.0, UI.Ink);
		textBlock.FontWeight = FontWeights.SemiBold;
		stackPanel.Children.Add(textBlock);
		TextBlock textBlock2 = UI.Text(label, 11.0, UI.Muted);
		textBlock2.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
		stackPanel.Children.Add(textBlock2);
		return stackPanel;
	}

	private void PropertyMac(StackPanel parent, string label, string value)
	{
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 13.0)
		};
		TextBlock textBlock = UI.Text(value, 12.0, UI.Ink);
		textBlock.MaxWidth = 120.0;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		DockPanel.SetDock(textBlock, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(textBlock);
		dockPanel.Children.Add(UI.Text(label, 12.0, UI.Muted));
		parent.Children.Add(dockPanel);
	}
}
