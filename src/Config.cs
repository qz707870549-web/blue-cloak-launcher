using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Luma;

public class Config
{
	public List<AppFolder> AppFolders = new List<AppFolder>();

	public string ThemeMode = "浅色";

	public bool ReduceMotion;

	public string AccentHex = "#7b5bd1";

	public bool LibraryListView;

	public List<string> CollapsedGroups = new List<string>();

	public List<string> ExcludedIds = new List<string>();

	public List<GroupRule> GroupRules = new List<GroupRule>();

	public double LibraryOpacity = 0.3;

	public double LibraryWidth = 1440.0;

	public double LibraryHeight = 920.0;

	public bool ReplaceStartMenu;
	public bool WinKeyOpensLibrary = true;
	public int StartIntegrationVersion;
	public bool? StartWithWindows;
	public bool ShowWorkspace = true;

	public List<Entry> Apps = new List<Entry>();

	public List<Entry> RemovedEntries = new List<Entry>();

	public List<string> Groups = new List<string> { "未分类" };

	public List<string> HiddenGroups = new List<string>();

	public string DockPosition = "底部";

	public double DockX = -1.0;

	public double DockY = -1.0;

	public double DockCornerRadius = 24.0;

	public double DockIconSize = 64.0;

	public double SurfaceOpacity = 0.62;

	public string SurfaceMaterial = "纯色";

	// Legacy JSON fields retained for compatibility; the desktop widget is removed.
	public bool ShowWidget;

	public bool WidgetLocked;

	public bool WidgetTopmost;

	public double WidgetX = -1.0;

	public double WidgetY = -1.0;

	public bool ShowUtilities;

	public bool ShowSystemApps;

	public List<string> Folders = new List<string>();

	public AppFolder FindAppFolder(string scope)
	{
		if (scope == null || !scope.StartsWith("@folder:"))
		{
			return null;
		}
		return AppFolders.FirstOrDefault((AppFolder f) => f.Id == scope.Substring(8));
	}

	public IEnumerable<Entry> FolderMembers(AppFolder folder)
	{
		return Apps.Where((Entry a) => a.FolderId == folder.Id);
	}

	private void NormalizeAppFolders()
	{
		AppFolders = (from f in AppFolders ?? new List<AppFolder>()
			where f != null && !string.IsNullOrWhiteSpace(f.Id)
			group f by f.Id into g
			select g.First()).ToList();
		foreach (AppFolder appFolder2 in AppFolders)
		{
			if (string.IsNullOrWhiteSpace(appFolder2.Name))
			{
				appFolder2.Name = "未命名文件夹";
			}
			if (!Groups.Contains(appFolder2.Group))
			{
				appFolder2.Group = "未分类";
			}
		}
		foreach (Entry entry in Apps.Where((Entry a) => !string.IsNullOrEmpty(a.FolderId)))
		{
			AppFolder appFolder = AppFolders.FirstOrDefault((AppFolder f) => f.Id == entry.FolderId);
			if (appFolder == null)
			{
				entry.FolderId = null;
			}
			else
			{
				entry.Group = appFolder.Group;
			}
		}
	}

	public AppFolder CombineEntries(Entry moving, Entry target)
	{
		if (moving == null || target == null || moving == target || !Apps.Contains(moving) || !Apps.Contains(target) || LibraryGroup(moving) != LibraryGroup(target) || !IsVisible(moving) || !IsVisible(target))
		{
			return null;
		}
		if (!string.IsNullOrEmpty(target.FolderId))
		{
			AppFolder appFolder = AppFolders.FirstOrDefault((AppFolder f) => f.Id == target.FolderId);
			if (!PutInFolder(moving, appFolder))
			{
				return null;
			}
			return appFolder;
		}
		string name = "新建文件夹";
		int num = 2;
		while (AppFolders.Any((AppFolder f) => f.Group == LibraryGroup(target) && f.Name == name))
		{
			name = "新建文件夹 " + num++;
		}
		AppFolder appFolder2 = new AppFolder
		{
			Name = name,
			Group = LibraryGroup(target)
		};
		AppFolders.Add(appFolder2);
		moving.FolderId = appFolder2.Id;
		target.FolderId = appFolder2.Id;
		moving.Group = (target.Group = appFolder2.Group);
		return appFolder2;
	}

