using System.Collections.Generic;
using TungDiroot.Core.Visibility;
using Xunit;

namespace TungDiroot.Core.Tests.Visibility;

public sealed class BulkVgSelectionPlannerTests
{
    [Fact]
    public void Expand_targets_flattens_sheets_deduplicates_views_and_excludes_source()
    {
        var selected = new[]
        {
            new BulkVgTargetSelection(10, BulkVgTargetKind.View, new long[0]),
            new BulkVgTargetSelection(20, BulkVgTargetKind.Sheet, new long[] { 10, 11, 12 }),
            new BulkVgTargetSelection(12, BulkVgTargetKind.ViewTemplate, new long[0])
        };

        var result = BulkVgSelectionPlanner.Expand(selected, sourceViewId: 11);

        Assert.Equal(new long[] { 10, 12 }, result);
    }

    [Fact]
    public void Expand_rejects_unknown_target_kind()
    {
        var selected = new[] { new BulkVgTargetSelection(10, (BulkVgTargetKind)99, new long[0]) };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => BulkVgSelectionPlanner.Expand(selected, 1));
    }
}
