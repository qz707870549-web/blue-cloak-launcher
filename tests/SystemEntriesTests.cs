using System;
using System.Linq;
using System.Windows.Media.Imaging;
using Luma;

class SystemEntriesTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread] static int Main()
    {
        try
        {
            Program.QaMode = true;
            var config = new Config(); config.Normalize(); SystemShortcuts.Ensure(config);
            Check(config.Apps.Count == 3 && config.Groups.Contains("电脑") && config.Apps.All(config.IsVisible), "three shell namespace entries are visible without the system-app switch");
            var computer = config.Apps.First(a => a.Name == "此电脑"); computer.Group = "我的分类"; computer.TopPinned = true; computer.CustomName = "我的电脑"; config.Groups.Add("我的分类");
            SystemShortcuts.Ensure(config);
            Check(config.Apps.Count == 3 && computer.Group == "我的分类" && computer.TopPinned && computer.CustomName == "我的电脑", "system entry migration is idempotent and preserves customization");
            var recycle = config.Apps.First(a => a.Name == "回收站"); config.Apps.Remove(recycle); config.ExcludedIds.Add(recycle.Id); SystemShortcuts.Ensure(config);
            Check(!config.Apps.Any(a => a.Id == recycle.Id), "explicitly removed shell entries do not reappear");
            foreach (var entry in SystemShortcuts.Entries())
            {
                var icon = IconLoader.Load(entry) as BitmapSource;
                Check(icon != null && icon.PixelWidth >= 32 && icon.IsFrozen, "real system icon extracted without launching a window: " + entry.Name);
                Check(!DiscoveryRules.IsSystemApp(entry) && !DiscoveryRules.IsUtility(entry), "shell entry remains outside optional component filtering: " + entry.Name);
            }
            foreach (string package in new[] { "Microsoft.WindowsCalculator", "Microsoft.WindowsNotepad", "Microsoft.ScreenSketch", "Microsoft.MSPaint", "Microsoft.Paint", "Microsoft.GetHelp", "Microsoft.WindowsStore", "Microsoft.WindowsAlarms", "Microsoft.YourPhone", "windows.immersivecontrolpanel" })
            {
                var entry = new Entry { Id = "shell:AppsFolder\\" + package + "_8wekyb3d8bbwe!App", Target = "shell:AppsFolder\\" + package + "_8wekyb3d8bbwe!App", Name = package, Group = "未分类", SystemApp = true };
                Check(CoreSystemApps.IsCore(entry) && !DiscoveryRules.IsSystemApp(entry), "essential packaged application remains visible: " + package);
                var target = new Config(); target.Normalize(); target.HiddenGroups.Add("未分类"); target.Apps.Add(entry); CoreSystemApps.Reveal(target);
                Check(target.IsVisible(entry) && entry.Group == "工具", "old automatically hidden essential entry moves into tools: " + package);
                entry.Group = "私人"; target.Groups.Add("私人"); target.HiddenGroups.Add("私人"); CoreSystemApps.Reveal(target);
                Check(entry.Group == "私人" && !target.IsVisible(entry), "explicit group hiding is preserved: " + package);
            }
            var imported = new Config(); imported.Normalize(); var settings = new Entry { Id = "ms-settings:", Target = "ms-settings:", Name = "设置" };
            imported.MergeImported(new System.Collections.Generic.List<Entry> { settings });
            Check(imported.IsVisible(settings) && settings.Group == "工具", "newly discovered settings receives a visible default group");
            var other = new Entry { Id = "shell:AppsFolder\\Microsoft.Xbox_8wekyb3d8bbwe!App", Target = "shell:AppsFolder\\Microsoft.Xbox_8wekyb3d8bbwe!App", Name = "Xbox" };
            Check(DiscoveryRules.IsSystemApp(other), "unrelated bundled components retain the existing visibility switch");
            Check(CoreSystemApps.Fallbacks(new Entry[0]).All(CoreSystemApps.IsCore), "classic executable fallbacks contain only installed core apps");
            var oldCalc = new Entry { Id = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "calc.exe"), Target = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "calc.exe"), Name = "计算器", Group = "工具", TopPinned = true };
            var newCalc = new Entry { Id = "shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", Target = "shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", Name = "计算器" };
            Check(CoreSystemApps.Deduplicate(new[] { oldCalc, newCalc }).Count == 1, "classic and packaged launchers for one core app are deduplicated");
            var aliases = new Config(); aliases.Normalize(); aliases.Groups.Add("工具"); aliases.Apps.Add(oldCalc); aliases.MergeImported(new System.Collections.Generic.List<Entry> { newCalc });
            Check(aliases.Apps.Count == 1 && oldCalc.TopPinned, "additive system migration keeps existing aliases and manual pins");
            aliases.Apps.Clear(); aliases.RemovedEntries.Add(oldCalc); aliases.MergeImported(new System.Collections.Generic.List<Entry> { newCalc });
            Check(aliases.Apps.Count == 0, "removed core apps are not restored under an alternate package identity");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
