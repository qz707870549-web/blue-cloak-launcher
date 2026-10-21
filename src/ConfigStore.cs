using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Luma;

public static class ConfigStore
{
	public static string DataDirectory(string applicationDirectory, string defaultDirectory)
	{
		if (!File.Exists(Path.Combine(applicationDirectory, "personal-seed.json")))
		{
			return defaultDirectory;
		}
		return Path.Combine(applicationDirectory, "personal-data");
	}

	public static void InitializePersonalData(string applicationDirectory, string configPath)
	{
		string path = Path.Combine(applicationDirectory, "personal-seed.json");
		if (File.Exists(path) && !new string[3]
		{
			configPath,
			configPath + ".bak",
			configPath + ".tmp"
		}.Any(File.Exists))
		{
			Config config = Read(path);
			Save(configPath, config);
		}
	}

	public static Config Read(string path)
	{
		string input = File.ReadAllText(path);
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
		if (!(javaScriptSerializer.DeserializeObject(input) is Dictionary<string, object> dictionary) || !dictionary.ContainsKey("Groups") || !dictionary.ContainsKey("Apps"))
		{
			throw new InvalidDataException("不是有效的 Luma 个人配置。");
		}
		Config config = javaScriptSerializer.Deserialize<Config>(input);
		if (config == null || config.Groups == null || config.Apps == null)
		{
			throw new InvalidDataException("配置缺少分组或应用列表。");
		}
		config.Normalize();
		return config;
	}

	public static Config Load(string path, out bool recovered, out bool protect)
	{
		recovered = false;
		protect = false;
		string[] array = new string[3]
		{
			path,
			path + ".bak",
			path + ".tmp"
		};
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (File.Exists(text))
			{
				try
				{
					Config result = Read(text);
					recovered = text != path;
					return result;
				}
				catch
				{
				}
			}
		}
		protect = array.Any(File.Exists);
		return new Config();
	}

	public static string Snapshot(string path)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		string text = Path.Combine(Path.GetDirectoryName(path), "backups");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, "settings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json");
		File.Copy(path, text2);
		return text2;
	}

	public static void Save(string path, Config config)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + ".tmp";
		File.WriteAllText(text, new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 }.Serialize(config));
		if (File.Exists(path))
		{
			File.Replace(text, path, path + ".bak", ignoreMetadataErrors: true);
		}
		else
		{
			File.Move(text, path);
		}
	}

	public static void RestoreGroups(Config target, Config source)
	{
		source.Normalize();
		target.Groups = source.Groups.ToList();
		target.HiddenGroups = source.HiddenGroups.ToList();
		target.GroupRules = GroupTemplate.Export(source).Rules;
		foreach (Entry app in target.Apps)
		{
			List<Entry> apps = source.Apps;
			Func<Entry, bool> predicate = (Entry item) => string.Equals(item.Id, app.Id, StringComparison.OrdinalIgnoreCase);
			Entry entry = apps.FirstOrDefault(predicate) ?? source.Apps.FirstOrDefault((Entry item) => string.Equals(item.Target, app.Target, StringComparison.OrdinalIgnoreCase));
			if (entry != null)
			{
				app.Group = entry.Group;
				app.TopPinned = entry.TopPinned;
				app.PinnedSortOrder = entry.PinnedSortOrder;
				app.SortOrder = entry.SortOrder;
				if (!string.IsNullOrEmpty(entry.CustomIcon))
				{
					app.CustomIcon = entry.CustomIcon;
				}
			}
			else
			{
				app.Group = GroupTemplate.Match(app, target.GroupRules) ?? "未分类";
			}
		}
		foreach (Entry saved in source.Apps.Where((Entry entry2) => entry2.FavoriteFolder || entry2.Website))
		{
			if (!target.Apps.Any((Entry entry2) => string.Equals(entry2.Id, saved.Id, StringComparison.OrdinalIgnoreCase)))
			{
				target.Apps.Add(saved);
			}
		}
		target.Normalize();
	}
}
