using System;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;
using Xunit;

namespace TungDiroot.Core.Tests.Layout;

public sealed class StyleMappingCandidateExtractorTests
{
    [Fact]
    public void Extracts_unique_cell_and_border_styles_with_deterministic_keys()
    {
        var bordered = CellStyleModel.Default with
        {
            Font = CellStyleModel.Default.Font with { Name = "Arial", Bold = true },
            Border = CellStyleModel.Default.Border with
            {
                Left = new BorderSideModel("thin", "FF112233"),
                Right = new BorderSideModel("thin", "FF112233"),
                Bottom = new BorderSideModel("double", "FF000000")
            }
        };
        var model = Model(
            new CellModel(new ExcelCellAddress(1, 1), "A", null, bordered, false),
            new CellModel(new ExcelCellAddress(1, 2), "B", null, bordered, false),
            new CellModel(new ExcelCellAddress(2, 1), "C", null, CellStyleModel.Default, false));

        var candidates = StyleMappingCandidateExtractor.Extract(model);

        Assert.Equal(2, candidates.Count(item => item.Kind == TableGenStyleCandidateKind.Text));
        Assert.Equal(2, candidates.Count(item => item.Kind == TableGenStyleCandidateKind.Line));
        Assert.Contains(candidates, item => item.Key == CellStyleKey.Create(bordered));
        Assert.Contains(candidates, item => item.Key == LineStyleKey.Create("thin", "FF112233"));
        Assert.Contains(candidates, item => item.Key == LineStyleKey.Create("double", "FF000000"));
    }

    [Fact]
    public void Empty_or_none_borders_do_not_create_line_candidates()
    {
        var candidates = StyleMappingCandidateExtractor.Extract(Model(
            new CellModel(new ExcelCellAddress(1, 1), string.Empty, null, CellStyleModel.Default, false)));

        Assert.Single(candidates);
        Assert.Equal(TableGenStyleCandidateKind.Text, candidates[0].Kind);
    }

    private static WorkbookRangeModel Model(params CellModel[] cells) => new(
        new[] { new WorkbookRowModel(1, 15, false), new WorkbookRowModel(2, 15, false) },
        new[] { new WorkbookColumnModel(1, 8, false), new WorkbookColumnModel(2, 8, false) },
        cells,
        Array.Empty<ExcelRangeReference>(),
        Array.Empty<WorkbookReadWarning>(),
        new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
}
