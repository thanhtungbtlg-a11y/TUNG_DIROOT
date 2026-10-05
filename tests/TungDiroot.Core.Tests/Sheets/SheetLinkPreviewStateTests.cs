using System;
using TungDiroot.Core.SheetTools;
using Xunit;

namespace TungDiroot.Core.Tests.SheetTools;

public sealed class SheetLinkPreviewStateTests
{
    [Fact]
    public void Changing_workbook_path_invalidates_the_previously_reviewed_batch()
    {
        var state = ReviewedState();
        Assert.True(state.CanApply);
        state.ChangePath("B.xlsx");
        Assert.False(state.CanApply);
        Assert.Null(state.Workbook);
        Assert.Null(state.Plan);
    }

    [Fact]
    public void Starting_preview_or_export_clears_old_apply_state_and_locks_inputs()
    {
        var state = ReviewedState();
        state.BeginOperation("A.xlsx");
        Assert.False(state.CanApply);
        Assert.False(state.CanEditInputs);
        Assert.Null(state.Plan);
    }

    [Fact]
    public void Failed_load_cannot_reenable_apply_for_the_previous_workbook()
    {
        var state = ReviewedState();
        state.BeginOperation("B.xlsx");
        state.Invalidate();
        state.SetBusy(false);
        Assert.False(state.CanApply);
        Assert.True(state.CanEditInputs);
    }

    [Fact]
    public void Old_async_result_cannot_replace_a_newer_successful_preview_even_at_the_same_path()
    {
        var state = new SheetLinkPreviewState();
        var older = state.BeginOperation("A.xlsx");
        var current = state.BeginOperation("A.xlsx");
        Assert.True(state.CompletePreview("A.xlsx", current, Workbook("Current"), Plan("Current")));
        Assert.False(state.CompletePreview("A.xlsx", older, Workbook("Old"), Plan("Old")));
        Assert.Equal("Current", Assert.Single(state.Workbook!.Sheets).SheetName);
        Assert.True(state.CanApply);
    }

    [Fact]
    public void Async_result_for_a_different_selected_path_is_rejected()
    {
        var state = new SheetLinkPreviewState();
        var version = state.BeginOperation("A.xlsx");
        state.ChangePath("B.xlsx");
        Assert.False(state.CompletePreview("A.xlsx", version, Workbook("Wrong"), Plan("Wrong")));
        Assert.Null(state.Plan);
    }

    private static SheetLinkPreviewState ReviewedState()
    {
        var state = new SheetLinkPreviewState();
        var version = state.BeginOperation("A.xlsx");
        Assert.True(state.CompletePreview("A.xlsx", version, Workbook("A"), Plan("A")));
        return state;
    }
    private static SheetLinkWorkbook Workbook(string name) => new(
        new[] { new SheetLinkSheetRow(true, SheetLinkAction.Create, null, "A101", name, "A1", "") }, Array.Empty<SheetLinkViewRow>());
    private static SheetLinkPreviewPlan Plan(string key) => new(new[]
    {
        new SheetLinkPreviewItem(SheetLinkRowKind.Sheet, 2, SheetLinkAction.Create, key, SheetLinkPreviewStatus.Ready, "READY", "CREATE " + key)
    });
}
