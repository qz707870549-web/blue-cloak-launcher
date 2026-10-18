using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Luma;

public static class DiscoveryRules
{
	[DllImport("msi.dll", CharSet = CharSet.Unicode)]
	private static extern uint MsiGetShortcutTarget(string shortcut, StringBuilder product, StringBuilder feature, StringBuilder component);

	[DllImport("msi.dll", CharSet = CharSet.Unicode)]
	private static extern int MsiGetComponentPath(string product, string component, StringBuilder path, ref uint length);

	public static string ShortcutExecutable(string shortcut, string fallback)
	{
		StringBuilder stringBuilder = new StringBuilder(39);
		StringBuilder feature = new StringBuilder(256);
		StringBuilder stringBuilder2 = new StringBuilder(39);
		if (MsiGetShortcutTarget(shortcut, stringBuilder, feature, stringBuilder2) != 0)
		{
			return fallback;
		}
		StringBuilder stringBuilder3 = new StringBuilder(32768);
		uint length = 32768u;
		if (MsiGetComponentPath(stringBuilder.ToString(), stringBuilder2.ToString(), stringBuilder3, ref length) != 3)
		{
			return fallback;
		}
		return stringBuilder3.ToString();
	}

	public static string Identity(string executable, string arguments)
	{
		return executable.Trim().Trim('"').Replace('/', '\\')
			.ToLowerInvariant() + (string.IsNullOrWhiteSpace(arguments) ? "" : (" | " + arguments.Trim()));
	}

	public static bool IsUtility(Entry app)
	{
		string text = app.Name.ToLowerInvariant();
		string text2 = (app.IconPath ?? "").Replace('/', '\\').ToLowerInvariant();
		string text3 = (app.Id ?? app.Target ?? "").Split(new string[1] { " | " }, StringSplitOptions.None)[0].ToLowerInvariant();
		if (Regex.IsMatch(text2, "\\\\programs\\\\solidworks(?:(?: 工具| tools)(?: 20\\d{2})?| 安装管理程序| installation manager| 20\\d{2})\\\\"))
		{
			return !Regex.IsMatch(text, "^solidworks\\s+20\\d{2}$");
		}
		if (text.StartsWith("solidworks", StringComparison.Ordinal))
		{
			return !Regex.IsMatch(text, "^solidworks\\s+20\\d{2}$");
		}
		if (Regex.IsMatch(text, "^(costing 模板编辑器|toolbox 设置|属性标签编制程序)(\\s+20\\d{2})?$"))
		{
			return true;
		}
		if (text2.Contains("\\wps office 工具\\") || text2.Contains("\\wps office tools\\"))
		{
			return true;
		}
		if (text3.EndsWith("\\qqmusic.exe") && text == "听歌识曲")
		{
			return true;
		}
		if (text3.EndsWith("\\desktopoverlayhost.exe") || (text3.EndsWith("\\bz.exe") && text3.Contains("bandizip")))
		{
			return true;
		}
		if (!new string[2] { "\\programs\\startup\\", "\\programs\\启动\\" }.Any((app.IconPath ?? "").ToLowerInvariant().Contains))
		{
			return new string[6] { "后台下载", "快速启动", "设置向导", "性能测试", "routing library manager", "network monitor" }.Any(text.Contains);
		}
		return true;
	}

	public static bool IsDocumentLaunch(string executable, string arguments)
	{
		if (string.IsNullOrWhiteSpace(arguments))
		{
			return false;
		}
		string value = Path.GetFileNameWithoutExtension(executable).ToLowerInvariant();
		if (new string[6] { "msedge", "chrome", "firefox", "iexplore", "acrord32", "acrobat" }.Contains(value))
		{
			return Regex.IsMatch(arguments, "https?://|file:|\\.(pdf|html?|docx?|xlsx?|pptx?)(\\s|\"|$)", RegexOptions.IgnoreCase);
		}
		return false;
	}

