using System;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Updates;
using Xunit;

namespace TungDiroot.Core.Tests.Updates;

public sealed class UpdatePlannerTests
{
    [Theory]
    [InlineData(false, true, UpdateAction.None)]
    [InlineData(true, true, UpdateAction.DataOnly)]
    [InlineData(true, false, UpdateAction.Recreate)]
    public void Chooses_safe_update_action(bool sourceChanged, bool structureCompatible, UpdateAction expected)
    {
        var input = new UpdatePlanInput(sourceChanged, structureCompatible, UpdateBehavior.DataOnly, Model("old"), Model("new"), "STRUCTURE_CHANGED");
        Assert.Equal(expected, UpdatePlanner.Create(input).Action);
    }

    [Fact]
    public void Data_only_diff_contains_only_changed_cell_values()
    {
        var before = Model("old", "same");
        var after = Model("new", "same");
        var plan = UpdatePlanner.Create(new UpdatePlanInput(true, true, UpdateBehavior.DataOnly, before, after));
        var change = Assert.Single(plan.Changes);
        Assert.Equal("A1", change.CellKey);
        Assert.Equal("old", change.BeforeText);
        Assert.Equal("new", change.AfterText);
    }

    [Fact]
    public void Image_updates_always_recreate_linked_image_elements()
    {
        var plan = UpdatePlanner.Create(new UpdatePlanInput(true, true, UpdateBehavior.DataOnly, Model("old"), Model("new"), ImportMode: ImportMode.Image));
        Assert.Equal(UpdateAction.Recreate, plan.Action);
        Assert.Equal("IMAGE_REPLACE_REQUIRED", plan.ReasonCode);
    }

    private static WorkbookRangeModel Model(string a1, string b1 = "") => new(
        new[] { new WorkbookRowModel(1, 15, false) },
        new[] { new WorkbookColumnModel(1, 8, false), new WorkbookColumnModel(2, 8, false) },
        new[]
        {
            new CellModel(new ExcelCellAddress(1, 1), a1, null, CellStyleModel.Default, false),
            new CellModel(new ExcelCellAddress(2, 1), b1, null, CellStyleModel.Default, false)
        }, Array.Empty<ExcelRangeReference>(), Array.Empty<WorkbookReadWarning>(), new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
}
