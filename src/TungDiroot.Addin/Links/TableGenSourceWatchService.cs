using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TungDiroot.Core.Links;

namespace TungDiroot.Addin.Links;

public sealed class TableGenSourceWatchService : IDisposable
{
    private readonly object _gate = new();
    private readonly Action<Action> _dispatch;
    private readonly Action<IReadOnlyList<string>> _sourcesChanged;
    private readonly SourceWatchDebouncer _debouncer;
    private readonly Timer _timer;
    private IReadOnlyList<FileSystemWatcher> _watchers = Array.Empty<FileSystemWatcher>();
    private HashSet<string> _trackedPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private int _timerRunning;

    public TableGenSourceWatchService(
        Action<Action> dispatch,
        Action<IReadOnlyList<string>> sourcesChanged,
        TimeSpan? quietPeriod = null)
    {
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        _sourcesChanged = sourcesChanged ?? throw new ArgumentNullException(nameof(sourcesChanged));
        var delay = quietPeriod ?? TimeSpan.FromMilliseconds(750);
        _debouncer = new SourceWatchDebouncer(delay);
        var pollMilliseconds = Math.Max(100, Math.Min(500, (int)Math.Ceiling(delay.TotalMilliseconds / 2)));
        _timer = new Timer(OnTimer, null, pollMilliseconds, pollMilliseconds);
    }

    public void UpdateSources(IEnumerable<string> sourcePaths)
    {
        if (sourcePaths == null) throw new ArgumentNullException(nameof(sourcePaths));
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourcePath in sourcePaths)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(sourcePath)) paths.Add(Path.GetFullPath(sourcePath));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // A malformed stored path is already surfaced by the TableGen link status.
            }
        }

        var nextWatchers = CreateWatchers(paths);
        IReadOnlyList<FileSystemWatcher> previous;
        lock (_gate)
        {
            if (_disposed)
            {
                DisposeWatchers(nextWatchers);
                return;
            }
            previous = _watchers;
            _watchers = nextWatchers;
            _trackedPaths = paths;
        }
        DisposeWatchers(previous);
        foreach (var watcher in nextWatchers) watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        IReadOnlyList<FileSystemWatcher> watchers;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            watchers = _watchers;
            _watchers = Array.Empty<FileSystemWatcher>();
            _trackedPaths.Clear();
        }
        _timer.Dispose();
        _debouncer.Dispose();
        DisposeWatchers(watchers);
    }

    private IReadOnlyList<FileSystemWatcher> CreateWatchers(HashSet<string> paths)
    {
        var watchers = new List<FileSystemWatcher>();
        foreach (var directory in paths.Select(Path.GetDirectoryName)
                     .Where(directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var watcher = new FileSystemWatcher(directory!)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };
                watcher.Created += OnChanged;
                watcher.Changed += OnChanged;
                watcher.Deleted += OnChanged;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnError;
                watchers.Add(watcher);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Source status remains available through manual "Kiểm tra nguồn".
            }
        }
        return watchers.AsReadOnly();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var kind = e.ChangeType switch
        {
            WatcherChangeTypes.Created => SourceWatchChangeKind.Created,
            WatcherChangeTypes.Deleted => SourceWatchChangeKind.Deleted,
            _ => SourceWatchChangeKind.Changed
        };
        _debouncer.Record(kind, e.FullPath, null, DateTimeOffset.UtcNow);
    }

    private void OnRenamed(object sender, RenamedEventArgs e) =>
        _debouncer.Record(SourceWatchChangeKind.Renamed, e.FullPath, e.OldFullPath, DateTimeOffset.UtcNow);

    private void OnError(object sender, ErrorEventArgs e)
    {
        string[] paths;
        lock (_gate) paths = _disposed ? Array.Empty<string>() : _trackedPaths.ToArray();
        var now = DateTimeOffset.UtcNow;
        foreach (var path in paths) _debouncer.Record(SourceWatchChangeKind.Changed, path, null, now);
    }

    private void OnTimer(object? state)
    {
        if (Interlocked.Exchange(ref _timerRunning, 1) != 0) return;
        try
        {
            var ready = _debouncer.DrainReady(DateTimeOffset.UtcNow);
            if (ready.Count == 0) return;
            string[] changed;
            lock (_gate)
            {
                if (_disposed) return;
                changed = ready.Where(path => _trackedPaths.Contains(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            if (changed.Length == 0) return;
            _dispatch(() =>
            {
                lock (_gate) if (_disposed) return;
                _sourcesChanged(changed);
            });
        }
        finally
        {
            Interlocked.Exchange(ref _timerRunning, 0);
        }
    }

    private static void DisposeWatchers(IEnumerable<FileSystemWatcher> watchers)
    {
        foreach (var watcher in watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }
}
