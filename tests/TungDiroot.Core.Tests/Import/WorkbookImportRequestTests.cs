using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using Xunit;

namespace TungDiroot.Core.Tests.Import;

public sealed class WorkbookImportRequestTests
{
    [Fact]
    public void Request_preserves_all_import_choices()
    {
        var range = ExcelRangeReference.Parse("B2:D9");
        var request = new WorkbookImportRequest(
            "C:\\files\\source.xlsx",
            "Report",
            range,
            ImportMode.Image,
            TargetViewKind.Legend,
            UpdateBehavior.DataOnly,
            ImageDpi.Dpi600,
            IncludeHiddenRowsAndColumns: true,
            BlackAndWhite: true,
            StyleProfileName: "MEP Company");

        Assert.Equal("C:\\files\\source.xlsx", request.FilePath);
        Assert.Equal("Report", request.WorksheetName);
        Assert.Equal("B2:D9", request.Range.ToA1());
        Assert.Equal(ImportMode.Image, request.Mode);
        Assert.Equal(TargetViewKind.Legend, request.TargetView);
        Assert.Equal(UpdateBehavior.DataOnly, request.UpdateBehavior);
        Assert.Equal(ImageDpi.Dpi600, request.Dpi);
        Assert.True(request.IncludeHiddenRowsAndColumns);
        Assert.True(request.BlackAndWhite);
        Assert.Equal("MEP Company", request.StyleProfileName);
    }

    [Fact]
    public void Image_dpi_values_match_their_export_resolution()
    {
        Assert.Equal(150, (int)ImageDpi.Dpi150);
        Assert.Equal(300, (int)ImageDpi.Dpi300);
        Assert.Equal(600, (int)ImageDpi.Dpi600);
    }
}
