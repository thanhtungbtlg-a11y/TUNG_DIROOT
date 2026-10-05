using System;
using TungDiroot.Core.Excel;
using Xunit;

namespace TungDiroot.Core.Tests.Excel;

public sealed class ExcelRangeReferenceTests
{
    [Theory]
    [InlineData("A1", 1, 1)]
    [InlineData("$BC$27", 55, 27)]
    [InlineData("AAA1048576", 703, 1048576)]
    [InlineData("XFD1048576", 16384, 1048576)]
    public void Parse_cell_address(string text, int column, int row)
    {
        var value = ExcelCellAddress.Parse(text);

        Assert.Equal(column, value.Column);
        Assert.Equal(row, value.Row);
    }

    [Fact]
    public void Parse_cell_address_normalizes_case_and_absolute_markers()
    {
        var value = ExcelCellAddress.Parse("$bc$27");

        Assert.Equal("BC27", value.ToA1());
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("A0")]
    [InlineData("A-1")]
    [InlineData("A1B")]
    [InlineData("$$A1")]
    [InlineData("XFE1")]
    [InlineData("A1048577")]
    public void Parse_cell_address_rejects_malformed_or_non_positive_coordinates(string text)
    {
        Assert.Throws<ArgumentException>(() => ExcelCellAddress.Parse(text));
    }

    [Fact]
    public void Normalize_reversed_range()
    {
        var range = ExcelRangeReference.Parse("H25:A1");

        Assert.Equal("A1:H25", range.ToA1());
    }

    [Fact]
    public void Parse_range_normalizes_each_axis_independently()
    {
        var range = ExcelRangeReference.Parse("A25:H1");

        Assert.Equal("A1:H25", range.ToA1());
    }

    [Fact]
    public void Parse_single_cell_range_normalizes_to_a_two_endpoint_a1_reference()
    {
        var range = ExcelRangeReference.Parse("$A$1");

        Assert.Equal("A1:A1", range.ToA1());
    }

    [Fact]
    public void Directly_constructed_range_normalizes_reversed_endpoints()
    {
        var range = new ExcelRangeReference(new ExcelCellAddress(8, 25), new ExcelCellAddress(1, 1));

        Assert.Equal("A1:H25", range.ToA1());
    }

    [Fact]
    public void Public_record_shape_preserves_named_arguments_deconstruction_and_with_expressions()
    {
        var address = new ExcelCellAddress(Column: 1, Row: 2);
        var (column, row) = address;
        var movedAddress = address with { Row = 3 };
        var range = new ExcelRangeReference(Start: address, End: movedAddress);
        var (start, end) = range;
        var extendedRange = range with { End = new ExcelCellAddress(2, 4) };

        Assert.Equal((1, 2), (column, row));
        Assert.Equal("A3", movedAddress.ToA1());
        Assert.Equal(address, start);
        Assert.Equal(movedAddress, end);
        Assert.Equal("A2:B4", extendedRange.ToA1());
    }

    [Fact]
    public void Default_cell_address_cannot_render_invalid_a1_text()
    {
        Assert.Throws<InvalidOperationException>(() => default(ExcelCellAddress).ToA1());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1, int.MaxValue)]
    public void Directly_constructed_cell_rejects_coordinates_outside_the_excel_grid(int column, int row)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExcelCellAddress(column, row));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A1:")]
    [InlineData(":A1")]
    [InlineData("A1:B2:C3")]
    [InlineData("A0:B2")]
    public void Parse_range_rejects_invalid_references(string text)
    {
        Assert.Throws<ArgumentException>(() => ExcelRangeReference.Parse(text));
    }
}
