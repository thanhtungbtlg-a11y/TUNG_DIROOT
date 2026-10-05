using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Filter;
using TungDiroot.Presentation.Filter;
using Xunit;

namespace TungDiroot.Presentation.Tests.Filter;

public sealed class FilterViewModelTests
{
    [Fact]
    public void Query_contains_checked_fixed_filters_and_valid_rules()
    {
        FilterQuery? requested = null;
        var vm = ViewModel(query => requested = query);
        vm.SetCatalog(Catalog());
        vm.Categories[0].IsSelected = true;
        vm.Families[0].IsSelected = true;
        vm.Types[0].IsSelected = true;
        vm.Scope = FilterScope.ActiveView;
        vm.Join = FilterJoin.Or;
        vm.Rules[0].SelectedParameter = vm.Parameters.Single(item => item.Name == "Comments");
        vm.Rules[0].SelectedOperator = FilterOperator.Contains;
        vm.Rules[0].Operand = "PCCC";

        vm.RunCommand.Execute(null);

        Assert.NotNull(requested);
        Assert.Equal(FilterScope.ActiveView, requested!.Scope);
        Assert.Equal(FilterJoin.Or, requested.Join);
        Assert.Equal(new long[] { 10 }, requested.CategoryIds);
        Assert.Equal(new[] { "Pipe" }, requested.FamilyNames);
        Assert.Equal(new[] { "Standard" }, requested.TypeNames);
        Assert.Equal(FilterOperator.Contains, Assert.Single(requested.Rules).Operator);
        Assert.True(vm.IsBusy);
    }

    [Fact]
    public void Parameter_kind_limits_available_operators()
    {
        var vm = ViewModel();
        vm.SetCatalog(Catalog());

        vm.Rules[0].SelectedParameter = vm.Parameters.Single(item => item.Name == "Length");

        Assert.Contains(FilterOperator.GreaterThan, vm.Rules[0].AvailableOperators);
        Assert.DoesNotContain(FilterOperator.Contains, vm.Rules[0].AvailableOperators);
    }

    [Fact]
    public void Rules_can_be_added_and_removed_without_removing_the_last_row()
    {
        var vm = ViewModel();
        vm.SetCatalog(Catalog());

        vm.AddRuleCommand.Execute(null);
        Assert.Equal(2, vm.Rules.Count);
        vm.RemoveRuleCommand.Execute(vm.Rules[1]);
        vm.RemoveRuleCommand.Execute(vm.Rules[0]);

        Assert.Single(vm.Rules);
    }

