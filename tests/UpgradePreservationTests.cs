using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma;

class UpgradePreservationTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static object Canonical(object value)
    {
        var map = value as IDictionary<string, object>;
        if (map != null) { var sorted = new SortedDictionary<string, object>(); foreach (var pair in map) sorted[pair.Key] = Canonical(pair.Value); return sorted; }
        var array = value as object[];
        return array == null ? value : array.Select(Canonical).ToArray();
    }
    static string Snapshot(Config config)
    {
        var json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
        return json.Serialize(Canonical(json.DeserializeObject(json.Serialize(config))));
    }
    [STAThread] static int Main()
    {
        try
        {
            Program.QaMode = true;
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "upgrade-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
            var legacy = json.Deserialize<Config>("{\"Groups\":[\"未分类\"],\"Apps\":[{\"Id\":\"old\",\"Target\":\"old.exe\"}]}");
            legacy.Normalize();
            Check(legacy.ShowAllAppsEntry && !legacy.IncludeHiddenInAllApps && !legacy.HideDesktopIcons, "new public preferences have compatible defaults for old JSON");
            Check(legacy.Apps[0].PinnedSortOrder == int.MaxValue && legacy.DuplicateEntries.Count == 0, "old entries gain independent pin order and an empty duplicate archive");

            string image = Path.Combine(directory, "recovery.png");
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 50, 120, 230, 255 }, 4);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(image)) png.Save(stream);
            var saved = new Config(); saved.Groups.Add("设计");
            saved.Apps.Add(new Entry { Id = "p", Name = "Pinned", Target = "p.exe", Group = "设计", TopPinned = true, PinnedSortOrder = 7, SortOrder = 23 });
            saved.DuplicateEntries.Add(new Entry { Id = "old-alias", Name = "Archived", Target = "old.lnk", CustomIcon = image, TopPinned = true, PinnedSortOrder = 12, Tags = "recover" });
            string before = Snapshot(saved), backup = Path.Combine(directory, "saved.lumabackup");
            GroupBackup.Export(backup, saved, directory);
            Check(before == Snapshot(saved), "archived-icon export never changes the source configuration");
            var document = GroupBackup.Read(backup);
            Check(document.Data.Apps[0].PinnedSortOrder == 7 && document.Data.Apps[0].SortOrder == 23, "actual backup file retains independent pin and category orders");
            Check(document.Data.DuplicateEntries.Count == 1 && document.Assets.Count == 1 && document.Data.DuplicateEntries[0].CustomIcon.StartsWith("@asset:"), "duplicate recovery records and their original icons are embedded in the backup");
            File.Delete(image);

            var current = new Config { ThemeMode = "深色", ShowAllAppsEntry = false, IncludeHiddenInAllApps = true, HideDesktopIcons = true };
            current.Groups.Add("现有"); current.ExcludedIds.Add("do-not-rescan");
            current.Apps.Add(new Entry { Id = "p", Name = "Pinned", Target = "p.exe", Group = "现有", SortOrder = 1 });
            current.DuplicateEntries.Add(new Entry { Id = "local", Name = "Local archive", Target = "local.exe", Tags = "keep" });
            string target = Path.Combine(directory, "import-data"), configPath = Path.Combine(target, "settings.json");
            ConfigStore.Save(configPath, current);
            string original = File.ReadAllText(configPath);
            var imported = GroupBackup.Import(backup, current, target, configPath);
            Check(imported.Apps.Single().PinnedSortOrder == 7 && imported.Apps.Single().SortOrder == 23, "backup merge restores pin order without conflating category order");
            Check(imported.ThemeMode == "深色" && !imported.ShowAllAppsEntry && imported.IncludeHiddenInAllApps && imported.HideDesktopIcons && imported.ExcludedIds.Contains("do-not-rescan"), "group import retains the receiving machine's appearance, visibility and exclusions");
            Check(imported.DuplicateEntries.Count == 2 && imported.DuplicateEntries.Any(e => e.Id == "local"), "import preserves local and incoming duplicate recovery records");
            var archived = imported.DuplicateEntries.Single(e => e.Id == "old-alias");
            Check(File.Exists(Path.Combine(target, archived.CustomIcon)) && archived.TopPinned && archived.PinnedSortOrder == 12 && archived.Tags == "recover", "archived metadata and icon restore after the source image is removed");
            Check(Directory.GetFiles(Path.Combine(target, "backups")).Any(file => File.ReadAllText(file) == original), "import snapshots the exact original configuration before replacing it");
            var repeated = GroupBackup.Import(backup, imported, target, configPath);
            Check(repeated.DuplicateEntries.Count == 2, "reimport remains idempotent when embedded asset references become relative image paths");
            string second = Path.Combine(directory, "second.lumabackup");
            GroupBackup.Export(second, repeated, target);
            Check(GroupBackup.Read(second).Assets.Count == 1, "recovered archive icons survive a second portable backup");

            var restore = new Config(); restore.Apps.Add(new Entry { Id = "p", Name = "Pinned", Target = "p.exe" });
            ConfigStore.RestoreGroups(restore, saved);
            Check(restore.Apps[0].PinnedSortOrder == 7 && restore.Apps[0].SortOrder == 23, "legacy group restoration also copies independent pin order");
            document.Assets.Clear(); string invalid = Path.Combine(directory, "invalid.lumabackup"); File.WriteAllText(invalid, json.Serialize(document));
            bool rejected = false; string stored = File.ReadAllText(configPath);
            try { GroupBackup.Import(invalid, repeated, target, configPath); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllText(configPath) == stored, "missing archived icon data is rejected before changing saved configuration");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
