using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace Luma;

public sealed class GroupBackupDocument
{
    public string Format = "Luma.GroupBackup";
    public int Version = 1;
    public string CreatedUtc = DateTime.UtcNow.ToString("o");
    public Config Data;
    public Dictionary<string, string> Assets = new Dictionary<string, string>();
}

public static class GroupBackup
{
    const int MaximumSize = 64 * 1024 * 1024;
    const string AssetPrefix = "@asset:";
    static JavaScriptSerializer Json() => new JavaScriptSerializer { MaxJsonLength = MaximumSize, RecursionLimit = 128 };
    public static Config Clone(Config config) => Json().Deserialize<Config>(Json().Serialize(config));
    static string Hash(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""); }
    static bool Equal(string a, string b) => !String.IsNullOrEmpty(a) && !String.IsNullOrEmpty(b) && String.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    // Merged originals are recovery data too; their images must travel with the backup.
    static IEnumerable<Entry> BackupEntries(Config config) => config.Apps.Concat(config.DuplicateEntries ?? new List<Entry>()).Where(entry => entry != null);
    static string ArchiveKey(Entry entry)
    {
        var json = Json();
        var copy = json.Deserialize<Entry>(json.Serialize(entry));
        copy.CustomIcon = AssetIdentity(copy.CustomIcon);
        copy.ImagePath = AssetIdentity(copy.ImagePath);
        // JavaScriptSerializer may enumerate reflected fields in a different order
        // after its metadata cache changes. Archive identity must ignore that order.
        var fields = (Dictionary<string, object>)json.DeserializeObject(json.Serialize(copy));
        return json.Serialize(new SortedDictionary<string, object>(fields, StringComparer.Ordinal));
    }
    static string AssetIdentity(string path)
    {
        if (String.IsNullOrEmpty(path)) return path;
        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("backup-icons/", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(normalized.Substring(13), "^[A-F0-9]{64}\\.png$", RegexOptions.IgnoreCase))
            return AssetPrefix + Path.GetFileNameWithoutExtension(normalized).ToUpperInvariant();
        return path;
    }

    public static void Export(string path, Config current, string dataDirectory)
    {
        var document = new GroupBackupDocument { Data = Clone(current) };
        document.Data.Normalize();
        foreach (var entry in BackupEntries(document.Data))
        {
            entry.CustomIcon = Collect(entry.CustomIcon, "custom-icons", dataDirectory, document);
            if (entry.Website) entry.ImagePath = Collect(entry.ImagePath, "website-icons", dataDirectory, document);
        }
        string json = Json().Serialize(document);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumSize) throw new InvalidDataException("备份文件过大，请减少自定义图标后重试。");
        string full = Path.GetFullPath(path), temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, json); if (File.Exists(full)) File.Replace(temporary, full, null); else File.Move(temporary, full); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    static string Collect(string path, string kind, string directory, GroupBackupDocument document)
    {
        if (String.IsNullOrEmpty(path)) return path;
        string leaf = Path.GetFileName(path);
        var file = new[] { path, Path.Combine(directory, path), Path.Combine(directory, kind, leaf), Path.Combine(directory, "backup-icons", leaf), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, kind, leaf) }.FirstOrDefault(File.Exists);
        if (file == null || new FileInfo(file).Length > 8 * 1024 * 1024) return path;
        var bitmap = IconLoader.ReadImage(file) as BitmapSource;
        if (bitmap == null || bitmap.PixelWidth > 4096 || bitmap.PixelHeight > 4096) return path;
        using (var stream = new MemoryStream())
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
            byte[] bytes = stream.ToArray(); string key = Hash(bytes);
            document.Assets[key] = Convert.ToBase64String(bytes);
            return AssetPrefix + key;
        }
    }

    public static GroupBackupDocument Read(string path)
    {
        if (new FileInfo(path).Length > MaximumSize) throw new InvalidDataException("备份超过 64MB，未导入。");
        string text = File.ReadAllText(path);
        var keys = Json().DeserializeObject(text) as Dictionary<string, object>;
        GroupBackupDocument document;
        if (keys != null && keys.ContainsKey("Format"))
        {
            document = Json().Deserialize<GroupBackupDocument>(text);
            if (document.Format != "Luma.GroupBackup" || document.Version != 1) throw new InvalidDataException("不支持这个备份格式或版本。");
        }
        else if (keys != null && keys.ContainsKey("Groups") && keys.ContainsKey("Rules") && !keys.ContainsKey("Apps"))
        {
            var template = Json().Deserialize<GroupTemplate>(text);
            if (template.Version != 1 || template.Rules == null || template.Groups == null) throw new InvalidDataException("分组模板不完整。");
            var config = new Config(); template.Apply(config); document = new GroupBackupDocument { Data = config };
        }
        else document = new GroupBackupDocument { Data = ConfigStore.Read(path) };
        if (document.Data == null || document.Data.Groups == null || document.Data.Apps == null || document.Data.Apps.Any(a => a == null) || document.Data.Groups.Count > 2000 || document.Data.Apps.Count > 20000) throw new InvalidDataException("备份的分组或应用数据不完整。");
        foreach (var entry in document.Data.Apps)
        {
            if (String.IsNullOrWhiteSpace(entry.Id) || String.IsNullOrWhiteSpace(entry.Target)) throw new InvalidDataException("备份中存在缺少身份或目标的快捷入口。");
            if (entry.Website) { Uri uri; if (!Websites.TryUrl(entry.Target, out uri)) throw new InvalidDataException("备份包含无效的网站地址。"); entry.Target = uri.AbsoluteUri; }
        }
        document.Assets = document.Assets ?? new Dictionary<string, string>();
        if (document.Assets.Count > 4096) throw new InvalidDataException("图标数量超过限制。");
        long total = 0;
        foreach (var asset in document.Assets)
        {
            if (!Regex.IsMatch(asset.Key, "^[A-F0-9]{64}$") || asset.Value == null || asset.Value.Length > 12 * 1024 * 1024) throw new InvalidDataException("备份图标数据无效。");
            byte[] bytes = Convert.FromBase64String(asset.Value); total += bytes.Length;
            if (total > 32 * 1024 * 1024 || Hash(bytes) != asset.Key) throw new InvalidDataException("备份图标校验失败。");
            using (var stream = new MemoryStream(bytes))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Any(frame => frame.PixelWidth > 4096 || frame.PixelHeight > 4096)) throw new InvalidDataException("备份图标尺寸过大。");
            }
        }
        foreach (var entry in BackupEntries(document.Data)) foreach (string reference in new[] { entry.CustomIcon, entry.ImagePath })
            if (reference != null && reference.StartsWith(AssetPrefix) && !document.Assets.ContainsKey(reference.Substring(AssetPrefix.Length))) throw new InvalidDataException("备份缺少引用的图标。");
        document.Data.Normalize();
        return document;
    }

    public static Config Merge(Config current, Config backup)
    {
        var result = Clone(current); var source = Clone(backup); source.Normalize(); result.Normalize();
        result.Groups = source.Groups.Concat(result.Groups).Distinct().ToList();
        result.HiddenGroups = source.HiddenGroups.Concat(result.HiddenGroups.Where(group => !source.Groups.Contains(group))).Distinct().ToList();
        result.CollapsedGroups = source.CollapsedGroups.Concat(result.CollapsedGroups.Where(group => !source.Groups.Contains(group))).Distinct().ToList();
        result.GroupRules = source.GroupRules.Concat(GroupTemplate.Export(source).Rules).Concat(result.GroupRules).Where(rule => rule != null).GroupBy(rule => String.Join("\u001f", GroupTemplate.NormalizeName(rule.Name), rule.Executable ?? "", rule.StableId ?? "", rule.Group ?? ""), StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        result.AppFolders = source.AppFolders.Concat(result.AppFolders.Where(folder => !source.AppFolders.Any(saved => saved.Id == folder.Id))).ToList();
        result.DuplicateEntries = source.DuplicateEntries.Concat(result.DuplicateEntries)
            .Where(entry => entry != null).GroupBy(ArchiveKey)
            .Select(group => group.First()).ToList();
        foreach (var entry in source.Apps)
        {
            var existing = result.Apps.FirstOrDefault(a => Equal(a.Id, entry.Id)) ?? result.Apps.FirstOrDefault(a => Equal(a.Target, entry.Target));
            if (existing == null && !entry.Website && !entry.FavoriteFolder)
            {
                var candidates = result.Apps.Where(a => !a.Website && !a.FavoriteFolder && Equal(a.Name, entry.Name)).ToList();
                if (candidates.Count == 1) existing = candidates[0];
            }
            if (existing == null) result.Apps.Add(entry);
            else
            {
                existing.Group = entry.Group; existing.FolderId = entry.FolderId; existing.Favorite = entry.Favorite;
                existing.TopPinned = entry.TopPinned; existing.Pinned = entry.Pinned; existing.SortOrder = entry.SortOrder;
                existing.PinnedSortOrder = entry.PinnedSortOrder;
                existing.CustomName = entry.CustomName; existing.Tags = entry.Tags;
                if ((existing.Website && entry.Website) || (existing.FavoriteFolder && entry.FavoriteFolder) || (Equal(existing.Id, entry.Id) && !String.IsNullOrEmpty(entry.CustomTarget)))
                { existing.Target = entry.Target; existing.CustomTarget = entry.CustomTarget; }
                existing.LastLaunchTicks = Math.Max(existing.LastLaunchTicks, entry.LastLaunchTicks);
                existing.LaunchCount = Math.Max(existing.LaunchCount, entry.LaunchCount);
                if (!String.IsNullOrEmpty(entry.CustomIcon)) existing.CustomIcon = entry.CustomIcon;
                if (entry.Website && !String.IsNullOrEmpty(entry.ImagePath)) existing.ImagePath = entry.ImagePath;
            }
            result.ExcludedIds.RemoveAll(id => Equal(id, entry.Id) || (existing != null && Equal(id, existing.Id)));
            result.RemovedEntries.RemoveAll(saved => Equal(saved.Id, entry.Id) || (existing != null && Equal(saved.Id, existing.Id)));
        }
        foreach (var app in result.Apps.Where(a => !source.Apps.Any(saved => Equal(saved.Id, a.Id)) && a.Group == "未分类"))
            app.Group = GroupTemplate.Match(app, result.GroupRules) ?? app.Group;
        result.Normalize(); return result;
    }

    public static Config Import(string path, Config current, string dataDirectory, string configPath, bool exactInitialCopy = false)
    {
        var document = Read(path); // Validate everything before touching current files.
        var merged = exactInitialCopy ? Clone(document.Data) : Merge(current, document.Data);
        ConfigStore.Snapshot(configPath);
        foreach (var asset in document.Assets)
        {
            string directory = Path.Combine(dataDirectory, "backup-icons"); Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, asset.Key + ".png"); File.WriteAllBytes(target, Convert.FromBase64String(asset.Value));
        }
        foreach (var entry in BackupEntries(merged))
        {
            if (entry.CustomIcon != null && entry.CustomIcon.StartsWith(AssetPrefix)) entry.CustomIcon = Path.Combine("backup-icons", entry.CustomIcon.Substring(AssetPrefix.Length) + ".png");
            if (entry.ImagePath != null && entry.ImagePath.StartsWith(AssetPrefix)) entry.ImagePath = Path.Combine("backup-icons", entry.ImagePath.Substring(AssetPrefix.Length) + ".png");
        }
        ConfigStore.Save(configPath, merged);
        return merged;
    }
}
