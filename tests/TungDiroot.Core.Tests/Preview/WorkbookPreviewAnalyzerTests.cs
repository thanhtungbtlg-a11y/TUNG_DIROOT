using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Preview;
using Xunit;

namespace TungDiroot.Core.Tests.Preview;

public sealed class WorkbookPreviewAnalyzerTests
{
    [Fact]
    public void Clean_range_reports_dimensions_and_no_issues()
    {
        var report = WorkbookPreviewAnalyzer.Analyze(Model(2, 2), ImportMode.RevitTable);

        Assert.Equal(2, report.RowCount);
        Assert.Equal(2, report.ColumnCount);
        Assert.Equal(4, report.CellCount);
        Assert.Empty(report.Issues);
        Assert.True(report.CanImport);
        Assert.Equal(4, report.Estimate.TextNotes);
        Assert.True(report.Estimate.TotalElements >= 4);
    }

    [Theory]
    [InlineData("#REF!", "FORMULA_REF_ERROR")]
    [InlineData("#VALUE!", "FORMULA_VALUE_ERROR")]
    [InlineData("#DIV/0!", "FORMULA_DIV_ZERO")]
    public void Formula_error_text_is_reported_with_cell_address(string displayed, string expectedCode)
    {
        var model = Model(1, 1, cellText: _ => displayed, formula: _ => "=A1");

        var issue = Assert.Single(WorkbookPreviewAnalyzer.Analyze(model, ImportMode.RevitTable).Issues);

        Assert.Equal(expectedCode, issue.Code);
        Assert.Equal(PreviewIssueSeverity.Error, issue.Severity);
        Assert.Equal("A1", issue.CellAddress);
    }

    [Fact]
    public void Missing_formula_cache_warning_is_preserved()
    {
        var warning = new WorkbookReadWarning("FORMULA_CACHE_MISSING", "Formula cell B2 has no cached value.");

        var issue = Assert.Single(WorkbookPreviewAnalyzer.Analyze(
            Model(1, 1, warnings: new[] { warning }), ImportMode.RevitTable).Issues);

        Assert.Equal("FORMULA_CACHE_MISSING", issue.Code);
        Assert.Equal(PreviewIssueSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Unsupported_drawing_without_preview_is_reported()
    {
        var drawing = new WorkbookDrawingItem(WorkbookDrawingKind.Chart, "C3:F12", "rId7", null, null);

        var issue = Assert.Single(WorkbookPreviewAnalyzer.Analyze(
            Model(1, 1, drawings: new[] { drawing }), ImportMode.RevitTable).Issues);

        Assert.Equal("DRAWING_PREVIEW_MISSING", issue.Code);
        Assert.Contains("C3:F12", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Revit_table_over_five_thousand_cells_is_warned_but_image_is_not()
    {
        var model = Model(51, 99);

        var table = WorkbookPreviewAnalyzer.Analyze(model, ImportMode.RevitTable);
        var image = WorkbookPreviewAnalyzer.Analyze(model, ImportMode.Image);

        Assert.Contains(table.Issues, issue => issue.Code == "LARGE_REVIT_TABLE");
        Assert.DoesNotContain(image.Issues, issue => issue.Code == "LARGE_REVIT_TABLE");
    }

    [Fact]
    public void Merged_range_is_identified_and_counts_only_visible_text()
    {
        var merge = ExcelRangeReference.Parse("A1:C1");
        var model = Model(1, 3, cellText: address => address.Column == 1 ? "HEADER" : string.Empty,
            mergedRanges: new[] { merge });

        var report = WorkbookPreviewAnalyzer.Analyze(model, ImportMode.RevitTable);

        Assert.Contains(report.Issues, issue => issue.Code == "MERGED_CELLS");
        Assert.Equal(1, report.MergedRangeCount);
        Assert.Equal(1, report.Estimate.TextNotes);
    }

    private static WorkbookRangeModel Model(
        int rows,
        int columns,
        Func<ExcelCellAddress, string>? cellText = null,
        Func<ExcelCellAddress, string?>? formula = null,
        IEnumerable<WorkbookReadWarning>? warnings = null,
        IEnumerable<WorkbookDrawingItem>? drawings = null,
        IEnumerable<ExcelRangeReference>? mergedRanges = null)
    {
        var cells = from row in Enumerable.Range(1, rows)
                    from column in Enumerable.Range(1, columns)
                    let address = new ExcelCellAddress(column, row)
                    select new CellModel(
                        address,
                        cellText?.Invoke(address) ?? "X",
                        formula?.Invoke(address),
                        CellStyleModel.Default,
                        formula == null);
        return new WorkbookRangeModel(
            Enumerable.Range(1, rows).Select(row => new WorkbookRowModel(row, 15D, false)),
            Enumerable.Range(1, columns).Select(column => new WorkbookColumnModel(column, 8.43D, false)),
            cells,
            mergedRanges ?? Array.Empty<ExcelRangeReference>(),
            warnings ?? Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(drawings ?? Array.Empty<WorkbookDrawingItem>()));
    }
}
