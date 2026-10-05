using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TungDiroot.Core.Parameters;

namespace TungDiroot.Presentation.UI;

public sealed class ParameterLoaderViewModel : BindableBase
{
    private readonly Action _analyze;
    private readonly Action<IReadOnlyList<ParameterAssignmentPlan>> _assign;
    private IReadOnlyList<ParameterPlanRowViewModel> _allRows = Array.Empty<ParameterPlanRowViewModel>();
    private string _sharedParameterPath = string.Empty;
    private string _workbookPath = string.Empty;
    private string _searchText = string.Empty;
    private ParameterPlanStatusFilter _statusFilter;
    private string _statusText = "Chọn file Shared Parameter và Excel cấu hình.";
    private bool _isBusy;

    public ParameterLoaderViewModel(Action analyze, Action<IReadOnlyList<ParameterAssignmentPlan>> assign)
    {
        _analyze = analyze ?? throw new ArgumentNullException(nameof(analyze));
        _assign = assign ?? throw new ArgumentNullException(nameof(assign));
        AnalyzeCommand = new RelayCommand(() => _analyze(), () => !IsBusy);
        AssignCommand = new RelayCommand(AssignSelected, () => !IsBusy && CanAssign);
        SelectReadyCommand = new RelayCommand(() => SetReadySelection(true), () => !IsBusy);
        ClearSelectionCommand = new RelayCommand(() => SetReadySelection(false), () => !IsBusy);
        InvertReadyCommand = new RelayCommand(InvertReady, () => !IsBusy);
    }

    public ObservableCollection<ParameterPlanRowViewModel> VisibleRows { get; } = new();
    public RelayCommand AnalyzeCommand { get; }
    public RelayCommand AssignCommand { get; }
    public RelayCommand SelectReadyCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand InvertReadyCommand { get; }

    public string SharedParameterPath
    {
        get => _sharedParameterPath;
        set { if (SetProperty(ref _sharedParameterPath, value ?? string.Empty)) ClearPlans(); }
    }

    public string WorkbookPath
    {
        get => _workbookPath;
        set { if (SetProperty(ref _workbookPath, value ?? string.Empty)) ClearPlans(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value ?? string.Empty)) ApplyFilter(); }
    }

    public ParameterPlanStatusFilter StatusFilter
    {
        get => _statusFilter;
        set { if (SetProperty(ref _statusFilter, value)) ApplyFilter(); }
    }

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value ?? string.Empty); }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            AnalyzeCommand.RaiseCanExecuteChanged();
            AssignCommand.RaiseCanExecuteChanged();
            SelectReadyCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
            InvertReadyCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CanAssign => _allRows.Any(row => row.CanSelect && row.IsSelected);
    public int ReadyCount => _allRows.Count(row => row.Status == ParameterPlanStatus.Ready);
    public int AlreadyBoundCount => _allRows.Count(row => row.Status == ParameterPlanStatus.AlreadyBound);
    public int ReviewCount => _allRows.Count(row => row.Status == ParameterPlanStatus.ReviewRequired);
    public int ErrorCount => _allRows.Count(row => row.Status is ParameterPlanStatus.MissingDefinition or
        ParameterPlanStatus.InvalidCategory or ParameterPlanStatus.ConfigurationError or ParameterPlanStatus.AssignmentFailed);

    public void SetPlans(IReadOnlyList<ParameterAssignmentPlan> plans)
    {
        if (plans == null) throw new ArgumentNullException(nameof(plans));
        _allRows = plans.Select(plan => new ParameterPlanRowViewModel(plan, SelectionChanged)).ToArray();
        StatusText = $"Đã kiểm tra {_allRows.Count} parameter; sẵn sàng {ReadyCount}.";
        ApplyFilter();
        RaiseCounts();
        SelectionChanged();
    }

    public void ApplyBatchResult(IReadOnlyList<ParameterAssignmentResult> results)
    {
        if (results == null) throw new ArgumentNullException(nameof(results));
        var assigned = results.Count(result => result.Outcome == ParameterAssignmentOutcome.Assigned);
        var failed = results.Count - assigned;
        var resultsByParameter = results
            .GroupBy(result => AssignmentKey(result.Plan), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last());
        if (resultsByParameter.Count > 0)
        {
            _allRows = _allRows.Select(row =>
            {
                if (!resultsByParameter.TryGetValue(AssignmentKey(row.Plan), out var result)) return row;
                var resultPlan = new ParameterAssignmentPlan(
                    row.Plan.Rule,
                    row.Plan.Definition,
                    result.Outcome == ParameterAssignmentOutcome.Assigned
                        ? ParameterPlanStatus.AlreadyBound
                        : ParameterPlanStatus.AssignmentFailed,
                    result.Outcome == ParameterAssignmentOutcome.Assigned
                        ? "Đã gán trong lượt hiện tại."
                        : string.IsNullOrWhiteSpace(result.Error) ? "Revit từ chối gán parameter." : result.Error!);
                return new ParameterPlanRowViewModel(resultPlan, SelectionChanged);
            }).ToArray();
            ApplyFilter();
            RaiseCounts();
            SelectionChanged();
        }
        StatusText = $"Đã gán {assigned}; thất bại {failed}.";
    }

    private static string AssignmentKey(ParameterAssignmentPlan plan) =>
        plan.Rule.DefinitionGroup.Trim() + "\u001f" + plan.Rule.ParameterName.Trim() + "\u001f" +
        (plan.Definition?.Guid.ToString("D") ?? string.Empty);

    private void AssignSelected() => _assign(_allRows.Where(row => row.CanSelect && row.IsSelected).Select(row => row.Plan).ToArray());

    private void SetReadySelection(bool selected)
    {
        foreach (var row in _allRows.Where(row => row.CanSelect)) row.IsSelected = selected;
        SelectionChanged();
    }

    private void InvertReady()
    {
        foreach (var row in _allRows.Where(row => row.CanSelect)) row.IsSelected = !row.IsSelected;
        SelectionChanged();
    }

    private void ClearPlans()
    {
        _allRows = Array.Empty<ParameterPlanRowViewModel>();
        VisibleRows.Clear();
        StatusText = "Nguồn đã thay đổi; hãy Kiểm tra lại.";
        RaiseCounts();
        SelectionChanged();
    }

    private void ApplyFilter() => ParameterPlanFilter.Apply(_allRows, SearchText, StatusFilter, VisibleRows);

    private void SelectionChanged()
    {
        RaisePropertyChanged(nameof(CanAssign));
        AssignCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCounts()
    {
        RaisePropertyChanged(nameof(ReadyCount));
        RaisePropertyChanged(nameof(AlreadyBoundCount));
        RaisePropertyChanged(nameof(ReviewCount));
        RaisePropertyChanged(nameof(ErrorCount));
    }
}
