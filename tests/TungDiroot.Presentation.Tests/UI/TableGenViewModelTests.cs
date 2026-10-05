using System;
using System.Collections.Generic;
using TungDiroot.Core.Batch;
using TungDiroot.Core.Import;
using TungDiroot.Presentation.Links;
using TungDiroot.Presentation.UI;
using Xunit;

namespace TungDiroot.Presentation.Tests.UI;

public sealed class TableGenViewModelTests
{
    [Fact]
    public void Reentrant_ui_request_is_rejected_without_unlocking_pending_action()
    {
        var model = new TableGenViewModel(new FakeSource(Array.Empty<TableGenLinkItem>()));

        Assert.True(model.TryBeginUiAction());
        Assert.False(model.TryBeginUiAction());
        Assert.True(model.IsBusy);
        Assert.False(model.AddExcelCommand.CanExecute(null));
    }

    [Fact]
    public void Ui_request_is_rejected_while_batch_is_busy()
    {
        var model = new TableGenViewModel(new FakeSource(Array.Empty<TableGenLinkItem>())) { IsBusy = true };

        Assert.False(model.TryBeginUiAction());
        Assert.True(model.IsBusy);
    }

    [Theory]
    [InlineData(BatchRunState.Completed)]
    [InlineData(BatchRunState.Stopped)]
    [InlineData(BatchRunState.Idle)]
    public void Inactive_batch_progress_cannot_unlock_an_owned_pending_ui_action(BatchRunState state)
    {
        var model = new TableGenViewModel(new FakeSource(Array.Empty<TableGenLinkItem>()));
        Assert.True(model.TryBeginUiAction());

        model.SetBatchProgress(new BatchProgress(state, 0, 0, 0, 0, 0, false));

        Assert.True(model.IsBusy);
        Assert.False(model.TryBeginUiAction());
    }

    [Fact]
    public void Batch_controls_are_disabled_for_ui_action_without_active_batch()
    {
        var model = new TableGenViewModel(new FakeSource(Array.Empty<TableGenLinkItem>()));
        Assert.True(model.TryBeginUiAction());

        Assert.False(model.PauseBatchCommand.CanExecute(null));
        Assert.False(model.ResumeBatchCommand.CanExecute(null));
        Assert.False(model.StopBatchCommand.CanExecute(null));
    }

    [Fact]
    public void Completing_ui_action_releases_its_busy_ownership()
    {
        var model = new TableGenViewModel(new FakeSource(Array.Empty<TableGenLinkItem>()));
        Assert.True(model.TryBeginUiAction());

        model.CompleteUiAction();

        Assert.False(model.IsBusy);
        Assert.True(model.TryBeginUiAction());
    }

    [Fact]
    public void Active_batch_enables_batch_controls_and_blocks_ui_actions()
    {
        var model = new TableGenViewModel(new FakeSource(Array.Empty<TableGenLinkItem>()));

        model.SetBatchProgress(new BatchProgress(BatchRunState.Running, 2, 0, 0, 0, 0, false));

        Assert.True(model.IsBusy);
        Assert.True(model.PauseBatchCommand.CanExecute(null));
        Assert.True(model.StopBatchCommand.CanExecute(null));
        Assert.False(model.TryBeginUiAction());
    }