	public bool PutInFolder(Entry entry, AppFolder folder)
	{
		if (entry == null || folder == null || !Apps.Contains(entry) || !AppFolders.Contains(folder) || HiddenGroups.Contains(folder.Group) || entry.FolderId == folder.Id)
		{
			return false;
		}
		entry.Group = folder.Group;
		entry.FolderId = folder.Id;
		entry.SortOrder = int.MaxValue;
		return true;
	}

	public bool MoveAppFolder(AppFolder folder, string group)
	{
		if (folder == null || !AppFolders.Contains(folder) || !Groups.Contains(group) || HiddenGroups.Contains(group) || folder.Group == group)
		{
			return false;
		}
		folder.Group = group;
		foreach (Entry item in FolderMembers(folder))
		{
			item.Group = group;
		}
		return true;
	}

	public Action<Config> RemoveAppFolders(IEnumerable<AppFolder> targets)
	{
		List<AppFolder> folders = targets.Where(AppFolders.Contains).Distinct().ToList();
		if (folders.Count == 0)
		{
			return null;
		}
		Dictionary<Entry, string> members = Apps.Where((Entry a) => folders.Any((AppFolder f) => f.Id == a.FolderId)).ToDictionary((Entry a) => a, (Entry a) => a.FolderId);
		foreach (Entry key in members.Keys)
		{
			key.FolderId = null;
		}
		foreach (AppFolder item in folders)
		{
			AppFolders.Remove(item);
		}
		return delegate(Config state)
		{
			foreach (AppFolder folder in folders)
			{
				if (!state.AppFolders.Any((AppFolder f) => f.Id == folder.Id))
				{
					state.AppFolders.Add(folder);
				}
			}
			foreach (KeyValuePair<Entry, string> pair in members)
			{
				if (state.Apps.Contains(pair.Key))
				{
					pair.Key.FolderId = pair.Value;
					pair.Key.Group = folders.First((AppFolder f) => f.Id == pair.Value).Group;
				}
			}
		};
	}

