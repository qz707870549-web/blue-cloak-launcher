using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Luma;

public static class ApplicationIdentity
{
    public static string Key(Entry app)
    {
        if (app == null || app.Website || app.FavoriteFolder || app.Manual || !string.IsNullOrWhiteSpace(app.CustomTarget)) return null;
        string id = (app.Id ?? "").Trim().Replace('/', '\\');
        var match = Regex.Match(id, @"^(?<exe>.+?\.exe)(?:\s*\|\s*(?<args>.*))?$", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        string exe = match.Groups["exe"].Value.Trim('"').ToLowerInvariant();
        if (!exe.Contains("\\")) return null;
        string args = match.Groups["args"].Value.Trim();
        string file = Path.GetFileName(exe);
        bool main = new[] { "wps.exe", "wpsoffice.exe", "doubao.exe", "quark.exe", "bandizip.exe" }.Contains(file);
        // These installers create shortcuts with different launch-source flags,
        // and leave versioned paths registered in App Paths after upgrades.
        // Explicit documents, URLs and browser profiles remain separate entries.
        string significant = Regex.Replace(args,
            @"(?:^|\s)(?:--?|/)(?:from|source|channel|launch-source|launch-from)(?:[= :]?(?:""[^""]*""|[^\s]+))?(?=\s|$)|(?:^|\s)(?:--?|/)(?:show|show-window|from-desktop|shortcut-startup|start-from-desktop|prometheus)(?=\s|$)", "", RegexOptions.IgnoreCase).Trim();
        if (main && significant.Length == 0)
        {
            exe = Regex.Replace(exe, @"\\(?:app-)?\d+(?:\.\d+){1,5}(?=\\)", @"\{version}");
            args = "";
        }
        return exe + " | " + args.ToLowerInvariant();
    }

    public static bool Same(Entry first, Entry second)
    {
        string key = Key(first);
        return key != null && string.Equals(key, Key(second), StringComparison.OrdinalIgnoreCase);
    }

    public static List<Entry> Unique(IEnumerable<Entry> source)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return source.Where(a => Key(a) == null || keys.Add(Key(a))).ToList();
    }

    public static void MergeMetadata(Entry keep, Entry other)
    {
        if (string.IsNullOrEmpty(keep.CustomName)) keep.CustomName = other.CustomName;
        if (string.IsNullOrEmpty(keep.CustomIcon)) keep.CustomIcon = other.CustomIcon;
        if (string.IsNullOrEmpty(keep.ImagePath)) keep.ImagePath = other.ImagePath;
        if (string.IsNullOrEmpty(keep.FolderId)) keep.FolderId = other.FolderId;
        keep.Tags = string.Join(" ", new[] { keep.Tags, other.Tags }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        keep.TopPinned |= other.TopPinned; keep.Pinned |= other.Pinned; keep.Favorite |= other.Favorite;
        keep.LastLaunchTicks = Math.Max(keep.LastLaunchTicks, other.LastLaunchTicks);
        keep.LaunchCount = Math.Max(keep.LaunchCount, other.LaunchCount);
        keep.SortOrder = Math.Min(keep.SortOrder, other.SortOrder);
        keep.PinnedSortOrder = Math.Min(keep.PinnedSortOrder, other.PinnedSortOrder);
    }
}