    [Fact]
    public void Filter_and_counts_follow_link_status()
    {
        var vm = new TableGenViewModel(new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx"), Item(LinkStatus.Changed, "B.xlsx"), Item(LinkStatus.Missing, "C.xlsx") }));
        Assert.Equal(1, vm.ReadyCount);
        Assert.Equal(1, vm.ChangedCount);
        Assert.Equal(1, vm.MissingCount);
        vm.StatusFilter = LinkStatusFilter.Changed;
        Assert.Single(vm.VisibleLinks);
    }

    [Fact]
    public void Search_is_ordinal_ignore_case_across_source_and_view_fields()
    {
        var vm = new TableGenViewModel(new FakeSource(new[] { Item(LinkStatus.Ready, "PCCC.xlsx", "Thong Ke", "A1:D4", "VIEW PCCC"), Item(LinkStatus.Ready, "Other.xlsx") }));
        vm.SearchText = "thong ke";
        Assert.Single(vm.VisibleLinks);
        vm.SearchText = "view pccc";
        Assert.Single(vm.VisibleLinks);
    }

    [Fact]
    public void Filter_changes_preserve_selection_and_auto_sync_edits()
    {
        IReadOnlyDictionary<Guid, bool>? saved = null;
        var item = Item(LinkStatus.Ready, "PCCC.xlsx", view: "VIEW PCCC");
        var vm = new TableGenViewModel(new FakeSource(new[] { item }), saveAutoSync: values => saved = values);
        vm.VisibleLinks[0].IsSelected = true;
        vm.VisibleLinks[0].AutoSync = true;
        vm.SearchText = "missing";
        Assert.Empty(vm.VisibleLinks);
        vm.SearchText = "PCCC";
        Assert.True(vm.VisibleLinks[0].IsSelected);
        Assert.True(vm.VisibleLinks[0].AutoSync);
        vm.SaveAutoSyncCommand.Execute(null);
        Assert.True(saved![item.LinkId]);
    }

    [Fact]
    public void Sync_all_uses_every_source_link_in_source_order_despite_filter_and_preserves_selection()
    {
        var first = Item(LinkStatus.Ready, "A.xlsx", view: "VIEW A");
        var second = Item(LinkStatus.Changed, "B.xlsx", view: "VIEW B");
        IReadOnlyList<Guid>? requested = null;
        var vm = new TableGenViewModel(
            new FakeSource(new[] { first, second }),
            syncAll: ids => requested = ids);

        vm.VisibleLinks[0].IsSelected = true;
        vm.StatusFilter = LinkStatusFilter.Changed;
        Assert.Single(vm.VisibleLinks);

        vm.SyncAllCommand.Execute(null);

        Assert.Equal(new[] { first.LinkId, second.LinkId }, requested);
        vm.StatusFilter = LinkStatusFilter.All;
        Assert.True(vm.VisibleLinks[0].IsSelected);
        Assert.False(vm.VisibleLinks[1].IsSelected);
    }

    [Fact]
    public void Sync_all_does_not_start_an_empty_batch_and_reports_the_reason()
    {
        var callbackCount = 0;
        var vm = new TableGenViewModel(
            new FakeSource(Array.Empty<TableGenLinkItem>()),
            syncAll: _ => callbackCount++);

        vm.SyncAllCommand.Execute(null);

        Assert.Equal(0, callbackCount);
        Assert.Contains("Không có liên kết", vm.BatchStatusText);
    }

    [Fact]
    public void Sync_all_is_disabled_while_a_batch_is_busy()
    {
        var vm = new TableGenViewModel(
            new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx") }));

        Assert.True(vm.SyncAllCommand.CanExecute(null));
        vm.IsBusy = true;
        Assert.False(vm.SyncAllCommand.CanExecute(null));
        vm.IsBusy = false;
        Assert.True(vm.SyncAllCommand.CanExecute(null));
    }

    [Fact]
    public void Link_actions_require_exactly_one_checked_row_and_pass_its_id()
    {
        var first = Item(LinkStatus.Ready, "A.xlsx");
        var second = Item(LinkStatus.Ready, "B.xlsx");
        Guid? opened = null;
        Guid? unlinked = null;
        Guid? changedTarget = null;
        Guid? duplicated = null;
        var vm = new TableGenViewModel(
            new FakeSource(new[] { first, second }),
            openView: id => opened = id,
            unlinkKeepView: id => unlinked = id,
            changeTargetView: id => changedTarget = id,
            duplicateConfiguration: id => duplicated = id);

        Assert.False(vm.OpenViewCommand.CanExecute(null));
        vm.VisibleLinks[0].IsSelected = true;
        Assert.True(vm.OpenViewCommand.CanExecute(null));
        Assert.True(vm.UnlinkKeepViewCommand.CanExecute(null));

        vm.OpenViewCommand.Execute(null);
        vm.UnlinkKeepViewCommand.Execute(null);
        vm.ChangeTargetViewCommand.Execute(null);
        vm.DuplicateConfigurationCommand.Execute(null);

        Assert.Equal(first.LinkId, opened);
        Assert.Equal(first.LinkId, unlinked);
        Assert.Equal(first.LinkId, changedTarget);
        Assert.Equal(first.LinkId, duplicated);
        vm.VisibleLinks[1].IsSelected = true;
        Assert.False(vm.OpenViewCommand.CanExecute(null));
        Assert.False(vm.DeleteLinkAndViewCommand.CanExecute(null));
    }

    [Fact]
    public void Busy_state_disables_all_link_management_actions()
    {
        var vm = new TableGenViewModel(new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx") }));
        vm.VisibleLinks[0].IsSelected = true;
        Assert.True(vm.OpenSourceCommand.CanExecute(null));
        Assert.True(vm.OpenSourceFolderCommand.CanExecute(null));

        vm.IsBusy = true;

        Assert.False(vm.OpenViewCommand.CanExecute(null));
        Assert.False(vm.OpenSourceCommand.CanExecute(null));
        Assert.False(vm.OpenSourceFolderCommand.CanExecute(null));
        Assert.False(vm.UnlinkKeepViewCommand.CanExecute(null));
        Assert.False(vm.DeleteLinkAndViewCommand.CanExecute(null));
        Assert.False(vm.ChangeTargetViewCommand.CanExecute(null));
        Assert.False(vm.DuplicateConfigurationCommand.CanExecute(null));
    }

    [Fact]
    public void Rebuild_requires_exactly_one_checked_link_and_passes_its_identity()
    {
        var first = Item(LinkStatus.Ready, "A.xlsx");
        var second = Item(LinkStatus.Ready, "B.xlsx");
        var rebuilt = new List<Guid>();
        var vm = new TableGenViewModel(new FakeSource(new[] { first, second }), recreateLink: rebuilt.Add);

        Assert.False(vm.RecreateLinkCommand.CanExecute(null));
        vm.RecreateLinkCommand.Execute(null);
        Assert.Empty(rebuilt);
        vm.VisibleLinks[0].IsSelected = true;
        Assert.True(vm.RecreateLinkCommand.CanExecute(null));
        vm.RecreateLinkCommand.Execute(null);
        Assert.Equal(new[] { first.LinkId }, rebuilt);
        vm.VisibleLinks[1].IsSelected = true;
        Assert.False(vm.RecreateLinkCommand.CanExecute(null));
        vm.RecreateLinkCommand.Execute(null);
        Assert.Equal(new[] { first.LinkId }, rebuilt);
    }

    [Fact]
    public void Busy_state_prevents_rebuild_even_if_execute_is_called_directly()
    {
        var rebuilt = new List<Guid>();
        var vm = new TableGenViewModel(new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx") }), recreateLink: rebuilt.Add);
        vm.VisibleLinks[0].IsSelected = true;
        vm.IsBusy = true;

        Assert.False(vm.RecreateLinkCommand.CanExecute(null));
        vm.RecreateLinkCommand.Execute(null);
        Assert.Empty(rebuilt);
        vm.IsBusy = false;
        Assert.True(vm.RecreateLinkCommand.CanExecute(null));
    }

    [Fact]
    public void Rebuild_is_unavailable_when_no_handler_was_configured()
    {
        var vm = new TableGenViewModel(new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx") }));
        vm.VisibleLinks[0].IsSelected = true;

        Assert.False(vm.RecreateLinkCommand.CanExecute(null));
    }

    [Fact]
    public void Hidden_checked_link_does_not_allow_ambiguous_rebuild()
    {
        var rebuilt = new List<Guid>();
        var vm = new TableGenViewModel(new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx"), Item(LinkStatus.Changed, "B.xlsx") }), recreateLink: rebuilt.Add);
        vm.VisibleLinks[0].IsSelected = true;
        vm.VisibleLinks[1].IsSelected = true;
        vm.StatusFilter = LinkStatusFilter.Changed;

        Assert.Single(vm.VisibleLinks);
        Assert.False(vm.RecreateLinkCommand.CanExecute(null));
        vm.RecreateLinkCommand.Execute(null);
        Assert.Empty(rebuilt);
    }

    [Fact]
    public void Rebuild_availability_notifies_when_filter_hides_the_checked_link()
    {
        var vm = new TableGenViewModel(new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx") }), recreateLink: _ => { });
        vm.VisibleLinks[0].IsSelected = true;
        Assert.True(vm.RecreateLinkCommand.CanExecute(null));
        var notifications = 0;
        vm.RecreateLinkCommand.CanExecuteChanged += (_, _) => notifications++;

        vm.SearchText = "missing";

        Assert.False(vm.RecreateLinkCommand.CanExecute(null));
        Assert.True(notifications > 0);
    }

    private static TableGenLinkItem Item(LinkStatus status, string path, string sheet = "Sheet1", string range = "A1", string view = "View") =>
        new(Guid.NewGuid(), path, sheet, range, view, ImportMode.RevitTable, TargetViewKind.Drafting, ImageDpi.Dpi300, false, status, null);

    private sealed class FakeSource : ITableGenLinkSource
    {
        private readonly IReadOnlyList<TableGenLinkItem> _items;
        public FakeSource(IReadOnlyList<TableGenLinkItem> items) => _items = items;
        public IReadOnlyList<TableGenLinkItem> Load() => _items;
    }
}
