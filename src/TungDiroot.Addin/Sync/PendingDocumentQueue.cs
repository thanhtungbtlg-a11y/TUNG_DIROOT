using System;
using System.Collections.Generic;

namespace TungDiroot.Addin.Sync;

public sealed class PendingDocumentQueue
{
    private readonly object _gate = new();
    private readonly Queue<string> _queue = new();
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);

    public void Enqueue(string documentUniqueId)
    {
        if (string.IsNullOrWhiteSpace(documentUniqueId)) return;
        lock (_gate)
        {
            if (!_known.Add(documentUniqueId)) return;
            _queue.Enqueue(documentUniqueId);
        }
    }

    public bool TryDequeue(string? activeDocumentUniqueId, out string documentUniqueId)
    {
        documentUniqueId = string.Empty;
        if (string.IsNullOrWhiteSpace(activeDocumentUniqueId)) return false;
        lock (_gate)
        {
            if (_queue.Count == 0 || !_known.Contains(activeDocumentUniqueId!)) return false;
            var pending = _queue.ToArray();
            _queue.Clear();
            foreach (var item in pending)
            {
                if (string.Equals(item, activeDocumentUniqueId, StringComparison.Ordinal)) documentUniqueId = item;
                else _queue.Enqueue(item);
            }
            _known.Remove(documentUniqueId);
            return true;
        }
    }
}
