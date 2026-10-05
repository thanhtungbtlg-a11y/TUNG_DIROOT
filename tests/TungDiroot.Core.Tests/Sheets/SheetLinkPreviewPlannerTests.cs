using System;
using TungDiroot.Core.SheetTools;
using Xunit;

namespace TungDiroot.Core.Tests.SheetTools;

public sealed class SheetLinkPreviewPlannerTests
{
    [Fact]
    public void Valid_create_sheet_and_floor_plan_are_ready()
    {
        var workbook = new SheetLinkWorkbook(
            new[] { new SheetLinkSheetRow(true, SheetLinkAction.Create, null, "A101", "Mặt bằng", "A1 Metric", "1;3") },
            new[] { new SheetLinkViewRow(true, SheetLinkAction.Create, null, "Mặt bằng tầng 1", SheetLinkViewKind.FloorPlan,
                "Level 1", "MEP", "A101", 420, 297) });

        var plan = SheetLinkPreviewPlanner.Plan(workbook, Catalog());

        Assert.Equal(2, plan.ReadyCount);
        Assert.False(plan.HasErrors);
        Assert.All(plan.Items, item => Assert.Equal(SheetLinkPreviewStatus.Ready, item.Status));
    }

    [Fact]
    public void Duplicate_sheet_number_and_view_name_are_blocked()
    {
        var workbook = new SheetLinkWorkbook(
            new[] { new SheetLinkSheetRow(true, SheetLinkAction.Create, null, "EXISTING", "Tên", "A1 Metric", "") },
            new[] { new SheetLinkViewRow(true, SheetLinkAction.Create, null, "Existing view", SheetLinkViewKind.Drafting,
                "", "", "", null, null) });

        var plan = SheetLinkPreviewPlanner.Plan(workbook, Catalog());

        Assert.True(plan.HasErrors);
        Assert.Contains(plan.Items, item => item.Code == "SHEET_NUMBER_DUPLICATE");
        Assert.Contains(plan.Items, item => item.Code == "VIEW_NAME_DUPLICATE");
    }

    [Fact]
    public void Update_requires_existing_unique_id()
    {
        var workbook = new SheetLinkWorkbook(
            new[] { new SheetLinkSheetRow(true, SheetLinkAction.Update, "missing", "A101", "Tên", "A1 Metric", "") },
            Array.Empty<SheetLinkViewRow>());

        var item = Assert.Single(SheetLinkPreviewPlanner.Plan(workbook, Catalog()).Items);

        Assert.Equal(SheetLinkPreviewStatus.Invalid, item.Status);
        Assert.Equal("SHEET_UPDATE_NOT_FOUND", item.Code);
    }

    [Fact]
    public void Plan_view_requires_level_and_paired_viewport_coordinates()
    {
        var workbook = new SheetLinkWorkbook(
            Array.Empty<SheetLinkSheetRow>(),
            new[] { new SheetLinkViewRow(true, SheetLinkAction.Create, null, "New plan", SheetLinkViewKind.CeilingPlan,
                "Missing level", "", "EXISTING", 100, null) });

        var item = Assert.Single(SheetLinkPreviewPlanner.Plan(workbook, Catalog()).Items);

        Assert.Equal(SheetLinkPreviewStatus.Invalid, item.Status);
        Assert.Contains("LEVEL_NOT_FOUND", item.Code);
        Assert.Contains("VIEWPORT_COORDINATES_INCOMPLETE", item.Code);
    }

    [Fact]
    public void Missing_revision_is_reported_before_apply()
    {
        var workbook = new SheetLinkWorkbook(
            new[] { new SheetLinkSheetRow(true, SheetLinkAction.Create, null, "A101", "Tên", "A1 Metric", "1;99") },
            Array.Empty<SheetLinkViewRow>());

        var item = Assert.Single(SheetLinkPreviewPlanner.Plan(workbook, Catalog()).Items);

        Assert.Equal("REVISION_NOT_FOUND", item.Code);
    }

    private static SheetLinkReferenceCatalog Catalog() => new(
        new[] { new SheetLinkExistingItem("sheet-1", "EXISTING") },
        new[] { new SheetLinkExistingItem("view-1", "Existing view") },
        new[] { "A1 Metric", "A3 Metric" },
        new[] { 1, 3 },
        new[] { "Level 1", "Level 2" },
        new[] { "MEP" });
}
