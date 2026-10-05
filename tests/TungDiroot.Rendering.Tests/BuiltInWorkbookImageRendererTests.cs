using System;
using System.IO;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using Xunit;

namespace TungDiroot.Rendering.Tests;

public sealed class BuiltInWorkbookImageRendererTests
{
    [Theory]
    [InlineData(ImageDpi.Dpi150, 300)]
    [InlineData(ImageDpi.Dpi300, 600)]
    [InlineData(ImageDpi.Dpi600, 1200)]
    public void Two_inch_table_has_expected_pixel_width(ImageDpi dpi, int expectedWidth)
    {
        var model = TwoInchTable();
        using var output = new MemoryStream();

        var result = new BuiltInWorkbookImageRenderer().Render(model, new ImageRenderOptions(dpi, false, true), output);

        Assert.Equal(expectedWidth, result.PixelWidth);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, result.Bytes[..8]);
        Assert.Equal(result.Bytes, output.ToArray());
    }

    [Fact]
    public void Missing_font_is_reported_with_a_fallback()
    {
        var model = TwoInchTable("TUNG_DIROOT_FONT_THAT_DOES_NOT_EXIST");
        using var output = new MemoryStream();

        var result = new BuiltInWorkbookImageRenderer().Render(model, ImageRenderOptions.Default, output);

        Assert.Contains(result.Warnings, warning => warning.Code == "FONT_FALLBACK");
    }

    private static WorkbookRangeModel TwoInchTable(string fontName = "Arial")
    {
        var style = CellStyleModel.Default with { Font = CellStyleModel.Default.Font with { Name = fontName } };
        return new WorkbookRangeModel(
            new[] { new WorkbookRowModel(1, 36D, false) },
            new[] { new WorkbookColumnModel(1, 144D / 7D, false) },
            new[] { new CellModel(new ExcelCellAddress(1, 1), "PCCC", null, style, false) },
            Array.Empty<ExcelRangeReference>(),
            Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(Array.Empty<WorkbookDrawingItem>()));
    }
}
