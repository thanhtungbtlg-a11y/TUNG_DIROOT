using System;
using TungDiroot.Core.Visibility;
using Xunit;

namespace TungDiroot.Core.Tests.Visibility;

public sealed class BulkVgFilterPlannerTests
{
    [Fact]
    public void Replace_reports_only_unselected_removals_and_keeps_existing_selected_filters_for_update()
    {
        var plan = BulkVgFilterPlanner.Plan(new long[] { 20, 10, 30 }, new long[] { 10, 20, 40 }, BulkVgApplyMode.Replace);

        Assert.Equal(new long[] { 30 }, plan.Remove);
        Assert.Equal(new long[] { 40 }, plan.Add);
        Assert.Equal(new long[] { 10, 20 }, plan.Update);
        Assert.Equal(new long[] { 10, 20, 40 }, plan.FinalOrder);
        Assert.True(plan.RebuildOrder);
        Assert.Empty(plan.Skip);
    }

    [Fact]
    public void Replace_with_identical_selected_settings_still_reports_an_outside_selection_removal()
    {
        var plan = BulkVgFilterPlanner.Plan(new long[] { 10, 30 }, new long[] { 10 }, BulkVgApplyMode.Replace);

        Assert.Equal(new long[] { 30 }, plan.Remove);
        Assert.Equal(new long[] { 10 }, plan.Update);
        Assert.Equal(new long[] { 10 }, plan.FinalOrder);
        Assert.False(plan.RebuildOrder);
    }

    [Fact]
    public void Replace_rebuilds_when_a_new_filter_must_precede_an_existing_filter()
    {
        var plan = BulkVgFilterPlanner.Plan(new long[] { 20 }, new long[] { 10, 20 }, BulkVgApplyMode.Replace);

        Assert.Equal(new long[] { 10 }, plan.Add);
        Assert.Equal(new long[] { 20 }, plan.Update);
        Assert.Equal(new long[] { 10, 20 }, plan.FinalOrder);
        Assert.True(plan.RebuildOrder);
        Assert.Empty(plan.Remove);
    }

    [Fact]
    public void Merge_keeps_target_membership_and_order_and_appends_only_missing_selected_filters()
    {
        var plan = BulkVgFilterPlanner.Plan(new long[] { 20, 30, 10 }, new long[] { 10, 20, 40 }, BulkVgApplyMode.Merge);

        Assert.Empty(plan.Remove);
        Assert.Equal(new long[] { 40 }, plan.Add);
        Assert.Equal(new long[] { 10, 20 }, plan.Update);
        Assert.Equal(new long[] { 20, 30, 10, 40 }, plan.FinalOrder);
        Assert.False(plan.RebuildOrder);
        Assert.Empty(plan.Skip);
    }

    [Fact]
    public void Update_existing_skips_missing_filters_without_adding_removing_or_reordering()
    {
        var plan = BulkVgFilterPlanner.Plan(new long[] { 20, 30, 10 }, new long[] { 10, 40, 20 }, BulkVgApplyMode.UpdateExisting);

        Assert.Empty(plan.Remove);
        Assert.Empty(plan.Add);
        Assert.Equal(new long[] { 10, 20 }, plan.Update);
        Assert.Equal(new long[] { 40 }, plan.Skip);
        Assert.Equal(new long[] { 20, 30, 10 }, plan.FinalOrder);
        Assert.False(plan.RebuildOrder);
    }

    [Theory]
    [InlineData(BulkVgApplyMode.Merge)]
    [InlineData(BulkVgApplyMode.Replace)]
    [InlineData(BulkVgApplyMode.UpdateExisting)]
    public void Category_only_selection_does_not_change_any_filter_membership(BulkVgApplyMode mode)
    {
        var plan = BulkVgFilterPlanner.Plan(new long[] { 20, 30, 10 }, Array.Empty<long>(), mode);

        Assert.Empty(plan.Remove);
        Assert.Empty(plan.Add);
        Assert.Empty(plan.Update);
        Assert.Empty(plan.Skip);
        Assert.Equal(new long[] { 20, 30, 10 }, plan.FinalOrder);
        Assert.False(plan.RebuildOrder);
    }

    [Fact]
    public void Plan_deduplicates_selected_filters_without_reordering_them()
    {
        var plan = BulkVgFilterPlanner.Plan(new long[] { 20, 30 }, new long[] { 10, 20, 10 }, BulkVgApplyMode.Merge);

        Assert.Equal(new long[] { 10 }, plan.Add);
        Assert.Equal(new long[] { 20 }, plan.Update);
        Assert.Equal(new long[] { 20, 30, 10 }, plan.FinalOrder);
    }

    [Fact]
    public void Plan_rejects_unknown_mode_before_a_destructive_membership_change()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BulkVgFilterPlanner.Plan(
            new long[] { 20 }, new long[] { 10 }, (BulkVgApplyMode)99));
    }
}
