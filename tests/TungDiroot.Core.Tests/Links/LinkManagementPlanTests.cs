using System;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class LinkManagementPlanTests
{
    [Theory]
    [InlineData(LinkManagementAction.OpenView, false, true, "VIEW_DELETED")]
    [InlineData(LinkManagementAction.OpenSource, true, false, "FILE_MISSING")]
    [InlineData(LinkManagementAction.OpenSourceFolder, true, false, "FILE_MISSING")]
    public void Navigation_actions_reject_missing_targets(
        LinkManagementAction action,
        bool viewExists,
        bool sourceExists,
        string expectedCode)
    {
        var plan = LinkManagementPlanner.Create(action, viewExists, sourceExists);

        Assert.False(plan.CanExecute);
        Assert.Equal(expectedCode, plan.ErrorCode);
        Assert.False(plan.RequiresTransaction);
    }

    [Fact]
    public void Unlink_removes_registry_and_marker_but_preserves_view_and_elements()
    {
        var plan = LinkManagementPlanner.Create(LinkManagementAction.UnlinkKeepView, true, true);

        Assert.True(plan.CanExecute);
        Assert.True(plan.RequiresTransaction);
        Assert.True(plan.RemoveRegistryRecord);
        Assert.True(plan.ClearViewMarker);
        Assert.False(plan.DeleteTargetView);
        Assert.False(plan.DeleteLinkedElements);
    }

    [Fact]
    public void Delete_removes_registry_and_target_view_without_separate_element_deletion()
    {
        var plan = LinkManagementPlanner.Create(LinkManagementAction.DeleteLinkAndView, true, true);

        Assert.True(plan.CanExecute);
        Assert.True(plan.RemoveRegistryRecord);
        Assert.True(plan.DeleteTargetView);
        Assert.False(plan.DeleteLinkedElements);
    }

    [Fact]
    public void Missing_view_can_still_be_cleaned_from_registry()
    {
        var unlink = LinkManagementPlanner.Create(LinkManagementAction.UnlinkKeepView, false, true);
        var delete = LinkManagementPlanner.Create(LinkManagementAction.DeleteLinkAndView, false, true);

        Assert.True(unlink.CanExecute);
        Assert.False(unlink.ClearViewMarker);
        Assert.True(delete.CanExecute);
        Assert.False(delete.DeleteTargetView);
    }
}

