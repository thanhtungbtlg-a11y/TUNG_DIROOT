using System;
using System.Linq;
using TungDiroot.Core.DataLink;
using TungDiroot.Presentation.DataLink;
using Xunit;

namespace TungDiroot.Presentation.Tests.DataLink;

public sealed class DataLinkViewModelTests
{
    [Fact]
    public void Apply_sends_only_selected_ready_changes()
    {
        DataLinkChangePlan? requested = null;
        var vm = new DataLinkViewModel(_ => { }, plan => requested = plan);
        vm.SetPlan(Plan(
            Change("ready", DataLinkChangeStatus.Ready),
            Change("blocked", DataLinkChangeStatus.StaleModelValue)));

        vm.SelectAllReadyCommand.Execute(null);
        vm.ApplyCommand.Execute(null);

        var applied = Assert.Single(requested!.Changes);
        Assert.Equal("ready", applied.Row.ElementUniqueId);
        Assert.Equal(DataLinkChangeStatus.Ready, applied.Status);
    }

    [Fact]
    public void Select_all_never_selects_blocked_rows()
    {
        var vm = new DataLinkViewModel(_ => { }, _ => { });
        vm.SetPlan(Plan(
            Change("ready", DataLinkChangeStatus.Ready),
            Change("readonly", DataLinkChangeStatus.ReadOnly),
            Change("stale", DataLinkChangeStatus.StaleModelValue)));

        vm.SelectAllReadyCommand.Execute(null);

        Assert.True(vm.Rows.Single(row => row.ElementUniqueId == "ready").IsSelected);
        Assert.All(vm.Rows.Where(row => row.ElementUniqueId != "ready"), row => Assert.False(row.IsSelected));
    }

    [Fact]
    public void Busy_state_disables_export_and_import()
    {
        var vm = new DataLinkViewModel(_ => { }, _ => { }) { IsBusy = true };

        Assert.False(vm.ExportCommand.CanExecute(null));
        Assert.False(vm.ImportCommand.CanExecute(null));
        Assert.False(vm.CanExport);
        Assert.False(vm.CanImport);
    }

    [Fact]
    public void Close_is_rejected_while_an_external_action_is_pending()
    {
        var vm = new DataLinkViewModel(_ => { }, _ => { }) { IsExternalActionPending = true };

        Assert.False(vm.CanClose);
        Assert.False(vm.RequestClose());
        Assert.Equal("Đang chờ Revit xử lý; chưa thể đóng cửa sổ.", vm.StatusText);
    }

    [Fact]
    public void Export_query_contains_only_checked_catalog_entries()
    {
        DataLinkQuery? requested = null;
        var vm = new DataLinkViewModel(query => requested = query, _ => { });
        vm.SetCatalog(new DataLinkCatalog(
            new[] { new DataLinkCategoryOption(10, "Pipes"), new DataLinkCategoryOption(20, "Ducts") },
            new[]
            {
                new DataLinkParameterOption(new DataLinkParameterSelection("builtin:1", false), "Mark", "String"),
                new DataLinkParameterOption(new DataLinkParameterSelection("builtin:2", true), "Type Mark", "String")
            }));
        vm.Categories[0].IsSelected = true;
        vm.Parameters[1].IsSelected = true;
        vm.Scope = DataLinkScope.ActiveView;

        vm.ExportCommand.Execute(null);

        Assert.Equal(DataLinkScope.ActiveView, requested!.Scope);
        Assert.Equal(new long[] { 10 }, requested.CategoryIds);
        Assert.Equal(new[] { new DataLinkParameterSelection("builtin:2", true) }, requested.Parameters);
    }

    [Fact]
    public void Export_is_disabled_until_a_parameter_is_checked()
    {
        var vm = new DataLinkViewModel(_ => { }, _ => { });
        vm.SetCatalog(new DataLinkCatalog(
            new[] { new DataLinkCategoryOption(10, "Pipes") },
            new[] { new DataLinkParameterOption(new DataLinkParameterSelection("builtin:1", false), "Mark", "String") }));

        Assert.False(vm.ExportCommand.CanExecute(null));
        vm.Parameters[0].IsSelected = true;
        Assert.True(vm.ExportCommand.CanExecute(null));
    }

    private static DataLinkChangePlan Plan(params DataLinkCandidateChange[] changes) =>
        new(new DataLinkDocumentStamp("model-a"), changes);

    private static DataLinkCandidateChange Change(string id, DataLinkChangeStatus status)
    {
        var parameter = new DataLinkParameterKey("builtin:-1", "Mark", false, "String", null);
        var row = new DataLinkElementRow(id, 1, null, "Walls", "Basic Wall", "Generic", parameter, "old", "old", "new");
        return new DataLinkCandidateChange(row, status, status == DataLinkChangeStatus.Ready ? "new" : null, status.ToString());
    }
}