    [Fact]
    public void Invalid_operand_is_reported_inline_and_run_is_not_requested()
    {
        var calls = 0;
        var vm = ViewModel(_ => calls++);
        vm.SetCatalog(Catalog());
        vm.Rules[0].SelectedParameter = vm.Parameters.Single(item => item.Name == "Length");
        vm.Rules[0].SelectedOperator = FilterOperator.GreaterThan;
        vm.Rules[0].Operand = "mười";

        vm.RunCommand.Execute(null);

        Assert.Equal(0, calls);
        Assert.False(vm.IsBusy);
        Assert.Contains("giá trị", vm.ValidationText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cancel_marks_request_and_disables_a_second_cancel()
    {
        var cancelled = 0;
        var vm = ViewModel(cancel: () => cancelled++);
        vm.SetCatalog(Catalog());
        vm.Rules[0].SelectedParameter = vm.Parameters[0];
        vm.Rules[0].SelectedOperator = FilterOperator.IsNotEmpty;
        vm.RunCommand.Execute(null);

        vm.CancelCommand.Execute(null);

        Assert.True(vm.IsCancellationRequested);
        Assert.Equal(1, cancelled);
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void Results_select_all_and_actions_use_only_selected_rows()
    {
        IReadOnlyList<FilterMatch>? acted = null;
        FilterResultAction? action = null;
        var vm = ViewModel(action: (kind, rows) => { action = kind; acted = rows; });
        vm.SetResults(new[] { Match("one"), Match("two") });

        vm.SelectAllResultsCommand.Execute(null);
        vm.SelectAndShowCommand.Execute(null);

        Assert.Equal(FilterResultAction.SelectAndShow, action);
        Assert.Equal(new[] { "one", "two" }, acted!.Select(item => item.Element.UniqueId));
    }

    [Fact]
    public void Zero_results_and_stale_document_are_explicit()
    {
        var vm = ViewModel(documentIdentity: "model-a");

        vm.SetResults(Array.Empty<FilterMatch>());

        Assert.Contains("0", vm.StatusText);
        Assert.True(vm.IsCurrentDocument("model-a"));
        Assert.False(vm.IsCurrentDocument("model-b"));
        Assert.False(vm.SelectAndShowCommand.CanExecute(null));
    }

    [Fact]
    public void Reset_temporary_visibility_is_available_without_selected_results()
    {
        FilterResultAction? requested = null;
        var vm = ViewModel(action: (action, _) => requested = action);

        vm.ResetTemporaryIsolationCommand.Execute(null);

        Assert.Equal(FilterResultAction.ResetTemporaryIsolation, requested);
    }

    [Fact]
    public void Profile_round_trip_restores_query_and_invokes_save()
    {
        FilterProfile? saved = null;
        var vm = ViewModel(save: profile => saved = profile);
        vm.SetCatalog(Catalog());
        vm.Categories[1].IsSelected = true;
        vm.Rules[0].SelectedParameter = vm.Parameters.Single(item => item.Name == "Comments");
        vm.Rules[0].SelectedOperator = FilterOperator.Contains;
        vm.Rules[0].Operand = "test";
        vm.ProfileName = "Ống cấp nước";
        vm.GroupParameter = vm.Parameters.Single(item => item.Name == "Type Mark");

        vm.SaveProfileCommand.Execute(null);

        Assert.NotNull(saved);
        var loaded = ViewModel();
        loaded.SetCatalog(Catalog());
        loaded.LoadProfile(saved!);
        Assert.Equal("Ống cấp nước", loaded.ProfileName);
        Assert.True(loaded.Categories.Single(item => item.Id == 20).IsSelected);
        Assert.Equal("test", Assert.Single(loaded.Rules).Operand);
        Assert.Equal("Type Mark", loaded.GroupParameter?.Name);
    }

    [Fact]
    public void Window_cannot_close_while_a_batch_or_external_event_is_running()
    {
        var vm = ViewModel();
        vm.IsBusy = true;
        Assert.False(vm.RequestClose());
        vm.IsBusy = false;
        vm.IsExternalActionPending = true;
        Assert.False(vm.RequestClose());
    }

    private static FilterViewModel ViewModel(
        Action<FilterQuery>? run = null,
        Action? cancel = null,
        Action<FilterResultAction, IReadOnlyList<FilterMatch>>? action = null,
        Action<FilterProfile>? save = null,
        string documentIdentity = "model-a") =>
        new(run ?? (_ => { }), cancel ?? (() => { }), action ?? ((_, _) => { }), save ?? (_ => { }), documentIdentity);

    private static FilterCatalog Catalog() => new(
        new[] { new FilterCategoryDescriptor(10, "Pipes"), new FilterCategoryDescriptor(20, "Ducts") },
        new[] { "Pipe", "Duct" },
        new[] { "Standard", "Round" },
        new[]
        {
            new FilterParameterDescriptor("instance:comments", "Comments", FilterValueKind.String, false),
            new FilterParameterDescriptor("instance:length", "Length", FilterValueKind.Number, false),
            new FilterParameterDescriptor("type:type_mark", "Type Mark", FilterValueKind.String, true)
        });

    private static FilterMatch Match(string id)
    {
        var snapshot = new FilterElementSnapshot(
            id, id == "one" ? 1 : 2, 10, "Pipes", "Pipe", "Standard", "Level 1",
            new Dictionary<string, FilterValue>());
        return new FilterMatch(snapshot, Array.Empty<FilterRule>());
    }
}
