using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TungDiroot.Core.Import;
using TungDiroot.Presentation.UI;
using Xunit;

namespace TungDiroot.Presentation.Tests.UI;

public sealed class AddExcelViewModelTests
{
    [Theory]
    [InlineData(ImportMode.Image, TargetViewKind.Schedule, false, "IMAGE_SCHEDULE_UNSUPPORTED")]
    [InlineData(ImportMode.RevitTable, TargetViewKind.Legend, false, "LEGEND_SEED_REQUIRED")]
    public void Invalid_rows_surface_the_planner_error(ImportMode mode, TargetViewKind target, bool seed, string code)
    {
        var row = Valid(); row.ImportMode = mode; row.TargetViewKind = target; row.LegendSeedAvailable = seed;
        Assert.Contains(AddExcelViewModel.ValidateRow(row), message => message.Code == code);
    }

    [Fact]
    public void Duplicate_view_names_are_rejected_before_import()
    {
        var first = Valid(); first.RequestedViewName = "DM PCCC";
        var second = Valid(); second.RequestedViewName = "dm pccc";
        Assert.Contains(new AddExcelViewModel(new[] { first, second }).Validate(), message => message.Code == "DUPLICATE_VIEW_NAME");
    }

    [Fact]
    public void Build_request_preserves_explicit_image_renderer()
    {
        var row = Valid();
        row.ImportMode = ImportMode.Image;
        row.RendererEngine = ImageRendererEngine.ExcelHighFidelity;
        row.UpdateBehavior = UpdateBehavior.DataOnly;
        row.StyleProfileName = "MEP Company";
        var request = Assert.Single(new AddExcelViewModel(new[] { row }).BuildRequests());
        Assert.Equal(ImageRendererEngine.ExcelHighFidelity, request.RendererEngine);
        Assert.Equal(UpdateBehavior.DataOnly, request.UpdateBehavior);
        Assert.Equal("MEP Company", request.StyleProfileName);
    }

    [Fact]
    public void Select_all_and_clear_all_update_every_import_row()
    {
        var first = Valid();
        var second = Valid();
        var viewModel = new AddExcelViewModel(new[] { first, second });

        viewModel.SetAllSelected(false);
        Assert.All(viewModel.Rows, row => Assert.False(row.IsSelected));

        viewModel.SetAllSelected(true);
        Assert.All(viewModel.Rows, row => Assert.True(row.IsSelected));
    }

    [Fact]
    public void Selection_summary_and_import_availability_follow_row_changes()
    {
        var first = Valid();
        var second = Valid();
        var viewModel = new AddExcelViewModel(new[] { first, second });
        var changes = new List<string?>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        first.IsSelected = false;

        Assert.Contains("SelectionSummary", changes);
        Assert.Contains("CanImport", changes);
        Assert.Equal("Đã chọn 1/2", viewModel.SelectionSummary);
        Assert.True(viewModel.CanImport);

        second.IsSelected = false;

        Assert.Equal("Đã chọn 0/2", viewModel.SelectionSummary);
        Assert.False(viewModel.CanImport);
    }

    [Fact]
    public void Import_availability_is_disabled_while_an_import_is_running()
    {
        var viewModel = new AddExcelViewModel(new[] { Valid() });
        Assert.True(viewModel.CanImport);

        viewModel.IsImporting = true;

        Assert.False(viewModel.CanImport);
    }

    private static AddExcelRowViewModel Valid() => new AddExcelRowViewModel
    {
        FilePath = "A.xlsx", WorksheetName = "PCCC", RangeReference = "A1:B2", RequestedViewName = "DM PCCC",
        EstimatedRows = 2, EstimatedColumns = 2
    };
}