	public void Normalize()
	{
		Apps = (Apps ?? new List<Entry>()).Where((Entry a) => a != null).ToList();
		RemovedEntries = (RemovedEntries ?? new List<Entry>()).Where((Entry a) => a != null).ToList();
		GroupRules = (GroupRules ?? new List<GroupRule>()).Where((GroupRule r) => r != null).ToList();
		foreach (Entry app in Apps)
		{
			if (string.IsNullOrWhiteSpace(app.Name))
			{
				app.Name = app.CustomName ?? "未命名应用";
			}
			if (string.IsNullOrEmpty(app.Id))
			{
				app.Id = app.Target ?? Guid.NewGuid().ToString("N");
			}
			if (app.Target == null)
			{
				app.Target = "";
			}
			if (app.LastLaunchTicks < 0 || app.LastLaunchTicks > DateTime.MaxValue.Ticks)
			{
				app.LastLaunchTicks = 0L;
			}
		}
		Size size = WindowSizePolicy.Normalize(LibraryWidth, LibraryHeight);
		LibraryWidth = size.Width;
		LibraryHeight = size.Height;
		foreach (Entry app2 in Apps)
		{
			app2.LaunchCount = Math.Max(0, app2.LaunchCount);
		}
		DockIconSize = FiniteRange(DockIconSize, 40.0, 96.0, 64.0);
		DockCornerRadius = FiniteRange(DockCornerRadius, 0.0, 60.0, 24.0);
		SurfaceOpacity = FiniteRange(SurfaceOpacity, 0.08, 1.0, 0.62);
		DockX = FiniteRange(DockX, -100000.0, 100000.0, -1.0);
		DockY = FiniteRange(DockY, -100000.0, 100000.0, -1.0);
		WidgetX = FiniteRange(WidgetX, -100000.0, 100000.0, -1.0);
		ShowWidget = false;
		WidgetY = FiniteRange(WidgetY, -100000.0, 100000.0, -1.0);
		if (CollapsedGroups == null)
		{
			CollapsedGroups = new List<string>();
		}
		if (ExcludedIds == null)
		{
			ExcludedIds = new List<string>();
		}
		if (ThemeMode != "系统" && ThemeMode != "浅色" && ThemeMode != "深色")
		{
			ThemeMode = "系统";
		}
		if (GroupRules == null)
		{
			GroupRules = new List<GroupRule>();
		}
		if (Groups == null)
		{
			Groups = new List<string>();
		}
		if (Groups.Contains("其他"))
		{
			foreach (Entry item in Apps ?? new List<Entry>())
			{
				if (item.Group == "其他")
				{
					item.Group = "未分类";
				}
			}
			Groups[Groups.IndexOf("其他")] = "未分类";
		}
		Groups = Groups.Where((string g) => !string.IsNullOrWhiteSpace(g) && g != "全部").Distinct().ToList();
		if (!Groups.Contains("未分类"))
		{
			Groups.Add("未分类");
		}
		if (HiddenGroups == null)
		{
			HiddenGroups = new List<string>();
		}
		HiddenGroups = HiddenGroups.Where(Groups.Contains).Distinct().ToList();
		if (Apps == null)
		{
			Apps = new List<Entry>();
		}
		foreach (Entry app3 in Apps)
		{
			if (string.IsNullOrEmpty(app3.Group) || !Groups.Contains(app3.Group))
			{
				app3.Group = "未分类";
			}
		}
		if (Folders == null)
		{
			Folders = new List<string>();
		}
		if (Apps.Any((Entry a) => a.Group == "未分类" && a.SystemApp) && !Groups.Contains("系统附带应用"))
		{
			Groups.Add("系统附带应用");
		}
		if (Apps.Any((Entry a) => a.Group == "未分类" && a.Utility && !a.SystemApp) && !Groups.Contains("软件配套工具"))
		{
			Groups.Add("软件配套工具");
		}
		NormalizeAppFolders();
	}

	private static double FiniteRange(double value, double min, double max, double fallback)
	{
		if (!double.IsNaN(value) && !double.IsInfinity(value))
		{
			return Math.Max(min, Math.Min(max, value));
		}
		return fallback;
	}

	public bool MoveGroup(string moving, string before)
	{
		if (string.IsNullOrEmpty(moving) || moving == before || !Groups.Contains(moving) || !Groups.Contains(before))
		{
			return false;
		}
		Groups.Remove(moving);
		Groups.Insert(Groups.IndexOf(before), moving);
		return true;
	}

	public bool MoveEntryToGroup(Entry entry, string group)
	{
		if (entry == null || !Apps.Contains(entry) || !Groups.Contains(group) || HiddenGroups.Contains(group) || (LibraryGroup(entry) == group && string.IsNullOrEmpty(entry.FolderId)))
		{
			return false;
		}
		entry.Group = group;
		entry.FolderId = null;
		entry.SortOrder = int.MaxValue;
		return true;
	}

	public IEnumerable<Entry> OrderedEntries(IEnumerable<Entry> entries)
	{
		return (from app in entries
			orderby app.TopPinned descending, app.SortOrder
			select app).ThenBy((Entry app) => app.Name, StringComparer.CurrentCultureIgnoreCase);
	}

