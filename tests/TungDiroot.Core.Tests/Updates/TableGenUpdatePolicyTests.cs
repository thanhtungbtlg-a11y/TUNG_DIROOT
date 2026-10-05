using System;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Links;
using TungDiroot.Core.Updates;
using Xunit;

namespace TungDiroot.Core.Tests.Updates;

public sealed class TableGenUpdatePolicyTests
{
    [Fact]
    public void Data_only_structure_change_requires_explicit_rebuild()
    {
        var input = Input(structureCompatible: false);

        var exception = Assert.Throws<InvalidOperationException>(() => TableGenUpdatePolicy.CreatePlan(input));

        Assert.StartsWith("DATA_ONLY_STRUCTURE_CHANGED:", exception.Message);
        Assert.Contains("Dựng lại bảng", exception.Message);
        Assert.Contains("Recreate", exception.Message);
    }

    [Fact]
    public void Compatible_data_only_update_keeps_text_only_changes()
    {
        var plan = TableGenUpdatePolicy.CreatePlan(Input());

        Assert.Equal(UpdateAction.DataOnly, plan.Action);
        Assert.Equal(new CellValueChange("A1", "old", "new"), Assert.Single(plan.Changes));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explicit_rebuild_overrides_data_only_and_structure_check_without_changing_saved_behavior(bool compatible)
    {
        var input = Input(sourceChanged: false, structureCompatible: compatible);

        var plan = TableGenUpdatePolicy.CreatePlan(input, forceRecreate: true);

        Assert.Equal(UpdateAction.Recreate, plan.Action);
        Assert.Empty(plan.Changes);
        Assert.Same(input.After, plan.After);
        Assert.Equal(UpdateBehavior.DataOnly, input.RequestedBehavior);
    }

    [Theory]
    [InlineData(SourceChange.Unchanged, false, true)]
    [InlineData(SourceChange.Unchanged, true, false)]
    [InlineData(SourceChange.Changed, false, false)]
    [InlineData(SourceChange.Changed, true, false)]
    public void Explicit_rebuild_bypasses_unchanged_skip(SourceChange change, bool forceRecreate, bool expectedSkip) =>
        Assert.Equal(expectedSkip, TableGenUpdatePolicy.ShouldSkip(change, forceRecreate));

    [Fact]
    public void Image_replacement_is_not_blocked_by_data_only_structure_guard()
    {
        var plan = TableGenUpdatePolicy.CreatePlan(Input(structureCompatible: false) with { ImportMode = ImportMode.Image });

        Assert.Equal(UpdateAction.Recreate, plan.Action);
    }

    [Fact]
    public void Saved_recreate_behavior_still_allows_structure_changes()
    {
        var plan = TableGenUpdatePolicy.CreatePlan(Input(structureCompatible: false) with { RequestedBehavior = UpdateBehavior.Recreate });

        Assert.Equal(UpdateAction.Recreate, plan.Action);
    }

    [Fact]
    public void Failed_transaction_start_cannot_proceed_to_table_writes() =>
        Assert.Throws<InvalidOperationException>(() => TableGenUpdatePolicy.RequireTransactionStarted(false));

    [Fact]
    public void Rolled_back_or_pending_commit_cannot_report_table_rebuild_success() =>
        Assert.Throws<InvalidOperationException>(() => TableGenUpdatePolicy.RequireTransactionCommitted(false));

    [Fact]
    public void Confirmed_transaction_outcomes_allow_success()
    {
        TableGenUpdatePolicy.RequireTransactionStarted(true);
        TableGenUpdatePolicy.RequireTransactionCommitted(true);
    }

    private static UpdatePlanInput Input(bool sourceChanged = true, bool structureCompatible = true) =>
        new(sourceChanged, structureCompatible, UpdateBehavior.DataOnly, Model("old"), Model("new"), "STRUCTURE_CHANGED");

    private static WorkbookRangeModel Model(string text) => new(
        new[] { new WorkbookRowModel(1, 15, false) },
        new[] { new WorkbookColumnModel(1, 8, false) },
        new[] { new CellModel(new ExcelCellAddress(1, 1), text, null, CellStyleModel.Default, false) },
        Array.Empty<ExcelRangeReference>(), Array.Empty<WorkbookReadWarning>(), new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
}
