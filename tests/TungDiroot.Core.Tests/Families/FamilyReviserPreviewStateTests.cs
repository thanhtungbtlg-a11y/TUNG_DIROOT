using TungDiroot.Core.Families;
using Xunit;

namespace TungDiroot.Core.Tests.Families;

public sealed class FamilyReviserPreviewStateTests
{
    [Theory]
    [InlineData("B.xlsx", "^TUNG_")]
    [InlineData("A.xlsx", "^OTHER_")]
    public void Changing_workbook_or_pattern_invalidates_the_ready_preview(string path, string pattern)
    {
        var state = ReadyState();

        state.ChangeInputs(path, pattern);

        Assert.False(state.CanApply);
        Assert.Null(state.Plan);
    }

    [Fact]
    public void Unchanged_inputs_keep_the_confirmed_preview()
    {
        var state = ReadyState();

        state.ChangeInputs(" A.xlsx ", " ^TUNG_ ");

        Assert.True(state.CanApply);
        Assert.Equal(1, state.Plan!.ReadyCount);
    }

    [Fact]
    public void Busy_operation_disables_source_edits_and_apply_without_losing_its_snapshot()
    {
        var state = ReadyState();

        state.SetBusy(true);

        Assert.False(state.CanEditInputs);
        Assert.False(state.CanApply);
        Assert.NotNull(state.Plan);
        state.SetBusy(false);
        Assert.True(state.CanEditInputs);
        Assert.True(state.CanApply);
    }

    [Fact]
    public void Failed_or_restarted_preview_cannot_resurrect_an_old_ready_plan()
    {
        var state = ReadyState();

        state.Invalidate();
        state.SetBusy(true);
        state.SetBusy(false);

        Assert.False(state.CanApply);
        Assert.Null(state.Plan);
    }

    [Fact]
    public void Late_preview_for_previous_inputs_is_not_accepted()
    {
        var state = ReadyState();
        state.ChangeInputs("B.xlsx", "^TUNG_");

        var accepted = state.SetPreview("A.xlsx", "^TUNG_", ReadyPlan());

        Assert.False(accepted);
        Assert.False(state.CanApply);
        Assert.Null(state.Plan);
    }

    private static FamilyReviserPreviewState ReadyState()
    {
        var state = new FamilyReviserPreviewState();
        state.ChangeInputs("A.xlsx", "^TUNG_");
        Assert.True(state.SetPreview("A.xlsx", "^TUNG_", ReadyPlan()));
        Assert.True(state.CanApply);
        return state;
    }

    private static FamilyReviserPlan ReadyPlan()
    {
        var row = new FamilyReviserRow(true, FamilyReviserAction.RenameFamily, "family-a", "Generic Models",
            "Family A", "TUNG_A", "", "", false, false, true);
        return new FamilyReviserPlan(new[]
        {
            new FamilyReviserPreviewItem(7, row, row, FamilyReviserPreviewStatus.Ready, "READY", "Ready")
        });
    }
}
