using TungDiroot.Core.Import;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class LinkTargetChangePlannerTests
{
    [Theory]
    [InlineData(TargetViewKind.Drafting, TargetViewKind.Legend, "TARGET_VIEW_KIND_MISMATCH")]
    [InlineData(TargetViewKind.Schedule, TargetViewKind.Schedule, "TARGET_VIEW_CHANGE_SCHEDULE_UNSUPPORTED")]
    public void Unsafe_target_changes_are_rejected(TargetViewKind sourceKind, TargetViewKind targetKind, string code)
    {
        var plan = LinkTargetChangePlanner.Create(sourceKind, targetKind, false);
        Assert.False(plan.CanExecute);
        Assert.Equal(code, plan.ErrorCode);
    }

    [Fact]
    public void Same_target_is_rejected()
    {
        var plan = LinkTargetChangePlanner.Create(TargetViewKind.Drafting, TargetViewKind.Drafting, true);
        Assert.False(plan.CanExecute);
        Assert.Equal("TARGET_VIEW_UNCHANGED", plan.ErrorCode);
    }

    [Theory]
    [InlineData(TargetViewKind.Drafting)]
    [InlineData(TargetViewKind.Legend)]
    public void Compatible_non_schedule_target_uses_copy_first_and_removes_old_content_after_success(TargetViewKind kind)
    {
        var plan = LinkTargetChangePlanner.Create(kind, kind, false);
        Assert.True(plan.CanExecute);
        Assert.True(plan.UseTransactionGroup);
        Assert.True(plan.RecreateAtTarget);
        Assert.True(plan.RemoveOldLinkedElements);
    }
}
