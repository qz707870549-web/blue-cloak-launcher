using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Luma;

public partial class Program : System.Windows.Application
{
	public static string DataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

	public static string ConfigPath = Path.Combine(DataDir, "settings.json");

	public Config State = new Config();

	

	public StartIntegration StartIntegration;
	public NativeStartGuard StartGuard;

	public bool Review;



	public Library Library;

	private NotifyIcon tray;

	private Mutex mutex;

	private EventWaitHandle reopen;

	private RegisteredWaitHandle reopenWait;

	private bool ownsInstance;

	private bool protectConfig;

	private bool snapshotSaved;

	private bool scanning;

	public static bool QaMode;

	private bool startSession;

	private string lastOpenedEntry;

	private long lastOpenedAt;

	[STAThread]
	public static void Main(string[] args)
	{
		QaMode = args.Contains("--qa") || args.Contains("--smoke-test");
		string environmentVariable = Environment.GetEnvironmentVariable("LUMA_DATA_DIR");
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			DataDir = Path.GetFullPath(environmentVariable);
		}
		DataDir = ConfigStore.DataDirectory(AppDomain.CurrentDomain.BaseDirectory, DataDir);
        ConfigPath = Path.Combine(DataDir, "settings.json");
		CrashLog.Install();
		CrashLog.Write("launch");
		try
		{
			MainCore(args);
		}
		catch (Exception ex)
		{
			Environment.ExitCode = 1;
			CrashLog.Write("startup-failed", ex);
			if (!args.Contains("--background") && !args.Contains("--uninstall-cleanup")) System.Windows.MessageBox.Show("蓝斗篷启动未能启动。原配置未覆盖。\n请查看 " + Path.Combine(DataDir, "startup.log") + "\n" + ex.Message, "蓝斗篷启动 · 启动诊断");
		}
	}

	private static void MainCore(string[] args)
	{
		if (args.Contains("--uninstall-cleanup"))
		{
			StartupRegistration.RemoveForExecutable(Process.GetCurrentProcess().MainModule.FileName);
			return;
		}
		DataDir = ConfigStore.DataDirectory(AppDomain.CurrentDomain.BaseDirectory, DataDir);
		ConfigPath = Path.Combine(DataDir, "settings.json");
		ConfigStore.InitializePersonalData(AppDomain.CurrentDomain.BaseDirectory, ConfigPath);
		if (args.Contains("--export-groups"))
		{
			Config config = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(ConfigPath));
			File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "group-template.json"), new JavaScriptSerializer().Serialize(GroupTemplate.Export(config)));
			return;
		}
		if (args.Contains("--start-probe"))
		{
			Directory.CreateDirectory(DataDir);
			File.WriteAllText(Path.Combine(DataDir, "start-buttons.json"), new JavaScriptSerializer().Serialize(StartIntegration.Discover()));
			return;
		}
		if (args.Contains("--scan-test"))
		{
			List<Entry> obj = Scanner.Scan(new List<string>());
			Directory.CreateDirectory(DataDir);
			File.WriteAllText(Path.Combine(DataDir, "scan-report.json"), new JavaScriptSerializer().Serialize(obj));
			return;
		}
		Program program = new Program();
		program.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs error)
		{
			CrashLog.Write("ui-operation", error.Exception);
			error.Handled = true;
			AirStyle.Notice("操作未完成，已记录到启动日志。\n" + error.Exception.Message, "操作提示");
		};
		program.Run();
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		string text = (e.Args.Contains("--smoke-test") ? ("LumaDock.AIRI.Smoke." + Process.GetCurrentProcess().Id) : (QaMode ? ("LumaDock.AIRI.QA." + Process.GetCurrentProcess().Id) : "LumaDock.Desktop"));
		mutex = new Mutex(initiallyOwned: true, text + ".Singleton", out var createdNew);
		ownsInstance = createdNew;
		if (!createdNew)
		{
			try
			{
				using EventWaitHandle eventWaitHandle = EventWaitHandle.OpenExisting((QaMode ? text + ".Open" : "LumaDock.OpenLibrary"));
				if (!e.Args.Contains("--background")) eventWaitHandle.Set();
			}
			catch
			{
			}
			CrashLog.Write("existing-instance-signaled");
			Shutdown();
			return;
		}
		reopen = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, (QaMode ? text + ".Open" : "LumaDock.OpenLibrary"));
		reopenWait = ThreadPool.RegisterWaitForSingleObject(reopen, delegate
		{
			base.Dispatcher.BeginInvoke(new Action(OpenLibrary));
		}, null, -1, executeOnlyOnce: false);
		base.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		Directory.CreateDirectory(DataDir);
		State = ConfigStore.Load(ConfigPath, out var recovered, out protectConfig);
		if (recovered)
		{
			CrashLog.Write("configuration-recovered-from-backup");
		}
		if (protectConfig)
		{
			CrashLog.Write("configuration-protected-read-only");
		}
		int num = Array.IndexOf(e.Args, "--restore-config");
		if (num >= 0)
		{
			if (num + 1 >= e.Args.Length)
			{
				throw new InvalidDataException("缺少恢复配置文件路径。");
			}
			Config state = ConfigStore.Read(e.Args[num + 1]);
			ConfigStore.Snapshot(ConfigPath);
			State = state;
			protectConfig = false;
			snapshotSaved = true;
		}
		if (!File.Exists(ConfigPath) && !recovered && !protectConfig && num < 0)
		{
			State = BuiltInGroups.Initialize(ConfigPath, DataDir);
		}
		if (!File.Exists(ConfigPath) && !recovered && !protectConfig && num < 0)
		{
			GroupTemplate.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "group-template.json"))?.Apply(State);
		}
		State.Normalize();
		State.RemoveLegacyDefaultGroups();
		State.EnsureUngroupedCategory();
		SystemShortcuts.Ensure(State);
		CoreSystemApps.Reveal(State);
		if (State.StartIntegrationVersion < 2)
		{
			State.ReplaceStartMenu = true;
			State.WinKeyOpensLibrary = true;
			State.StartIntegrationVersion = 2;
		}
		if (QaMode)
		{
			State.ReplaceStartMenu = false;
			State.ShowWidget = false;
		}
		CrashLog.Write("config-ready");
		if (!QaMode)
		{
			State.StartWithWindows = State.StartWithWindows ?? StartupRegistration.LegacyEnabled();
			if (State.StartWithWindows == true) Task.Run(() =>
			{
				try { CrashLog.Write(StartupRegistration.Apply(true)); }
				catch (Exception error) { CrashLog.Write("startup-registration-repair", error); }
			});
		}
		FluentTheme.Apply(State.ThemeMode);
		CrashLog.Write("theme-ready");
		Review = e.Args.Contains("--review") || QaMode;
		if (!QaMode)
		{
			try
			{
				StartIntegration = new StartIntegration(this);
				StartGuard = new NativeStartGuard(this);
			}
			catch (Exception exception2)
			{
				State.ReplaceStartMenu = false;
				CrashLog.Write("start-integration-disabled", exception2);
			}
		}
		tray = new NotifyIcon();
		tray.Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName) ?? SystemIcons.Application;
		tray.Text = "蓝斗篷启动 · 双击打开应用分组";
		tray.Visible = true;
		tray.DoubleClick += delegate
		{
			OpenLibrary();
		};
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add("全部应用 / 设置", null, delegate
		{
			base.Dispatcher.Invoke(OpenLibrary);
		});
		contextMenuStrip.Items.Add("显示 / 隐藏桌面图标", null, delegate
		{
			ToggleDesktop();
		});
		contextMenuStrip.Items.Add("退出蓝斗篷启动", null, delegate
		{
			base.Dispatcher.Invoke(base.Shutdown);
		});
		tray.ContextMenuStrip = contextMenuStrip;
		if (!e.Args.Contains("--background") || (Review && !e.Args.Contains("--smoke-test")))
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(OpenLibrary));
		}
		if (e.Args.Contains("--smoke-test"))
		{
			DispatcherTimer smoke = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(2.0)
			};
			smoke.Tick += delegate
			{
				smoke.Stop();
				CrashLog.Write("smoke-complete");
				Shutdown();
			};
			smoke.Start();
		}
		CrashLog.Write("startup-ready");
		if ((recovered || protectConfig) && !QaMode)
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
			{
				AirStyle.Notice(protectConfig ? "配置文件无法读取，原文件已保留，本次不会覆盖保存。\n可在基础设置中导入有效备份。" : "已从备份恢复配置，应用与分类可以继续使用。", "配置恢复");
			});
		}
		if (!State.Apps.Any(a => !SystemShortcuts.IsKnown(a)) && !QaMode && !protectConfig)
		{
			Scan();
		}
		else if (State.SystemEntriesVersion < 1 && !QaMode && !protectConfig)
		{
			ImportCoreSystemApps();
		}
		StartAutoDiscovery();
		if (!QaMode && State.HideDesktopIcons) DesktopIcons.SetHidden(true);
	}

	private async void ImportCoreSystemApps()
	{
		try
		{
			var entries = await Task.Run(() => Scanner.Scan(new List<string>()).Where(CoreSystemApps.IsCore).ToList());
			State.MergeImported(entries);
			CoreSystemApps.Reveal(State);
			State.SystemEntriesVersion = 1;
			Refresh();
		}
		catch (Exception error) { CrashLog.Write("core-system-app-discovery", error); }
	}

	public async void ImportStartMenu()
	{
		if (scanning)
		{
			return;
		}
		scanning = true;
		try
		{
			List<Entry> found = await Task.Run(() => Scanner.ScanStartMenu());
			State.MergeImported(found);
			Refresh();
		}
		finally
		{
			scanning = false;
		}
	}

	public async void Scan()
	{
		if (scanning)
		{
			return;
		}
		scanning = true;
		try
		{
			List<Entry> found = await Task.Run(() => Scanner.Scan(State.Folders.ToList()));
			State.MergeDiscovered(found);
			CoreSystemApps.Reveal(State);
			State.SystemEntriesVersion = 1;
			if (!State.Apps.Any((Entry x) => x.Pinned))
			{
				string[] array = new string[5] { "Chrome", "Edge", "微信", "QQ音乐", "资源管理器" };
				foreach (string term in array)
				{
					List<Entry> apps = State.Apps;
					Func<Entry, bool> predicate = (Entry x) => x.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
					Entry entry = apps.FirstOrDefault(predicate);
					if (entry != null)
					{
						entry.Pinned = true;
					}
				}
			}
			Refresh();
		}
		finally
		{
			scanning = false;
		}
	}

	public void RemoveEntry(Entry entry)
	{
		if (AirStyle.Confirm("移除快捷入口", "只移除蓝斗篷启动中的入口，不删除原软件或文件。可在基础设置中恢复。", "移除入口"))
		{
			if (!State.RemovedEntries.Any((Entry a) => a.Id == entry.Id))
			{
				State.RemovedEntries.Add(entry);
			}
			if (!State.ExcludedIds.Contains(entry.Id))
			{
				State.ExcludedIds.Add(entry.Id);
			}
			State.Apps.Remove(entry);
			Refresh();
		}
	}

	public void RestoreEntries()
	{
		foreach (Entry entry in State.RemovedEntries)
		{
			if (!State.Apps.Any((Entry a) => a.Id == entry.Id))
			{
				State.Apps.Add(entry);
			}
		}
		State.RemovedEntries.Clear();
		State.ExcludedIds.Clear();
		Refresh();
		Scan();
	}

	public void Save()
	{
		if (protectConfig)
		{
			return;
		}
		try
		{
			if (!snapshotSaved)
			{
				ConfigStore.Snapshot(ConfigPath);
				snapshotSaved = true;
			}
			ConfigStore.Save(ConfigPath, State);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("保存设置失败，原配置已保留：" + ex.Message);
		}
	}

	public void ImportGroups(string path) { ImportGroupBackup(path); }

    public void OpenLibrary()
	{
		OpenLibraryAt(null);
	}

	private void OpenLibraryAt(StartButtonBounds button)
	{
		RequestDiscoveryOnOpen();
		startSession = openingFromStart || (button != null && State.ReplaceStartMenu);
		CrashLog.Write("open-library");
		if (Library == null)
		{
			Library = new Library(this);
			Library.Closed += delegate
			{
				Library = null;
			};
		}
		Library menu = Library;
		menu.Topmost = true;
		if (menu.IsVisible && menu.WindowState != WindowState.Minimized)
		{
			menu.Render();
			menu.Activate();
			menu.FocusSearch(false);
			return;
		}
		int revision = menu.BeginOpening();
		try
		{
			menu.WindowState = startSession ? WindowState.Normal : WindowSizePolicy.RestoreState(menu.WindowState, menu.PreviousWindowState);
			menu.Render();
			new WindowInteropHelper(menu).EnsureHandle();
			if (startSession)
			{
				PositionStartMenu(button);
			}
			if (!startSession)
			{
				Rect workArea = SystemParameters.WorkArea;
				menu.Width = Math.Min(menu.Width, workArea.Width - 32.0);
				menu.Height = Math.Min(menu.Height, workArea.Height - 32.0);
				menu.Left = workArea.Left + (workArea.Width - menu.Width) / 2.0;
				menu.Top = workArea.Top + (workArea.Height - menu.Height) / 2.0;
			}
			menu.Show();
			AirStyle.Enter((FrameworkElement)menu.Content);
			CrashLog.Write("library-visible");
			menu.Activate();
			menu.FocusSearch(false);
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
			{
				if (Library != menu || !menu.IsVisible || !menu.IsOpening(revision))
				{
					return;
				}
				try
				{
					if (startSession && !menu.IsUserResizing)
					{
						PositionStartMenu(button);
					}
					menu.RefreshRestoredLayout();
					if (menu.WindowState != WindowState.Minimized)
					{
						menu.FocusSearch(false);
					}
				}
				finally
				{
					menu.EndOpening(revision);
				}
			});
		}
		catch
		{
			menu.EndOpening(revision);
			throw;
		}
	}

	public void DismissLibrary()
	{
		StartGuard?.Cancel();
		if (Library != null && Library.IsVisible && !Library.IsInteracting)
		{
			CrashLog.Write("dismiss-explicit");
			Library.Hide();
		}
	}

	public void DismissOutsidePointer(int x, int y)
	{
		if (Review || QaMode)
		{
			return;
		}
		Library menu = Library;
		if (menu != null && menu.IsVisible && menu.WindowState != WindowState.Minimized && !menu.IsInteracting && !base.Windows.Cast<Window>().Any((Window w) => w != menu && w.IsActive))
		{
			System.Windows.Point point = menu.PointFromScreen(new System.Windows.Point(x, y));
			if (!new Rect(0.0, 0.0, menu.ActualWidth, menu.ActualHeight).Contains(point))
			{
				DismissLibrary();
			}
		}
	}

	public void SetStartReplacement(bool enabled)
	{
		State.ReplaceStartMenu = enabled;
		if (enabled && StartIntegration == null && !QaMode)
		{
			StartIntegration = new StartIntegration(this);
		}
		if (StartIntegration != null)
		{
			StartIntegration.Enabled = enabled;
		}
		Save();
	}

	public void ToggleStartMenu(StartButtonBounds button)
	{
		if (State.ReplaceStartMenu)
		{
			ToggleStartSurface(button);
		}
	}

	private bool openingFromStart;
	public void OpenStartSurface(StartButtonBounds button)
	{
		openingFromStart = true;
		try { OpenLibraryAt(button); }
		finally { openingFromStart = false; }
	}
	public void ToggleStartSurface(StartButtonBounds button)
	{
		if (Library != null && WindowSizePolicy.ToggleCloses(Library.IsVisible, Library.WindowState)) { DismissLibrary(); return; }
		openingFromStart = true;
		try { OpenLibraryAt(button); }
		finally { openingFromStart = false; }
	}

	public void PositionStartMenu(StartButtonBounds button)
	{
		if (Library != null)
		{
			DesktopPlacement.Place(Library, State, button);
		}
	}

	public void AutoDismissLibrary(Library menu)
	{
		if (Review || QaMode || (StartGuard?.Pending ?? false))
		{
			return;
		}
		base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			if (!(StartGuard?.Pending ?? false) && Library == menu && menu.IsVisible && menu.WindowState != WindowState.Minimized && !menu.IsInteracting && !menu.IsActive && !base.Windows.Cast<Window>().Any((Window w) => w != menu && w.IsActive))
			{
				DismissLibrary();
			}
		});
	}

	public void ToggleLibrary()
	{
		if (Library != null && WindowSizePolicy.ToggleCloses(Library.IsVisible, Library.WindowState))
		{
			DismissLibrary();
		}
		else
		{
			OpenLibrary();
		}
	}

	public void Refresh()
	{
		State.Normalize();
		Save();
		if (discoveryTimer != null) RefreshDiscoveryWatchers();
		
		if (Library != null)
		{
			Library.Render();
		}
	}

	public void AppearanceChanged()
	{
		
		foreach (Window window in base.Windows)
		{
			if (window.Content is Border)
			{
				Glass.Apply(window, State);
			}
		}
		
	}

	public void RecordLaunch(Entry a)
	{
		UsageRanking.Record(a, DateTime.UtcNow);
		Save();
		if (Library != null)
		{
			Library.UpdateRecent();
		}
	}

	public void Launch(Entry a)
	{
		if (a == null)
		{
			return;
		}
		string text = a.Id ?? a.Target ?? a.Name;
		long timestamp = Stopwatch.GetTimestamp();
		if (!(lastOpenedEntry == text) || !((double)(timestamp - lastOpenedAt) / (double)Stopwatch.Frequency < 0.65))
		{
			lastOpenedEntry = text;
			lastOpenedAt = timestamp;
			if (State.ReplaceStartMenu)
			{
				DismissLibrary();
			}
			if (a.FavoriteFolder || a.Website)
			{
				LaunchNew(a);
			}
			else
			{
				WindowController.Open(this, a);
			}
		}
	}

	public void LaunchNew(Entry a)
	{
		try
		{
			if (a.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
			{
				Process.Start("explorer.exe", a.Target);
			}
			else
			{
				Process.Start(new ProcessStartInfo(a.Target)
				{
					UseShellExecute = true
				});
			}
			RecordLaunch(a);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("无法打开 " + a.Name + "\n" + ex.Message + "\n可以右键移除失效项目。", "蓝斗篷启动");
		}
	}

	public void ToggleDesktop()
	{
		IntPtr intPtr = Native.Desktop();
		if (intPtr != IntPtr.Zero)
		{
			Native.ShowWindow(intPtr, (!Native.IsWindowVisible(intPtr)) ? 5 : 0);
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (!ownsInstance)
		{
			base.OnExit(e);
			return;
		}
		StartGuard?.Dispose();
		StopAutoDiscovery();
		if (StartIntegration != null)
		{
			StartIntegration.Dispose();
		}
		if (tray != null)
		{
			tray.Dispose();
		}
		Save();
		if (reopenWait != null)
		{
			reopenWait.Unregister(null);
		}
		if (reopen != null)
		{
			reopen.Dispose();
		}
		if (mutex != null)
		{
			mutex.Dispose();
		}
		base.OnExit(e);
	}
}
