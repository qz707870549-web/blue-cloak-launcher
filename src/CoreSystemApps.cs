using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Luma;

public static class CoreSystemApps
{
    static readonly string[] Packages = { "microsoft.windowscalculator_", "microsoft.windowsnotepad_", "microsoft.screensketch_", "microsoft.mspaint_", "microsoft.paint_", "microsoft.gethelp_", "microsoft.windowsstore_", "microsoft.windowsalarms_", "microsoft.yourphone_", "windows.immersivecontrolpanel_" };
    static readonly string[] Executables = { "calc.exe", "notepad.exe", "snippingtool.exe", "mspaint.exe" };
    public static bool IsCore(Entry entry)
    {
        if (entry == null || entry.Website || entry.FavoriteFolder) return false;
        foreach (string value in new[] { entry.Id, entry.Target })
        {
            string text = (value ?? "").ToLowerInvariant();
            if (text == "ms-settings:") return true;
            if (text.StartsWith("shell:appsfolder\\") && Packages.Any(text.Substring(17).StartsWith)) return true;
            if (text.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\').ToLowerInvariant() + "\\") && Executables.Contains(Path.GetFileName(text.Split(new[] { " | " }, StringSplitOptions.None)[0]))) return true;
        }
        return false;
    }
    public static List<Entry> Fallbacks(IEnumerable<Entry> installed)
    {
        var result = new List<Entry>();
        var known = installed.ToList();
        string[] names = { "计算器", "记事本", "截图工具", "画图" };
        string[] packages = { "microsoft.windowscalculator_", "microsoft.windowsnotepad_", "microsoft.screensketch_", "microsoft.mspaint_" };
        for (int i = 0; i < Executables.Length; i++)
        {
            int index = i;
            if (known.Any(a => (a.Target ?? "").IndexOf(packages[index], StringComparison.OrdinalIgnoreCase) >= 0 || (index == 3 && (a.Target ?? "").IndexOf("microsoft.paint_", StringComparison.OrdinalIgnoreCase) >= 0))) continue;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), Executables[i]);
            if (File.Exists(path)) result.Add(new Entry { Id = path.ToLowerInvariant(), Name = names[i], Target = path, IconPath = path, Group = "工具" });
        }
        return result;
    }
    public static string Family(Entry entry)
    {
        if (!IsCore(entry)) return null;
        foreach (string value in new[] { entry.Id, entry.Target })
        {
            string text = (value ?? "").ToLowerInvariant();
            if (text == "ms-settings:" || text.Contains("windows.immersivecontrolpanel_")) return "settings";
            var package = Packages.FirstOrDefault(p => text.Contains(p));
            if (package != null) return package == "microsoft.paint_" ? "microsoft.mspaint_" : package;
            int index = Array.IndexOf(Executables, Path.GetFileName(text.Split(new[] { " | " }, StringSplitOptions.None)[0]));
            if (index >= 0) return Packages[index];
        }
        return null;
    }
    public static bool Same(Entry a, Entry b) => Family(a) != null && Family(a) == Family(b);
    public static List<Entry> Deduplicate(IEnumerable<Entry> entries) => entries
        .GroupBy(a => Family(a) ?? ("entry:" + a.Id), StringComparer.OrdinalIgnoreCase)
        .Select(g => g.OrderByDescending(a => !String.IsNullOrEmpty(a.ImagePath)).ThenByDescending(a => (a.Target ?? "").StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase)).First()).ToList();
    public static void Reveal(Config config)
    {
        foreach (var entry in config.Apps.Where(IsCore))
        {
            if (entry.Group == "未分类" && (entry.SystemApp || entry.Utility))
            {
                if (!config.Groups.Contains("工具")) config.Groups.Add("工具");
                entry.Group = "工具";
            }
            entry.SystemApp = false; entry.Utility = false;
        }
    }
}
