using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Luma;

public static class Scanner
{
	public static List<Entry> ScanStartMenu()
	{
		string[] roots = new string[2]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
			Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
		};
		return (from app in Scan(new List<string>())
			where !string.IsNullOrEmpty(app.IconPath) && roots.Any((string root) => app.IconPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			select app).ToList();
	}

	private static string Category(string n)
	{
		n = n.ToLowerInvariant();
		if (new string[6] { "微信", "wechat", "qq", "豆包", "discord", "teams" }.Any(n.Contains))
		{
			return "社交";
		}
		if (new string[9] { "photoshop", "figma", "solidworks", "design", "adobe", "剪映", "画图", "blender", "字由" }.Any(n.Contains))
		{
			return "创作";
		}
		if (new string[10] { "steam", "game", "游戏", "music", "音乐", "bilibili", "哔哩", "hmcl", "naraka", "永劫" }.Any(n.Contains))
		{
			return "娱乐";
		}
		if (new string[10] { "office", "word", "excel", "wps", "notion", "网盘", "文档", "edge", "chrome", "codex" }.Any(n.Contains))
		{
			return "效率";
		}
		return "工具";
	}

	public static List<Entry> Scan(List<string> extra)
	{
		List<Entry> result = new List<Entry>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Action<string, string, string> action = delegate(string n, string t, string icon)
		{
			if (!string.IsNullOrWhiteSpace(n) && !string.IsNullOrWhiteSpace(t))
			{
				string text6 = n.ToLowerInvariant();
				if (!new string[6] { "uninstall", "卸载", "readme", "help", "帮助", "license" }.Any(text6.Contains))
				{
					string text7 = t.Trim().ToLowerInvariant();
					if (seen.Add(text7))
					{
						result.Add(new Entry
						{
							Id = text7,
							Name = n,
							Target = t,
							IconPath = icon,
							Group = Category(n)
						});
					}
				}
			}
		};
		string[] first = new string[4]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
			Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
			Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
			Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
		};
		dynamic val = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
		foreach (string item in first.Concat(extra))
		{
			foreach (string item2 in Files(item, 0, extra.Contains(item) ? 4 : 12))
			{
				string text = Path.GetExtension(item2).ToLowerInvariant();
				if (text != ".lnk" && text != ".url" && text != ".exe" && text != ".appref-ms")
				{
					continue;
				}
				try
				{
					string arg = item2;
					string text2 = null;
					if (!(text == ".url"))
					{
						goto IL_0127;
					}
					if (!DiscoveryRules.IsGameShortcut(item2))
					{
						continue;
					}
					text2 = DiscoveryRules.GameIdentity(item2);
					goto IL_0127;
					IL_0127:
					if (!(text == ".lnk"))
					{
						goto IL_0328;
					}
					dynamic val2 = val.CreateShortcut(item2);
					string text3 = DiscoveryRules.ShortcutExecutable(item2, (string)val2.TargetPath);
					string arguments = (string)val2.Arguments;
					if (!string.IsNullOrEmpty(text3) && File.Exists(text3))
					{
						string value = Path.GetExtension(text3).ToLowerInvariant();
						if (new string[5] { ".exe", ".com", ".bat", ".cmd", ".appref-ms" }.Contains(value) && !DiscoveryRules.IsDocumentLaunch(text3, arguments))
						{
							text2 = DiscoveryRules.Identity(text3, arguments);
							goto IL_0328;
						}
					}
					goto end_IL_00fb;
					IL_0328:
					int count = result.Count;
					action(Path.GetFileNameWithoutExtension(item2), arg, item2);
					if (result.Count > count && text2 != null)
					{
						result[result.Count - 1].Id = text2;
					}
					end_IL_00fb:;
				}
				catch
				{
				}
			}
		}
		RegistryKey[] array = new RegistryKey[2]
		{
			Registry.CurrentUser,
			Registry.LocalMachine
		};
		foreach (RegistryKey registryKey in array)
		{
			string[] array2 = new string[2] { "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths", "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\App Paths" };
			foreach (string name in array2)
			{
				try
				{
					using RegistryKey registryKey2 = registryKey.OpenSubKey(name);
					if (registryKey2 == null)
					{
						continue;
					}
					string[] subKeyNames = registryKey2.GetSubKeyNames();
					foreach (string name2 in subKeyNames)
					{
						using RegistryKey registryKey3 = registryKey2.OpenSubKey(name2);
						string text4 = Convert.ToString(registryKey3.GetValue(null)).Trim('"');
						if (File.Exists(text4))
						{
							action(Path.GetFileNameWithoutExtension(text4), text4, text4);
						}
					}
				}
				catch
				{
				}
			}
		}
		try
		{
			dynamic val3 = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
			dynamic val4 = val3.NameSpace("shell:AppsFolder");
			foreach (dynamic item3 in val4.Items())
			{
				string arg2 = item3.Name;
				string text5 = item3.Path;
				if (!text5.Contains("!"))
				{
					continue;
				}
				int count2 = result.Count;
				action(arg2, "shell:AppsFolder\\" + text5, null);
				if (result.Count > count2)
				{
					try
					{
						result[result.Count - 1].ImagePath = DiscoveryRules.PackageLogo((string)item3.ExtendedProperty("System.AppUserModel.PackageInstallPath"), (string)item3.ExtendedProperty("System.Tile.SmallLogoPath"));
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		action("文件资源管理器", "explorer.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
		action("Windows 设置", "ms-settings:", null);
		result.AddRange(SystemShortcuts.Entries());
		result.AddRange(CoreSystemApps.Fallbacks(result));
		return ApplicationIdentity.Unique(CoreSystemApps.Deduplicate(DiscoveryRules.Normalize(result)));
	}

	private static IEnumerable<string> Files(string p, int depth, int max)
	{
		if (depth > max || !Directory.Exists(p))
		{
			yield break;
		}
		string[] array = new string[0];
		string[] dirs = new string[0];
		try
		{
			array = Directory.GetFiles(p);
			dirs = Directory.GetDirectories(p);
		}
		catch
		{
		}
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			yield return array2[i];
		}
		array2 = dirs;
		foreach (string text in array2)
		{
			FileAttributes attributes;
			try
			{
				attributes = File.GetAttributes(text);
			}
			catch
			{
				continue;
			}
			if ((attributes & FileAttributes.ReparsePoint) != 0)
			{
				continue;
			}
			foreach (string item in Files(text, depth + 1, max))
			{
				yield return item;
			}
		}
	}
}
