using System;
using System.Linq;
using System.Collections.Generic;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;
using Xunit;

namespace TungDiroot.Core.Tests.Layout;

public sealed class TableLayoutPlannerTests
{
    [Fact]
    public void Merged_header_creates_one_text_and_outer_border()
    {
        var model = MergedHeader();

        var plan = TableLayoutPlanner.Create(model, LayoutOptions.Default);

        Assert.Single(plan.Texts);
        Assert.Equal("DANH MỤC", plan.Texts[0].Text);
        Assert.Equal(4, plan.Borders.Count(border => border.Role == BorderRole.Outer));
        Assert.Equal(112D, plan.WidthPoints, 3);
    }

    [Fact]
    public void Black_and_white_changes_style_without_mutating_source()
    {
        var source = MergedHeader();

        var plan = TableLayoutPlanner.Create(source, new LayoutOptions(true, false));

        Assert.Equal("FF000000", plan.Texts[0].Style.Font.Color);
        Assert.Equal("FF112233", source.Cells[0].Style.Font.Color);
    }

    [Fact]
    public void Black_and_white_retains_original_company_mapping_for_text_and_borders()
    {
        var source = MergedHeader();
        var profile = new StyleMappingProfile(new Dictionary<string, RevitStyleMapping>
        {
            [CellStyleKey.Create(source.Cells[0].Style)] = new("company-text", null),
            [LineStyleKey.Create("thin", "FF112233")] = new(null, "company-line")
        });
        var plan = TableLayoutPlanner.Create(source, new LayoutOptions(true, false));

        Assert.Equal("FF000000", plan.Texts[0].Style.Font.Color);
        Assert.Equal("company-text", profile.ResolveText(plan.Texts[0])?.TextNoteTypeUniqueId);
        Assert.All(plan.Borders, line => Assert.Equal("company-line", profile.ResolveLine(line)?.GraphicsStyleUniqueId));
    }

    [Fact]
    public void Black_and_white_does_not_coalesce_borders_with_different_original_mapping_keys()
    {
        var result = BorderSegmentCoalescer.Coalesce(new[]
        {
            new LinePlacement(0, 0, 10, 0, "thin", "FF000000", BorderRole.Inner, "red-mapping"),
            new LinePlacement(10, 0, 20, 0, "thin", "FF000000", BorderRole.Inner, "blue-mapping")
        });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, line => line.MappingKey == "red-mapping");
        Assert.Contains(result, line => line.MappingKey == "blue-mapping");
    }

    private static WorkbookRangeModel MergedHeader()
    {
        var thin = new BorderSideModel("thin", "FF112233");
        var style = CellStyleModel.Default with
        {
            Font = CellStyleModel.Default.Font with { Color = "FF112233" },
            Border = new BorderModel(thin, thin, thin, thin)
        };
        var cells = Enumerable.Range(1, 4).Select(column => new CellModel(
            new ExcelCellAddress(column, 1), column == 1 ? "DANH MỤC" : string.Empty, null, style, false));
        return new WorkbookRangeModel(
            new[] { new WorkbookRowModel(1, 20D, false) },
            Enumerable.Range(1, 4).Select(column => new WorkbookColumnModel(column, 4D, false)),
            cells,
            new[] { ExcelRangeReference.Parse("A1:D1") },
            Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
    }
}
