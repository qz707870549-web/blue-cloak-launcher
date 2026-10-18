using System;
using System.Collections.Generic;
using System.Linq;

namespace Luma;

public static class LibraryEditing
{
	public static bool CanEditGroup(string group)
	{
		if (!string.IsNullOrWhiteSpace(group) && group != "未分类" && group != "系统附带应用")
		{
			return group != "软件配套工具";
		}
		return false;
	}

	public static bool RenameGroup(Config config, string group, string name)
	{
		name = (name ?? "").Trim();
		if (!CanEditGroup(group) || !config.Groups.Contains(group) || name == "" || name == "全部" || name.StartsWith("@") || config.Groups.Any((string g) => string.Equals(g, name, StringComparison.CurrentCultureIgnoreCase)))
		{
			return false;
		}
		int index = config.Groups.IndexOf(group);
		config.Groups[index] = name;
		foreach (Entry item in config.Apps.Where((Entry a) => a.Group == group))
		{
			item.Group = name;
		}
		foreach (AppFolder item2 in config.AppFolders.Where((AppFolder f) => f.Group == group))
		{
			item2.Group = name;
		}
		foreach (GroupRule item3 in config.GroupRules.Where((GroupRule r) => r.Group == group))
		{
			item3.Group = name;
		}
		for (int num = 0; num < config.HiddenGroups.Count; num++)
		{
			if (config.HiddenGroups[num] == group)
			{
				config.HiddenGroups[num] = name;
			}
		}
		for (int num2 = 0; num2 < config.CollapsedGroups.Count; num2++)
		{
			if (config.CollapsedGroups[num2] == group)
			{
				config.CollapsedGroups[num2] = name;
			}
		}
		return true;
	}

	public static Action<Config> RemoveEntries(Config config, IEnumerable<Entry> targets)
	{
		List<Entry> entries = targets.Where(config.Apps.Contains).Distinct().ToList();
		if (entries.Count == 0)
		{
			return null;
		}
		foreach (Entry entry in entries)
		{
			if (!config.RemovedEntries.Any((Entry a) => a.Id == entry.Id))
			{
				config.RemovedEntries.Add(entry);
			}
			if (!config.ExcludedIds.Contains(entry.Id))
			{
				config.ExcludedIds.Add(entry.Id);
			}
			config.Apps.Remove(entry);
		}
		return delegate(Config state)
		{
			foreach (Entry entry2 in entries)
			{
				if (!state.Apps.Any((Entry a) => a.Id == entry2.Id))
				{
					state.Apps.Add(entry2);
				}
				state.RemovedEntries.RemoveAll((Entry a) => a.Id == entry2.Id);
				state.ExcludedIds.RemoveAll((string id) => id == entry2.Id);
			}
		};
	}

	public static Action<Config> MoveEntries(Config config, IEnumerable<Entry> targets, string group)
	{
		if (!config.Groups.Contains(group) || config.HiddenGroups.Contains(group))
		{
			return null;
		}
		List<Entry> list = (from a in targets.Where(config.Apps.Contains)
			where config.LibraryGroup(a) != @group || !string.IsNullOrEmpty(a.FolderId)
			select a).Distinct().ToList();
		if (list.Count == 0)
		{
			return null;
		}
		Dictionary<string, Tuple<string, int, string>> before = (from a in list
			group a by a.Id into g
			select g.First()).ToDictionary((Entry a) => a.Id, (Entry a) => Tuple.Create(a.Group, a.SortOrder, a.FolderId));
		foreach (Entry item in list)
		{
			config.MoveEntryToGroup(item, group);
		}
		return delegate(Config state)
		{
			foreach (Entry app in state.Apps)
			{
				if (before.TryGetValue(app.Id, out var value) && state.Groups.Contains(value.Item1))
				{
					app.Group = value.Item1;
					app.SortOrder = value.Item2;
					app.FolderId = value.Item3;
				}
			}
		};
	}

	public static Action<Config> RemoveGroups(Config config, IEnumerable<string> targets)
	{
		List<string> names = targets.Where((string g) => CanEditGroup(g) && config.Groups.Contains(g)).Distinct().ToList();
		if (names.Count == 0)
		{
			return null;
		}
		Dictionary<string, int> positions = names.ToDictionary((string g) => g, (string g) => config.Groups.IndexOf(g));
		List<string> hidden = names.Where(config.HiddenGroups.Contains).ToList();
		List<string> collapsed = names.Where(config.CollapsedGroups.Contains).ToList();
		Dictionary<string, string> entries = (from a in config.Apps
			where names.Contains(a.Group)
			group a by a.Id into g
			select g.First()).ToDictionary((Entry a) => a.Id, (Entry a) => a.Group);
		Dictionary<AppFolder, string> folders = config.AppFolders.Where((AppFolder f) => names.Contains(f.Group)).ToDictionary((AppFolder f) => f, (AppFolder f) => f.Group);
		foreach (AppFolder key in folders.Keys)
		{
			key.Group = "未分类";
		}
		Dictionary<GroupRule, string> rules = config.GroupRules.Where((GroupRule r) => names.Contains(r.Group)).ToDictionary((GroupRule r) => r, (GroupRule r) => r.Group);
		foreach (Entry item in config.Apps.Where((Entry a) => names.Contains(a.Group)))
		{
			item.Group = "未分类";
		}
		foreach (GroupRule key2 in rules.Keys)
		{
			key2.Group = "未分类";
		}
		config.Groups.RemoveAll(names.Contains);
		config.HiddenGroups.RemoveAll(names.Contains);
		config.CollapsedGroups.RemoveAll(names.Contains);
		return delegate(Config state)
		{
			foreach (KeyValuePair<string, int> item2 in positions.OrderBy((KeyValuePair<string, int> p) => p.Value))
			{
				if (!state.Groups.Contains(item2.Key))
				{
					state.Groups.Insert(Math.Min(item2.Value, state.Groups.Count), item2.Key);
				}
			}
			foreach (Entry app in state.Apps)
			{
				if (entries.TryGetValue(app.Id, out var value))
				{
					app.Group = value;
				}
			}
			foreach (KeyValuePair<AppFolder, string> item3 in folders)
			{
				if (state.AppFolders.Contains(item3.Key))
				{
					item3.Key.Group = item3.Value;
				}
			}
			foreach (KeyValuePair<GroupRule, string> item4 in rules)
			{
				if (state.GroupRules.Contains(item4.Key))
				{
					item4.Key.Group = item4.Value;
				}
			}
			foreach (string item5 in hidden)
			{
				if (!state.HiddenGroups.Contains(item5))
				{
					state.HiddenGroups.Add(item5);
				}
			}
			foreach (string item6 in collapsed)
			{
				if (!state.CollapsedGroups.Contains(item6))
				{
					state.CollapsedGroups.Add(item6);
				}
			}
		};
	}
}