	public static bool IsGameShortcut(string path)
	{
		try
		{
			return File.ReadLines(path).Any((string line) => Regex.IsMatch(line, "^URL=(steam://rungameid/|steam://run/|com\\.epicgames\\.launcher://apps/|battlenet://)", RegexOptions.IgnoreCase));
		}
		catch
		{
			return false;
		}
	}

	public static string GameIdentity(string path)
	{
		try
		{
			return File.ReadLines(path).First((string line) => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)).Substring(4)
				.Trim()
				.ToLowerInvariant();
		}
		catch
		{
			return path.ToLowerInvariant();
		}
	}

	public static bool IsSystemApp(Entry app)
	{
		string text = (app.Id ?? app.Target ?? "").ToLowerInvariant();
		string value = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\').ToLowerInvariant() + "\\";
		if (text.StartsWith(value) && !text.Contains("\\installer\\"))
		{
			return true;
		}
		if (text == "explorer.exe" || text == "ms-settings:")
		{
			return true;
		}
		if (Regex.IsMatch(text, "\\\\windowsapps\\\\(?:microsoft\\.|microsoftwindows\\.|microsoftcorporationii\\.)"))
		{
			return true;
		}
		if (new string[4] { "\\internet explorer\\", "\\windows mail\\", "\\windows media player\\", "\\common files\\microsoft shared\\ink\\" }.Any(text.Contains))
		{
			return true;
		}
		if (!text.StartsWith("shell:appsfolder\\"))
		{
			return false;
		}
		return new string[18]
		{
			"microsoft.windows", "microsoftwindows.", "windows.immersivecontrolpanel", "microsoft.paint_", "microsoft.screensketch_", "microsoft.zunemusic_", "microsoft.bing", "microsoft.yourphone_", "microsoft.get", "microsoft.microsoftsolitairecollection_",
			"microsoft.microsoftstickynotes_", "microsoft.gamingapp_", "microsoft.xbox", "microsoftcorporationii.microsoftfamily_", "microsoftcorporationii.quickassist_", "microsoftcorporationii.windowshdrcalibration_", "microsoft.sechealthui_", "microsoft.desktopappinstaller_"
		}.Any(text.Substring(17).StartsWith);
	}

	public static string PackageLogo(string install, string logo)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(install) || string.IsNullOrWhiteSpace(logo))
			{
				return null;
			}
			string text = Path.Combine(install, logo);
			string directoryName = Path.GetDirectoryName(text);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
			return Directory.GetFiles(directoryName, fileNameWithoutExtension + "*.png").OrderByDescending(delegate(string p)
			{
				if (p.IndexOf("targetsize-64", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return 6;
				}
				if (p.IndexOf("targetsize-96", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return 5;
				}
				if (p.IndexOf("targetsize-256", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return 4;
				}
				if (p.IndexOf("scale-200", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return 3;
				}
				return (p.IndexOf("scale-100", StringComparison.OrdinalIgnoreCase) < 0) ? 1 : 2;
			}).ThenByDescending((string p) => p.Contains("altform-lightunplated") ? 2 : (p.Contains("altform-unplated") ? 1 : 0))
				.FirstOrDefault() ?? (File.Exists(text) ? text : null);
		}
		catch
		{
			return null;
		}
	}

	public static List<Entry> Normalize(IEnumerable<Entry> entries)
	{
		List<Entry> list = new List<Entry>();
		foreach (IGrouping<string, Entry> item in entries.GroupBy((Entry a) => a.Id, StringComparer.OrdinalIgnoreCase))
		{
			Entry entry = (from a in item
				orderby IsUtility(a) ? 1 : 0, (!(a.IconPath ?? "").EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) ? 1 : 0
				select a).First();
			entry.Utility = IsUtility(entry);
			entry.SystemApp = IsSystemApp(entry);
			list.Add(entry);
		}
		return list;
	}
}
