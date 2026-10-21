using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Luma;

public partial class Program
{
    readonly List<FileSystemWatcher> discoveryWatchers = new List<FileSystemWatcher>();
    DispatcherTimer discoveryTimer;
    long discoveryChangeTicks;
    int discoveryPending = 1;
    DateTime discoveryCompleted = DateTime.MinValue;
    DateTime discoveryFollowUp = DateTime.MaxValue;
    long discoveryHandledChange;
    bool discoveryStopped;
    public static FileSystemWatcher WatchDiscoveryFolder(string root, Action changed)
    {
        var watcher = new FileSystemWatcher(root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
        watcher.Created += delegate { changed(); }; watcher.Changed += delegate { changed(); };
        watcher.Deleted += delegate { changed(); }; watcher.Renamed += delegate { changed(); };
        watcher.Error += delegate { changed(); }; watcher.EnableRaisingEvents = true;
        return watcher;
    }
    void MarkDiscoveryDirty()
    {
        if (discoveryStopped) return;
        Interlocked.Exchange(ref discoveryChangeTicks, DateTime.UtcNow.Ticks);
        Interlocked.Exchange(ref discoveryPending, 1);
    }
    void StartAutoDiscovery()
    {
        if (QaMode || protectConfig || discoveryTimer != null) return;
        RefreshDiscoveryWatchers();
        discoveryTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(2) };
        discoveryTimer.Tick += delegate
        {
            if (scanning || discoveryStopped) return;
            if (DateTime.UtcNow >= discoveryFollowUp) { discoveryFollowUp = DateTime.MaxValue; Interlocked.Exchange(ref discoveryPending, 1); }
            long change = Interlocked.Read(ref discoveryChangeTicks);
            if (change != 0 && DateTime.UtcNow.Ticks - change < TimeSpan.FromSeconds(2).Ticks) return;
            if (Volatile.Read(ref discoveryPending) != 0 || DateTime.UtcNow - discoveryCompleted > TimeSpan.FromMinutes(5)) DiscoverNewApps();
        };
        discoveryTimer.Start();
    }
    public static void SyncDiscoveryWatches(List<FileSystemWatcher> watchers, IEnumerable<string> roots, Action changed, Action<Exception> failed)
    {
        var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            try { if (Directory.Exists(root)) desired.Add(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar); }
            catch (Exception error) { failed(error); }
        }
        Func<FileSystemWatcher, string> key = watcher => Path.GetFullPath(watcher.Path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var watcher in watchers.ToArray())
            if (!desired.Contains(key(watcher))) { watcher.Dispose(); watchers.Remove(watcher); }
        foreach (string root in desired)
        {
            if (watchers.Any(watcher => String.Equals(key(watcher), root, StringComparison.OrdinalIgnoreCase))) continue;
            try { watchers.Add(WatchDiscoveryFolder(root, changed)); }
            catch (Exception error) { failed(error); }
        }
    }
    void RefreshDiscoveryWatchers()
    {
        if (QaMode || protectConfig || discoveryStopped) return;
        var roots = new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu }
            .Select(Environment.GetFolderPath).Concat(State.Folders);
        SyncDiscoveryWatches(discoveryWatchers, roots, MarkDiscoveryDirty, error => CrashLog.Write("discovery-watcher-unavailable", error));
    }
    void RequestDiscoveryOnOpen()
    {
        if (QaMode || protectConfig || discoveryStopped) return;
        if (DateTime.UtcNow - discoveryCompleted > TimeSpan.FromMinutes(1)) Interlocked.Exchange(ref discoveryPending, 1);
    }
    async void DiscoverNewApps()
    {
        if (scanning || QaMode || protectConfig || discoveryStopped) return;
        RefreshDiscoveryWatchers();
        scanning = true; Interlocked.Exchange(ref discoveryPending, 0);
        long change = Interlocked.Read(ref discoveryChangeTicks);
        if (change != 0 && change != discoveryHandledChange) { discoveryHandledChange = change; discoveryFollowUp = DateTime.UtcNow.AddSeconds(15); }
        var folders = State.Folders.ToList();
        try
        {
            var found = await Task.Run(() => Scanner.Scan(folders));
            if (discoveryStopped) return;
            int added = State.MergeDiscovered(found);
            CoreSystemApps.Reveal(State);
            discoveryCompleted = DateTime.UtcNow;
            if (added > 0) { Save(); Library?.Render(); }
        }
        catch (Exception error) { discoveryCompleted = DateTime.UtcNow; CrashLog.Write("automatic-app-discovery", error); }
        finally { scanning = false; }
    }
    void StopAutoDiscovery()
    {
        discoveryStopped = true; discoveryTimer?.Stop();
        foreach (var watcher in discoveryWatchers) watcher.Dispose();
        discoveryWatchers.Clear();
    }
}
