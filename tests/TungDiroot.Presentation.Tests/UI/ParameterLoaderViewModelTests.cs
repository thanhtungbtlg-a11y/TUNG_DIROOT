using System;
using System.Collections.Generic;
using TungDiroot.Core.Parameters;
using TungDiroot.Presentation.UI;
using Xunit;

namespace TungDiroot.Presentation.Tests.UI;

public sealed class ParameterLoaderViewModelTests
{
    [Fact]
    public void Only_ready_rows_can_be_selected_and_assign_receives_hidden_selected_rows()
    {
        IReadOnlyList<ParameterAssignmentPlan>? requested = null;
        var vm = new ParameterLoaderViewModel(analyze: () => { }, assign: plans => requested = plans);
        vm.SetPlans(new[] { Plan("A", ParameterPlanStatus.Ready), Plan("B", ParameterPlanStatus.ReviewRequired) });

        vm.SelectReadyCommand.Execute(null);
        vm.SearchText = "B";
        vm.AssignCommand.Execute(null);

        Assert.Equal("A", Assert.Single(requested!).Definition!.Name);
        Assert.False(vm.VisibleRows[0].CanSelect);
    }

    [Fact]
    public void Assign_is_disabled_without_a_selected_ready_row()
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        vm.SetPlans(new[] { Plan("A", ParameterPlanStatus.AlreadyBound) });
        Assert.False(vm.AssignCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("asset")]
    [InlineData("11111111")]
    [InlineData("ost_doors")]
    [InlineData("preview reason")]
    public void Search_covers_group_name_guid_category_and_reason(string query)
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        vm.SetPlans(new[] { Plan("Asset", ParameterPlanStatus.Ready, "Preview reason") });

        vm.SearchText = query;

        Assert.Single(vm.VisibleRows);
    }

    [Fact]
    public void Status_filter_preserves_selection_when_row_is_hidden()
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        vm.SetPlans(new[] { Plan("A", ParameterPlanStatus.Ready), Plan("B", ParameterPlanStatus.AlreadyBound) });
        vm.VisibleRows[0].IsSelected = true;

        vm.StatusFilter = ParameterPlanStatusFilter.AlreadyBound;
        Assert.Single(vm.VisibleRows);
        vm.StatusFilter = ParameterPlanStatusFilter.All;

        Assert.True(vm.VisibleRows[0].IsSelected);
    }

    [Fact]
    public void Invert_selection_changes_only_ready_rows()
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        vm.SetPlans(new[] { Plan("A", ParameterPlanStatus.Ready), Plan("B", ParameterPlanStatus.Ready), Plan("C", ParameterPlanStatus.ReviewRequired) });
        vm.VisibleRows[0].IsSelected = true;

        vm.InvertReadyCommand.Execute(null);

        Assert.False(vm.VisibleRows[0].IsSelected);
        Assert.True(vm.VisibleRows[1].IsSelected);
        Assert.False(vm.VisibleRows[2].IsSelected);
    }

    [Fact]
    public void Changing_a_source_path_clears_stale_plans()
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        vm.SetPlans(new[] { Plan("A", ParameterPlanStatus.Ready) });
        vm.SelectReadyCommand.Execute(null);

        vm.WorkbookPath = "new.xlsx";

        Assert.Empty(vm.VisibleRows);
        Assert.False(vm.CanAssign);
    }

    [Fact]
    public void Counts_follow_all_rows_not_current_filter()
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        vm.SetPlans(new[]
        {
            Plan("A", ParameterPlanStatus.Ready),
            Plan("B", ParameterPlanStatus.AlreadyBound),
            Plan("C", ParameterPlanStatus.ReviewRequired),
            Plan("D", ParameterPlanStatus.InvalidCategory)
        });

        vm.StatusFilter = ParameterPlanStatusFilter.Ready;

        Assert.Equal(1, vm.ReadyCount);
        Assert.Equal(1, vm.AlreadyBoundCount);
        Assert.Equal(1, vm.ReviewCount);
        Assert.Equal(1, vm.ErrorCount);
    }

    [Fact]
    public void Failed_assignment_is_overlaid_on_refreshed_row_with_revit_reason()
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        var attempted = Plan("Rejected", ParameterPlanStatus.Ready);
        vm.SetPlans(new[] { Plan("Rejected", ParameterPlanStatus.Ready) });

        vm.ApplyBatchResult(new[] { ParameterAssignmentResult.Failed(attempted, "REVIT_BINDING_INSERT_RETURNED_FALSE") });

        var row = Assert.Single(vm.VisibleRows);
        Assert.Equal(ParameterPlanStatus.AssignmentFailed, row.Status);
        Assert.Equal("REVIT_BINDING_INSERT_RETURNED_FALSE", row.Reason);
        Assert.False(row.CanSelect);
        Assert.Equal(1, vm.ErrorCount);
    }

    [Fact]
    public void Assigned_result_is_authoritative_even_without_a_successful_refresh()
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        var attempted = Plan("Created", ParameterPlanStatus.Ready);
        vm.SetPlans(new[] { attempted });

        vm.ApplyBatchResult(new[] { ParameterAssignmentResult.Assigned(attempted) });

        var row = Assert.Single(vm.VisibleRows);
        Assert.Equal(ParameterPlanStatus.AlreadyBound, row.Status);
        Assert.False(row.CanSelect);
        Assert.Equal(1, vm.AlreadyBoundCount);
    }

    [Theory]
    [InlineData(ParameterPlanStatus.Ready, "Sẵn sàng")]
    [InlineData(ParameterPlanStatus.AlreadyBound, "Đã gán")]
    [InlineData(ParameterPlanStatus.ReviewRequired, "Cần xem xét")]
    [InlineData(ParameterPlanStatus.AssignmentFailed, "Gán thất bại")]
    public void Status_rows_use_vietnamese_display_labels(ParameterPlanStatus status, string expected)
    {
        var vm = new ParameterLoaderViewModel(() => { }, _ => { });
        vm.SetPlans(new[] { Plan("A", status) });

        Assert.Equal(expected, vm.VisibleRows[0].StatusDisplay);
    }

    [Fact]
    public void Status_filter_options_use_vietnamese_display_labels()
    {
        Assert.Contains(ParameterPlanStatusDisplay.FilterOptions,
            option => option.Value == ParameterPlanStatusFilter.All && option.Label == "Tất cả");
        Assert.Contains(ParameterPlanStatusDisplay.FilterOptions,
            option => option.Value == ParameterPlanStatusFilter.InvalidCategory && option.Label == "Category không hợp lệ");
    }

    private static ParameterAssignmentPlan Plan(
        string name,
        ParameterPlanStatus status,
        string reason = "Preview reason")
    {
        var definition = new SharedParameterDefinitionModel("Identity", name, Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var rule = new ParameterRuleDraft(2, true, "Identity", name, ParameterBindingKind.Instance, "PG_DATA",
            new[] { "OST_Doors" }, Array.Empty<ParameterConfigurationIssue>());
        return new ParameterAssignmentPlan(rule, definition, status, reason);
    }
}
