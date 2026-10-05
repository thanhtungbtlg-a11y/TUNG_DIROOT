using System;
using System.Collections.Generic;
using System.Linq;

namespace TungDiroot.Core.Sync;

public sealed record AutoSyncCandidate(Guid LinkId, bool AutoSync, bool SourceChanged, bool ViewEditable, string? SkipReasonCode = null);
public sealed record AutoSyncSkip(Guid LinkId, string ReasonCode);
public sealed record AutoSyncPlan(IReadOnlyList<Guid> SelectedLinkIds, IReadOnlyList<AutoSyncSkip> Skipped);

public static class AutoSyncPlanner
{
    public static AutoSyncPlan SelectChanged(IEnumerable<AutoSyncCandidate> candidates)
    {
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        var selected = new List<Guid>();
        var skipped = new List<AutoSyncSkip>();
        foreach (var item in candidates)
        {
            if (!item.AutoSync) { skipped.Add(new AutoSyncSkip(item.LinkId, "AUTO_SYNC_DISABLED")); continue; }
            if (!item.SourceChanged) { skipped.Add(new AutoSyncSkip(item.LinkId, item.SkipReasonCode ?? "UNCHANGED")); continue; }
            if (!item.ViewEditable) { skipped.Add(new AutoSyncSkip(item.LinkId, item.SkipReasonCode ?? "VIEW_NOT_EDITABLE")); continue; }
            selected.Add(item.LinkId);
        }
        return new AutoSyncPlan(selected.AsReadOnly(), skipped.AsReadOnly());
    }
}
