using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;
using Xunit;
using System;

namespace TungDiroot.Core.Tests.Excel;

public sealed class NumericDisplayFormatterTests
{
    [Fact]
    public void Keep_excel_format_returns_existing_display_text()
    {
        var cell = NumericCell("1234.5", "1,234.50");

        var result = NumericDisplayFormatter.Format(cell, NumericDisplayOptions.ExcelDefault);

        Assert.Equal("1,234.50", result);
    }

    [Fact]
    public void Custom_symbols_and_precision_are_applied_to_raw_numeric_value()
    {
        var options = new NumericDisplayOptions(false, ",", true, ".", 2);

        var result = NumericDisplayFormatter.Format(NumericCell("1234.5", "1234.5"), options);

        Assert.Equal("1.234,50", result);
    }

    [Fact]
    public void Grouping_can_be_disabled_without_changing_decimal_precision()
    {
        var options = new NumericDisplayOptions(false, ".", false, ",", 3);

        var result = NumericDisplayFormatter.Format(NumericCell("1234.5", "1234.5"), options);

        Assert.Equal("1234.500", result);
    }

    [Theory]
    [InlineData("PUMP-01")]
    [InlineData("#REF!")]
    [InlineData("#DIV/0!")]
    public void Nonnumeric_or_formula_error_text_is_never_reformatted(string displayed)
    {
        var cell = new CellModel(new ExcelCellAddress(1, 1), displayed, "=A1", CellStyleModel.Default, true, null);
        var options = new NumericDisplayOptions(false, ",", true, ".", 2);

        Assert.Equal(displayed, NumericDisplayFormatter.Format(cell, options));
    }

    [Fact]
    public void Apply_returns_a_new_range_with_formatted_text_and_preserved_structure()
    {
        var numeric = NumericCell("1234.5", "1234.5");
        var source = new WorkbookRangeModel(
            new[] { new WorkbookRowModel(1, 15D, false) },
            new[] { new WorkbookColumnModel(1, 8D, false) },
            new[] { numeric },
            new[] { ExcelRangeReference.Parse("A1:A1") },
            Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));

        var formatted = NumericDisplayFormatter.Apply(
            source,
            new NumericDisplayOptions(false, ",", true, ".", 2));

        Assert.Equal("1.234,50", formatted.Cell("A1").DisplayText);
        Assert.Equal("1234.5", source.Cell("A1").DisplayText);
        Assert.Equal("A1:A1", Assert.Single(formatted.MergedRanges).ToA1());
    }

    private static CellModel NumericCell(string raw, string displayed) =>
        new(new ExcelCellAddress(1, 1), displayed, null, CellStyleModel.Default, false, raw);
}
