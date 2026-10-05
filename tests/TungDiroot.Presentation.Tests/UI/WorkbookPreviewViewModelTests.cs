using System;
using System.Collections.Generic;
using TungDiroot.Core.Preview;
using TungDiroot.Presentation.UI;
using Xunit;

namespace TungDiroot.Presentation.Tests.UI;

public sealed class WorkbookPreviewViewModelTests
{
    [Fact]
    public void Clean_report_is_ready_and_summarizes_dimensions_and_elements()
    {
        var viewModel = new WorkbookPreviewViewModel("PCCC!A1:D5", Report());

        Assert.Equal("PCCC!A1:D5", viewModel.SourceLabel);
        Assert.Equal("5 hàng × 4 cột · 20 ô", viewModel.DimensionsText);
        Assert.Equal("Ước tính 26 element Revit", viewModel.EstimateText);
        Assert.Equal("Sẵn sàng", viewModel.StatusText);
        Assert.True(viewModel.CanImport);
        Assert.Empty(viewModel.Issues);
    }

    [Fact]
    public void Warning_report_remains_importable_and_error_report_is_blocked()
    {
        var warning = new WorkbookPreviewIssue(PreviewIssueSeverity.Warning, "FONT_FALLBACK", "Thiếu font");
        var error = new WorkbookPreviewIssue(PreviewIssueSeverity.Error, "FORMULA_REF_ERROR", "Lỗi #REF!", "B2");

        var warningViewModel = new WorkbookPreviewViewModel("warning", Report(warning));
        var errorViewModel = new WorkbookPreviewViewModel("error", Report(error));

        Assert.Equal("Có cảnh báo", warningViewModel.StatusText);
        Assert.True(warningViewModel.CanImport);
        Assert.Equal("Warning", Assert.Single(warningViewModel.Issues).SeverityText);
        Assert.Equal("Có lỗi cần sửa", errorViewModel.StatusText);
        Assert.False(errorViewModel.CanImport);
        Assert.Equal("B2", Assert.Single(errorViewModel.Issues).CellAddress);
    }

    [Fact]
    public void Preview_availability_tracks_selected_row_and_busy_state()
    {
        var row = new AddExcelRowViewModel { FilePath = "A.xlsx", WorksheetName = "PCCC", RangeReference = "A1:B2" };
        var viewModel = new AddExcelViewModel(new[] { row });
        Assert.False(viewModel.CanPreview);

        viewModel.SelectedRow = row;
        Assert.True(viewModel.CanPreview);

        viewModel.IsPreviewing = true;
        Assert.False(viewModel.CanPreview);

        viewModel.IsPreviewing = false;
        viewModel.SelectedRow = null;
        Assert.False(viewModel.CanPreview);
    }

    private static WorkbookPreviewReport Report(params WorkbookPreviewIssue[] issues) =>
        new(5, 4, 20, 0, new WorkbookElementEstimate(20, 4, 2, 0), issues);
}
