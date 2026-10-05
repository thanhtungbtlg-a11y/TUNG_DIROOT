using TungDiroot.Core.Import;
using TungDiroot.Core.RevitPlanning;
using Xunit;

namespace TungDiroot.Core.Tests.RevitPlanning;

public sealed class ViewCreationPlannerTests
{
    [Theory]
    [InlineData(ImportMode.Image, TargetViewKind.Schedule, false, "IMAGE_SCHEDULE_UNSUPPORTED")]
    [InlineData(ImportMode.RevitTable, TargetViewKind.Legend, false, "LEGEND_SEED_REQUIRED")]
    [InlineData(ImportMode.RevitTable, TargetViewKind.Drafting, true, null)]
    public void Validate_view_request(ImportMode mode, TargetViewKind kind, bool hasLegendSeed, string? expectedError)
    {
        var plan = ViewCreationPlanner.Plan(new ViewCreationRequest(mode, kind, hasLegendSeed, "DM PCCC"));

        Assert.Equal(expectedError, plan.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_view_name_is_rejected(string name)
    {
        Assert.Equal("VIEW_NAME_REQUIRED", ViewCreationPlanner.Plan(
            new ViewCreationRequest(ImportMode.RevitTable, TargetViewKind.Drafting, false, name)).ErrorCode);
    }
}