	public bool SaveCategoryOrder(IList<Entry> reordered)
	{
		if (reordered == null || reordered.Count < 2 || reordered.Any((Entry app) => app == null || !Apps.Contains(app)) || reordered.Distinct().Count() != reordered.Count)
		{
			return false;
		}
		string group = LibraryGroup(reordered[0]);
		bool pinned = reordered[0].TopPinned;
		if (reordered.Any((Entry app) => LibraryGroup(app) != group || app.TopPinned != pinned))
		{
			return false;
		}
		List<Entry> list = OrderedEntries(Apps.Where((Entry app) => LibraryGroup(app) == group && app.TopPinned == pinned)).ToList();
		HashSet<Entry> hashSet = new HashSet<Entry>(reordered);
		int num = 0;
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			if (hashSet.Contains(list[num2]))
			{
				list[num2] = reordered[num++];
			}
		}
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			list[num3].SortOrder = num3;
		}
		return true;
	}

	public string LibraryGroup(Entry app)
	{
		if (!(app.Group == "未分类") || !app.SystemApp)
		{
			if (!(app.Group == "未分类") || !app.Utility)
			{
				return app.Group;
			}
			return "软件配套工具";
		}
		return "系统附带应用";
	}

	public bool IsVisible(Entry app)
	{
		if (!HiddenGroups.Contains(LibraryGroup(app)) && (!app.SystemApp || ShowSystemApps))
		{
			if (app.Utility)
			{
				return ShowUtilities;
			}
			return true;
		}
		return false;
	}

	public IEnumerable<string> VisibleGroups()
	{
		return Groups.Where((string group) => !HiddenGroups.Contains(group) && (group != "系统附带应用" || ShowSystemApps) && (group != "软件配套工具" || ShowUtilities));
	}

	public void MergeImported(List<Entry> found)
	{
		foreach (Entry app in found)
		{
			if (!ExcludedIds.Contains(app.Id) && !Apps.Any((Entry saved) => string.Equals(saved.Id, app.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(saved.Target, app.Target, StringComparison.OrdinalIgnoreCase)))
			{
				app.Group = GroupTemplate.Match(app, GroupRules) ?? "未分类";
				Apps.Add(app);
			}
		}
		Normalize();
	}

	public void MergeScan(List<Entry> found)
	{
		found = found.Where((Entry entry2) => !ExcludedIds.Contains(entry2.Id) && !Apps.Any((Entry saved) => saved.Manual && string.Equals(saved.Target, entry2.Target, StringComparison.OrdinalIgnoreCase))).ToList();
		foreach (Entry app in found)
		{
			List<Entry> apps = Apps;
			Func<Entry, bool> predicate = (Entry a) => !a.FavoriteFolder && !a.Website && !a.Manual && string.Equals(a.Id, app.Id, StringComparison.OrdinalIgnoreCase);
			Entry entry = apps.FirstOrDefault(predicate) ?? Apps.FirstOrDefault((Entry a) => !a.FavoriteFolder && string.Equals(a.Target, app.Target, StringComparison.OrdinalIgnoreCase));
			if (entry != null)
			{
				app.Group = entry.Group;
				app.CustomName = entry.CustomName;
				app.CustomTarget = entry.CustomTarget;
				if (!string.IsNullOrEmpty(app.CustomTarget))
				{
					app.Target = app.CustomTarget;
				}
				app.Tags = entry.Tags;
				app.Favorite = entry.Favorite;
				app.LastLaunchTicks = entry.LastLaunchTicks;
				app.LaunchCount = entry.LaunchCount;
				app.FolderId = entry.FolderId;
				app.Pinned = entry.Pinned;
				app.TopPinned = entry.TopPinned;
				app.CustomIcon = entry.CustomIcon;
				app.SortOrder = entry.SortOrder;
			}
			else
			{
				app.Group = GroupTemplate.Match(app, GroupRules) ?? "未分类";
			}
		}
		found.AddRange(Apps.Where((Entry a) => a.FavoriteFolder || a.Website || a.Manual));
		Apps = found.OrderBy((Entry a) => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		Normalize();
	}
}
