using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Links;
using TungDiroot.Core.Tests.Fixtures;
using Xunit;

namespace TungDiroot.Core.Tests.Excel;

public sealed class OpenXmlWorkbookRangeTests
{
    [Fact]
    public void ReadRange_preserves_display_data_and_layout()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D6"), RangeReadOptions.Default);

        Assert.Equal("DANH MỤC BẢN VẼ", model.Cell("A1").DisplayText);
        Assert.True(model.Cell("A1").Style.Font.Bold);
        Assert.Equal("A1:D1", Assert.Single(model.MergedRanges).ToA1());
        Assert.Equal(25.0, model.Rows[0].HeightPoints, 1);
        Assert.True(model.Columns[2].IsHidden);
    }

    [Fact]
    public void ReadRange_formats_shared_boolean_percent_and_cached_formula_values()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D3"), RangeReadOptions.Default);

        Assert.Equal("Shared value", model.Cell("A2").DisplayText);
        Assert.Equal("TRUE", model.Cell("B2").DisplayText);
        Assert.Equal("25.00%", model.Cell("C2").DisplayText);
        Assert.Equal("1+1", model.Cell("A3").Formula);
        Assert.True(model.Cell("A3").HasCachedFormulaValue);
        Assert.Equal("2", model.Cell("A3").DisplayText);
    }

    [Fact]
    public void ReadRange_preserves_raw_numeric_value_for_optional_company_formatting()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:C2"), RangeReadOptions.Default);

        Assert.Equal("0.25", model.Cell("C2").RawNumericValue);
        Assert.Null(model.Cell("A2").RawNumericValue);
        Assert.Null(model.Cell("B2").RawNumericValue);
    }

    [Fact]
    public void ReadRange_formats_builtin_time_minutes_without_changing_percent_display()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:F3"), RangeReadOptions.Default);

        Assert.Equal("12:00", model.Cell("E2").DisplayText);
        Assert.Equal("1/1/2024", model.Cell("F2").DisplayText);
        Assert.Equal("25.00%", model.Cell("C2").DisplayText);
    }

    [Fact]
    public void ReadRange_honors_explicit_false_and_default_true_font_properties()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("G2:H2"), RangeReadOptions.Default);

        var explicitFalse = model.Cell("G2").Style.Font;
        Assert.False(explicitFalse.Bold);
        Assert.False(explicitFalse.Italic);
        Assert.False(explicitFalse.Underline);
        var defaultTrue = model.Cell("H2").Style.Font;
        Assert.True(defaultTrue.Bold);
        Assert.True(defaultTrue.Italic);
        Assert.True(defaultTrue.Underline);
        Assert.NotEqual(FingerprintForFont(explicitFalse), FingerprintForFont(defaultTrue));
    }

    [Fact]
    public void ReadRange_clips_merged_ranges_to_the_requested_range()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("B1:C1"), RangeReadOptions.Default);

        Assert.Equal("B1:C1", Assert.Single(model.MergedRanges).ToA1());
    }

    [Fact]
    public void ReadRange_normalizes_a_directly_constructed_reversed_range_before_grid_creation()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path,
            "PCCC",
            new ExcelRangeReference(new ExcelCellAddress(4, 3), new ExcelCellAddress(1, 1)),
            RangeReadOptions.Default);

        Assert.Equal(3, model.Rows.Count);
        Assert.Equal(4, model.Columns.Count);
        Assert.Equal("DANH MỤC BẢN VẼ", model.Cell("A1").DisplayText);
    }

    [Fact]
    public void ReadRange_rejects_a_valid_but_unsafe_full_grid_before_opening_the_file()
    {
        var fullGrid = ExcelRangeReference.Parse("A1:XFD1048576");

        var exception = Assert.Throws<ArgumentException>(() => new OpenXmlWorkbookReader().ReadRange(
            "missing.xlsx", "PCCC", fullGrid, RangeReadOptions.Default));

        Assert.Equal("range", exception.ParamName);
        Assert.Contains("materialization", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadRange_rejects_more_than_250000_materialized_cells_before_opening_the_file()
    {
        var oversized = ExcelRangeReference.Parse("A1:SG500");
        var exception = Assert.Throws<ArgumentException>(() => new OpenXmlWorkbookReader().ReadRange(
            "missing.xlsx", "PCCC", oversized, RangeReadOptions.Default));
        Assert.Equal("range", exception.ParamName);
    }

    [Fact]
    public void ReadRange_warns_and_leaves_display_empty_for_formula_without_cached_value()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D3"), RangeReadOptions.Default);

        Assert.Equal(string.Empty, model.Cell("D2").DisplayText);
        Assert.False(model.Cell("D2").HasCachedFormulaValue);
        Assert.Contains(model.Warnings, warning => warning.Code == "FORMULA_CACHE_MISSING" && warning.Message.Contains("D2", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadRange_excludes_hidden_dimensions_when_requested()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D3"), new RangeReadOptions(false));

        Assert.DoesNotContain(model.Rows, row => row.Address == 3);
        Assert.DoesNotContain(model.Columns, column => column.Address == 3);
        Assert.DoesNotContain(model.Cells, cell => cell.Address.Row == 3 || cell.Address.Column == 3);
    }

    [Fact]
    public void Range_model_collections_cannot_be_mutated_through_downcasts()
    {
        using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();
        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D3"), RangeReadOptions.Default);

        Assert.False(model.Cells is IList<CellModel>);
        Assert.False(model.Rows is IList<WorkbookRowModel>);
        Assert.False(model.Columns is IList<WorkbookColumnModel>);
        Assert.False(model.MergedRanges is IList<ExcelRangeReference>);
        Assert.False(model.Warnings is IList<WorkbookReadWarning>);
    }

    [Fact]
    public void ReadRange_preserves_image_relationship_and_bytes()
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithImageDrawing();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D3"), RangeReadOptions.Default);

        var image = Assert.Single(model.Drawings.Items);
        Assert.Equal(WorkbookDrawingKind.Image, image.Kind);
        Assert.Equal("A1:B2", image.Anchor);
        Assert.NotNull(image.RelationshipId);
        Assert.NotEmpty(image.ImageBytes.ToArray());
        Assert.False(model.Drawings.Items is IList<WorkbookDrawingItem>);
    }

    [Fact]
    public void Image_payload_mutation_cannot_change_future_reads()
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithImageDrawing();
        var image = Assert.Single(new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D3"), RangeReadOptions.Default).Drawings.Items);
        var expected = image.ImageBytes.ToArray();

        Assert.True(MemoryMarshal.TryGetArray(image.ImageBytes, out ArraySegment<byte> exposed));
        exposed.Array![exposed.Offset] ^= 0xFF;

        Assert.Equal(expected, image.ImageBytes.ToArray());
    }

    [Fact]
    public void ReadRange_keeps_shape_preview_and_warns_when_shape_preview_is_missing()
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithShapeDrawings();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D6"), RangeReadOptions.Default);
        var shapes = model.Drawings.Items.Where(item => item.Kind == WorkbookDrawingKind.Shape).OrderBy(item => item.Anchor).ToArray();

        Assert.Equal(2, shapes.Length);
        Assert.Equal("A4:B5", shapes[0].Anchor);
        var preview = Assert.IsType<WorkbookDrawingPreview>(shapes[0].CachedPreview);
        Assert.NotNull(preview.RelationshipId);
        Assert.NotEmpty(preview.Bytes.ToArray());
        var expectedPreview = preview.Bytes.ToArray();
        Assert.True(MemoryMarshal.TryGetArray(preview.Bytes, out ArraySegment<byte> exposedPreview));
        exposedPreview.Array![exposedPreview.Offset] ^= 0xFF;
        Assert.Equal(expectedPreview, preview.Bytes.ToArray());
        Assert.Equal("C4:D5", shapes[1].Anchor);
        Assert.Null(shapes[1].CachedPreview);
        Assert.Contains(model.Warnings, warning => warning.Code == "DRAWING_PREVIEW_MISSING" && warning.Message.Contains("C4:D5", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadRange_keeps_a_genuine_one_cell_anchor_and_fingerprint_includes_it()
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithOneCellImageDrawing("0", "0");
        var range = ExcelRangeReference.Parse("A1:D3");
        var model = new OpenXmlWorkbookReader().ReadRange(fixture.Path, "PCCC", range, RangeReadOptions.Default);

        Assert.Equal("A1:A1", Assert.Single(model.Drawings.Items).Anchor);
        Assert.NotEqual(FingerprintWithoutDrawings(model, range), SourceFingerprint.Create(1, 1, "PCCC", range, model));
    }

    [Fact]
    public void ReadRange_marks_a_malformed_two_cell_anchor_unknown_and_fingerprint_excludes_it()
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithMalformedTwoCellImageDrawing();
        var range = ExcelRangeReference.Parse("A1:D3");
        var model = new OpenXmlWorkbookReader().ReadRange(fixture.Path, "PCCC", range, RangeReadOptions.Default);

        Assert.Equal("twoCellAnchor", Assert.Single(model.Drawings.Items).Anchor);
        Assert.Equal(FingerprintWithoutDrawings(model, range), SourceFingerprint.Create(1, 1, "PCCC", range, model));
    }

    [Theory]
    [InlineData("2147483647", "0")]
    [InlineData("0", "2147483647")]
    public void ReadRange_marks_overflowing_one_cell_anchor_coordinates_unknown(string zeroBasedColumn, string zeroBasedRow)
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithOneCellImageDrawing(zeroBasedColumn, zeroBasedRow);
        var range = ExcelRangeReference.Parse("A1:D3");
        var model = new OpenXmlWorkbookReader().ReadRange(fixture.Path, "PCCC", range, RangeReadOptions.Default);

        Assert.Equal("oneCellAnchor", Assert.Single(model.Drawings.Items).Anchor);
        Assert.Equal(FingerprintWithoutDrawings(model, range), SourceFingerprint.Create(1, 1, "PCCC", range, model));
    }

    [Theory]
    [InlineData("-1", "0")]
    [InlineData("0", "-1")]
    [InlineData("16384", "0")]
    [InlineData("0", "1048576")]
    public void ReadRange_excludes_negative_and_immediately_out_of_bounds_one_cell_anchor_coordinates(string zeroBasedColumn, string zeroBasedRow)
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithOneCellImageDrawing(zeroBasedColumn, zeroBasedRow);
        var range = ExcelRangeReference.Parse("A1:D3");
        var model = new OpenXmlWorkbookReader().ReadRange(fixture.Path, "PCCC", range, RangeReadOptions.Default);

        var drawing = Assert.Single(model.Drawings.Items);
        Assert.Equal("oneCellAnchor", drawing.Anchor);
        Assert.NotNull(drawing.RelationshipId);
        Assert.Equal(FingerprintWithoutDrawings(model, range), SourceFingerprint.Create(1, 1, "PCCC", range, model));
    }

    [Fact]
    public void ReadRange_accepts_the_final_excel_cell_as_a_one_cell_anchor()
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithOneCellImageDrawing("16383", "1048575");
        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:A1"), RangeReadOptions.Default);

        Assert.Equal("XFD1048576:XFD1048576", Assert.Single(model.Drawings.Items).Anchor);
    }

    [Fact]
    public void Cell_lookup_enforces_the_excel_grid_boundary()
    {
        var finalCell = new CellModel(new ExcelCellAddress(16384, 1048576), "final", null, CellStyleModel.Default, false);
        var model = new WorkbookRangeModel(
            Array.Empty<WorkbookRowModel>(),
            Array.Empty<WorkbookColumnModel>(),
            new[] { finalCell },
            Array.Empty<ExcelRangeReference>(),
            Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));

        Assert.Equal("final", model.Cell("XFD1048576").DisplayText);
        Assert.Throws<ArgumentException>(() => model.Cell("XFE1"));
    }

    [Fact]
    public void ReadRange_ignores_drawing_extension_list_metadata()
    {
        using var fixture = TestWorkbookFactory.CreateWorkbookWithDrawingExtensionList();

        var model = new OpenXmlWorkbookReader().ReadRange(
            fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D3"), RangeReadOptions.Default);

        Assert.Single(model.Drawings.Items);
        Assert.DoesNotContain(model.Drawings.Items, item => item.Kind == WorkbookDrawingKind.Other);
        Assert.DoesNotContain(model.Warnings, warning => warning.Code == "DRAWING_PREVIEW_MISSING");
    }

    private static SourceFingerprint FingerprintWithoutDrawings(WorkbookRangeModel model, ExcelRangeReference range)
    {
        var withoutDrawings = new WorkbookRangeModel(
            model.Rows,
            model.Columns,
            model.Cells,
            model.MergedRanges,
            model.Warnings,
            new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
        return SourceFingerprint.Create(1, 1, "PCCC", range, withoutDrawings);
    }

    private static SourceFingerprint FingerprintForFont(FontModel font)
    {
        var style = CellStyleModel.Default with { Font = font };
        var model = new WorkbookRangeModel(
            Array.Empty<WorkbookRowModel>(),
            Array.Empty<WorkbookColumnModel>(),
            new[] { new CellModel(new ExcelCellAddress(1, 1), "font", null, style, false) },
            Array.Empty<ExcelRangeReference>(),
            Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
        return SourceFingerprint.Create(1, 1, "PCCC", ExcelRangeReference.Parse("A1:A1"), model);
    }
}
