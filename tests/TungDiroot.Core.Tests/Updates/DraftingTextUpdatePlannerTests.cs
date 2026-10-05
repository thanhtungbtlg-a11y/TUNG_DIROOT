using System;
using System.Collections.Generic;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Updates;
using Xunit;

namespace TungDiroot.Core.Tests.Updates;

public sealed class DraftingTextUpdatePlannerTests
{
    [Fact]
    public void Blank_cells_do_not_force_recreation_of_an_existing_text_scope()
    {
        var after = Model("new", "", "same");

        var plan = DraftingTextUpdatePlanner.Create(after, Notes(("A1", "old"), ("C1", "same")));

        Assert.Equal(UpdateAction.DataOnly, plan.Action);
        var change = Assert.Single(plan.Changes);
        Assert.Equal(new CellValueChange("A1", "old", "new"), change);
    }

    [Fact]
    public void Merged_child_cells_are_not_part_of_the_rendered_text_scope()
    {
        var after = Model("new heading", "ignored child value", "same", merge: true);

        var plan = DraftingTextUpdatePlanner.Create(after, Notes(("A1", "old heading"), ("C1", "same")));

        Assert.Equal(UpdateAction.DataOnly, plan.Action);
        var change = Assert.Single(plan.Changes);
        Assert.Equal(new CellValueChange("A1", "old heading", "new heading"), change);
    }

    [Fact]
    public void Completely_blank_table_keeps_existing_geometry_with_no_text_changes()
    {
        var plan = DraftingTextUpdatePlanner.Create(Model("", "", ""), Notes());

        Assert.Equal(UpdateAction.DataOnly, plan.Action);
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public void Cells_outside_visible_layout_axes_do_not_require_text_notes()
    {
        var after = new WorkbookRangeModel(
            new[] { new WorkbookRowModel(1, 15, false) },
            new[] { new WorkbookColumnModel(1, 8, false) },
            new[] { Cell("A1", "new"), Cell("A2", "excluded") },
            Array.Empty<ExcelRangeReference>(), Array.Empty<WorkbookReadWarning>(), new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));

        var plan = DraftingTextUpdatePlanner.Create(after, Notes(("A1", "old")));

        Assert.Equal(UpdateAction.DataOnly, plan.Action);
        Assert.Equal(new CellValueChange("A1", "old", "new"), Assert.Single(plan.Changes));
    }

    [Fact]
    public void Newly_nonempty_cell_requires_explicit_recreate_instead_of_silent_fallback()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DraftingTextUpdatePlanner.Create(Model("same", "new", ""), Notes(("A1", "same"))));

        Assert.Contains("DATA_ONLY_TEXT_SCOPE_CHANGED", exception.Message);
        Assert.Contains("Recreate", exception.Message);
    }

    [Fact]
    public void Cleared_text_requires_explicit_recreate_instead_of_silent_fallback()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DraftingTextUpdatePlanner.Create(Model("same", "", ""), Notes(("A1", "same"), ("B1", "old"))));

        Assert.Contains("DATA_ONLY_TEXT_SCOPE_CHANGED", exception.Message);
        Assert.Contains("Recreate", exception.Message);
    }

    [Fact]
    public void Missing_note_is_rejected_even_when_the_note_count_still_matches()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DraftingTextUpdatePlanner.Create(Model("new", "", ""), Notes(("Z99", "orphan"))));

        Assert.Contains("DATA_ONLY_TEXT_SCOPE_CHANGED", exception.Message);
    }

    [Fact]
    public void Duplicate_text_tags_are_rejected_with_an_actionable_domain_error()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DraftingTextUpdatePlanner.Create(Model("new", "", ""), Notes(("A1", "old"), ("a1", "duplicate"))));

        Assert.Contains("DATA_ONLY_TEXT_SCOPE_CHANGED", exception.Message);
        Assert.Contains("Recreate", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cell")]
    public void Malformed_text_tag_is_rejected_with_an_actionable_domain_error(string key)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DraftingTextUpdatePlanner.Create(Model("new", "", ""), Notes((key, "old"))));

        Assert.Contains("DATA_ONLY_TEXT_SCOPE_CHANGED", exception.Message);
    }

    [Fact]
    public void Cell_keys_match_without_case_sensitivity()
    {
        var plan = DraftingTextUpdatePlanner.Create(Model("new", "", ""), Notes(("a1", "old")));

        Assert.Equal(UpdateAction.DataOnly, plan.Action);
        Assert.Equal(new CellValueChange("A1", "old", "new"), Assert.Single(plan.Changes));
    }

    private static KeyValuePair<string, string>[] Notes(params (string Key, string Text)[] values) =>
        Array.ConvertAll(values, item => new KeyValuePair<string, string>(item.Key, item.Text));

    private static WorkbookRangeModel Model(string a1, string b1, string c1, bool merge = false) => new(
        new[] { new WorkbookRowModel(1, 15, false) },
        new[] { new WorkbookColumnModel(1, 8, false), new WorkbookColumnModel(2, 8, false), new WorkbookColumnModel(3, 8, false) },
        new[] { Cell("A1", a1), Cell("B1", b1), Cell("C1", c1) },
        merge ? new[] { ExcelRangeReference.Parse("A1:B1") } : Array.Empty<ExcelRangeReference>(),
        Array.Empty<WorkbookReadWarning>(), new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));

    private static CellModel Cell(string address, string text) =>
        new(ExcelCellAddress.Parse(address), text, null, CellStyleModel.Default, false);
}
