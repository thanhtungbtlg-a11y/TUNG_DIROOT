using System;
using TungDiroot.Core.Sync;
using Xunit;

namespace TungDiroot.Core.Tests.Sync;

public sealed class AutoSyncPlannerTests
{
    [Fact]
    public void Selects_only_auto_sync_changed_editable_links()
    {
        var selected = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var blocked = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var result = AutoSyncPlanner.SelectChanged(new[]
        {
            new AutoSyncCandidate(selected, true, true, true),
            new AutoSyncCandidate(Guid.NewGuid(), true, false, true),
            new AutoSyncCandidate(blocked, true, true, false, "VIEW_NOT_EDITABLE"),
            new AutoSyncCandidate(Guid.NewGuid(), false, true, true)
        });
        Assert.Equal(new[] { selected }, result.SelectedLinkIds);
        Assert.Contains(result.Skipped, item => item.LinkId == blocked && item.ReasonCode == "VIEW_NOT_EDITABLE");
    }
}
