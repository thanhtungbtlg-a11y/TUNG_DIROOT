using System;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Updates;
using Xunit;

namespace TungDiroot.Core.Tests.Updates;

public sealed class StructureCompatibilityTests
{
    [Fact]
    public void Same_rows_columns_and_merges_support_data_only()
    {
        var before = new StructureSignature(10, 4, new[] { "A1:D1" });
        var after = new StructureSignature(10, 4, new[] { "A1:D1" });

        Assert.True(StructureCompatibility.Compare(before, after).CanUpdateDataOnly);
    }

    [Fact]
    public void Changed_merge_requires_recreate()
    {
        var result = StructureCompatibility.Compare(
            new StructureSignature(10, 4, new[] { "A1:D1" }),
            new StructureSignature(10, 4, new[] { "A1:C1" }));

        Assert.Equal("MERGE_STRUCTURE_CHANGED", result.ReasonCode);
    }

    [Fact]
    public void Structure_stamp_is_stable_and_changes_with_merge_layout()
    {
        var first = Model("A1:B1");
        var equivalent = Model("A1:B1");
        var changed = Model("A1:A2");
        Assert.Equal(StructureStamp.Create(first), StructureStamp.Create(equivalent));
        Assert.NotEqual(StructureStamp.Create(first), StructureStamp.Create(changed));
    }

    [Fact]
    public void Structure_stamp_changes_when_visible_addresses_shift_with_same_counts()
    {
        var original = Model("A1:B1");
        var shifted = new WorkbookRangeModel(
            new[] { new WorkbookRowModel(2, 15, false), new WorkbookRowModel(3, 15, false) },
            new[] { new WorkbookColumnModel(2, 8, false), new WorkbookColumnModel(3, 8, false) },
            new[] { new CellModel(new ExcelCellAddress(2, 2), "A", null, CellStyleModel.Default, false) },
            new[] { ExcelRangeReference.Parse("B2:C2") }, Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));

        Assert.NotEqual(StructureStamp.Create(original), StructureStamp.Create(shifted));
    }

    private static WorkbookRangeModel Model(string merge) => new(
        new[] { new WorkbookRowModel(1, 15, false), new WorkbookRowModel(2, 15, false) },
        new[] { new WorkbookColumnModel(1, 8, false), new WorkbookColumnModel(2, 8, false) },
        new[] { new CellModel(new ExcelCellAddress(1, 1), "A", null, CellStyleModel.Default, false) },
        new[] { ExcelRangeReference.Parse(merge) }, Array.Empty<WorkbookReadWarning>(),
        new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
}
