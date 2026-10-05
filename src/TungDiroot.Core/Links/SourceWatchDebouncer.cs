using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TungDiroot.Core.Links;

public enum SourceWatchChangeKind
{
    Created,
    Changed,
    Renamed,
    Deleted
}

public sealed class SourceWatchDebouncer : IDisposable
{
    private readonly object _gate = new();
    private readonly TimeSpan _quietPeriod;
    private readonly Dictionary<string, PendingPath> _pending = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public SourceWatchDebouncer(TimeSpan quietPeriod)
    {
        if (quietPeriod <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        _quietPeriod = quietPeriod;
    }

    public bool Record(SourceWatchChangeKind kind, string path, string? oldPath, DateTimeOffset observedUtc)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!TryNormalize(path, out var normalized)) return false;
            RecordPath(normalized, observedUtc);
            if (kind == SourceWatchChangeKind.Renamed && TryNormalize(oldPath, out var normalizedOld))
                RecordPath(normalizedOld, observedUtc);
            return true;
        }
    }

    public IReadOnlyList<string> DrainReady(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (_disposed || _pending.Count == 0) return Array.Empty<string>();
            var ready = _pending.Values
                .Where(item => nowUtc - item.LastObservedUtc >= _quietPeriod)
                .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var item in ready) _pending.Remove(item.Path);
            return new ReadOnlyCollection<string>(ready.Select(item => item.Path).ToArray());
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear();
        }
    }

    private void RecordPath(string path, DateTimeOffset observedUtc)
    {
        if (_pending.TryGetValue(path, out var current))
            _pending[path] = current with { LastObservedUtc = observedUtc };
        else
            _pending[path] = new PendingPath(path, observedUtc);
    }

    private static bool TryNormalize(string? path, out string normalized)
    {
        normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : path!.Trim().Replace('/', '\\');
        return normalized.Length > 0;
    }

    private sealed record PendingPath(string Path, DateTimeOffset LastObservedUtc);
}
