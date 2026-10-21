using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Luma;
class DiscoveryRefreshTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread] static int Main()
    {
        try
        {
            var state = new Config(); state.Groups.Add("设计"); state.HiddenGroups.Add("未分类"); state.EnsureUngroupedCategory();
            Check(state.VisibleGroups().Contains("未分类") && Config.GroupDisplayName("未分类") == "未分组应用", "upgrade exposes an ungrouped-app category without renaming stored group identities");
            state.HiddenGroups.Add("设计"); state.EnsureUngroupedCategory(); Check(state.HiddenGroups.Contains("设计"), "ungrouped migration preserves other hidden categories");
            state.HiddenGroups.Add("未分类"); state.EnsureUngroupedCategory();
            Check(state.HiddenGroups.Contains("未分类"), "later user choices to hide ungrouped apps are not overwritten by repeated migration");
            state.HiddenGroups.Remove("未分类");
            var editor = new Entry { Id = "editor", Name = "Editor", Target = "editor.exe", Group = "设计", TopPinned = true, CustomName = "我的编辑器", CustomIcon = "my-icon.png", Favorite = true };
            var site = new Entry { Id = "site", Name = "Site", Target = "https://example.com/", Group = "设计", Website = true };
            state.Apps.AddRange(new[] { editor, site });
            var installed = new Entry { Id = "new-app", Name = "New App", Target = "new.exe" };
            Check(state.MergeDiscovered(new List<Entry> { installed }) == 1 && installed.Group == "未分类" && state.IsVisible(installed), "new installation is added to the visible ungrouped category");
            Check(state.MergeDiscovered(new List<Entry> { new Entry { Id = "editor", Name = "Editor updated", Target = "editor-new.exe" } }) == 0 && editor.TopPinned && editor.Favorite && editor.Group == "设计" && editor.CustomName == "我的编辑器" && editor.CustomIcon == "my-icon.png", "discovery preserves existing grouping, aliases, icons and pins");
            Check(state.MergeDiscovered(new List<Entry>()) == 0 && state.Apps.Count == 3 && state.Apps.Contains(site), "empty or partial scans never remove existing applications or websites");
            Check(state.MergeDiscovered(new List<Entry> { new Entry { Id = "new-app", Name = "New App", Target = "new.exe" } }) == 0 && state.Apps.Count == 3, "repeated automatic refresh does not duplicate installed apps");
            state.ExcludedIds.Add("REMOVED");
            Check(state.MergeDiscovered(new List<Entry> { new Entry { Id = "removed", Name = "Removed", Target = "removed.exe" } }) == 0, "automatic refresh honors case-insensitive explicit exclusions");
            state.GroupRules.Add(new GroupRule { Name = "Design App", Group = "设计" });
            var matched = new Entry { Id = "design-new", Name = "Design App", Target = "design-new.exe" }; state.MergeDiscovered(new List<Entry> { matched });
            Check(matched.Group == "设计", "matching existing classification rules still group new installations automatically");
            string fixture=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"discovery-fixture",Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(fixture);
            using(var signal=new System.Threading.ManualResetEvent(false))
            using(var watcher=Program.WatchDiscoveryFolder(fixture,()=>signal.Set())) {
                string sub=System.IO.Path.Combine(fixture,"Programs");System.IO.Directory.CreateDirectory(sub);signal.Reset();
                System.IO.File.WriteAllText(System.IO.Path.Combine(sub,"New Application.lnk"),"isolated watcher fixture only");
                Check(signal.WaitOne(3000),"new shortcut creation in an isolated start-menu fixture schedules discovery");
            }
            string another = System.IO.Path.Combine(fixture, "Additional"); System.IO.Directory.CreateDirectory(another);
            var watches = new List<System.IO.FileSystemWatcher>();
            try
            {
                Program.SyncDiscoveryWatches(watches, new[] { fixture, fixture + System.IO.Path.DirectorySeparatorChar }, () => { }, error => { throw error; });
                Check(watches.Count == 1, "equivalent configured folder paths create just one watcher");
                Program.SyncDiscoveryWatches(watches, new[] { fixture, another }, () => { }, error => { throw error; });
                Check(watches.Count == 2, "a newly configured scan directory receives its own watcher without restarting");
                Program.SyncDiscoveryWatches(watches, new[] { another }, () => { }, error => { throw error; });
                Check(watches.Count == 1 && watches[0].Path.TrimEnd('\\') == another, "removed scan-directory watchers are disposed and removed");
            }
            finally { foreach (var watcher in watches) watcher.Dispose(); }
            Program.QaMode = true; var app = new Program();
            typeof(Program).GetMethod("StartAutoDiscovery", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, null);
            Check(typeof(Program).GetField("discoveryTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app) == null, "offline verification never starts live discovery timers or desktop watchers");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
