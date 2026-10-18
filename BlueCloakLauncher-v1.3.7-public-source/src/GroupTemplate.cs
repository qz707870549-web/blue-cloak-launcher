using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Luma;

public class GroupTemplate
{
	public int Version = 1;

	public List<string> Groups = new List<string>();

	public List<string> HiddenGroups = new List<string>();

	public List<GroupRule> Rules = new List<GroupRule>();

	public static string NormalizeName(string name)
	{
		return Regex.Replace((name ?? "").Trim(), "\\s+", " ").ToLowerInvariant();
	}

	public static string ExecutableName(Entry app)
	{
		try
		{
			string text = (app.Id ?? app.Target ?? "").Split(new string[1] { " | " }, StringSplitOptions.None)[0];
			if (text.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || text.Contains("://"))
			{
				return "";
			}
			return Path.GetFileName(text).ToLowerInvariant();
		}
		catch
		{
			return "";
		}
	}

	private static string StableIdentity(Entry app)
	{
		string text = app.Id ?? "";
		if (!text.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase) && !text.Contains("://"))
		{
			return "";
		}
		return text.ToLowerInvariant();
	}

	public static GroupTemplate Export(Config config)
	{
		config.Normalize();
		return new GroupTemplate
		{
			Groups = config.Groups.ToList(),
			HiddenGroups = config.HiddenGroups.ToList(),
			Rules = (from app in config.Apps
				where !app.FavoriteFolder && !app.Website && app.Group != "未分类"
				select new GroupRule
				{
					Name = app.Name,
					Executable = ExecutableName(app),
					StableId = StableIdentity(app),
					Group = app.Group
				}).ToList()
		};
	}

	public static GroupTemplate Load(string path)
	{
		try
		{
			GroupTemplate groupTemplate = new JavaScriptSerializer().Deserialize<GroupTemplate>(File.ReadAllText(path));
			if (groupTemplate == null || groupTemplate.Version != 1 || groupTemplate.Groups == null || groupTemplate.Rules == null)
			{
				return null;
			}
			return groupTemplate;
		}
		catch
		{
			return null;
		}
	}

	public void Apply(Config config)
	{
		config.Groups = Groups.Where((string group) => !string.IsNullOrWhiteSpace(group) && group != "全部").Distinct().ToList();
		config.HiddenGroups = (HiddenGroups ?? new List<string>()).Where(config.Groups.Contains).ToList();
		config.GroupRules = Rules.Where((GroupRule rule) => rule != null && config.Groups.Contains(rule.Group)).ToList();
		config.Normalize();
	}

	private static string UniqueGroup(IEnumerable<GroupRule> rules)
	{
		List<string> list = rules.Select((GroupRule rule) => rule.Group).Distinct().Take(2)
			.ToList();
		if (list.Count != 1)
		{
			return null;
		}
		return list[0];
	}

	public static string Match(Entry app, IEnumerable<GroupRule> rules)
	{
		List<GroupRule> source = rules.Where((GroupRule rule) => rule != null && !string.IsNullOrWhiteSpace(rule.Group)).ToList();
		string stable = StableIdentity(app);
		string name = NormalizeName(app.Name);
		string executable = ExecutableName(app);
		if (stable != "")
		{
			string text = UniqueGroup(source.Where((GroupRule rule) => string.Equals(rule.StableId, stable, StringComparison.OrdinalIgnoreCase)));
			if (text != null)
			{
				return text;
			}
		}
		List<GroupRule> list = source.Where((GroupRule rule) => NormalizeName(rule.Name) == name && name != "").ToList();
		if (list.Count > 0)
		{
			return UniqueGroup(list.Where((GroupRule rule) => string.Equals(rule.Executable, executable, StringComparison.OrdinalIgnoreCase))) ?? UniqueGroup(list);
		}
		if (executable == "" || new string[6] { "explorer.exe", "rundll32.exe", "cmd.exe", "powershell.exe", "wscript.exe", "cscript.exe" }.Contains(executable))
		{
			return null;
		}
		return UniqueGroup(source.Where((GroupRule rule) => string.Equals(rule.Executable, executable, StringComparison.OrdinalIgnoreCase)));
	}
}
