using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TungDiroot.Core.Filter;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Presentation.Filter;

public sealed class FilterViewModel : BindableBase
{
    private static readonly string[] DefaultPalette =
    {
        "#1F77B4", "#FF7F0E", "#2CA02C", "#D62728", "#9467BD", "#17BECF", "#E377C2", "#7F7F7F"
    };

    private readonly Action<FilterQuery> _run;
    private readonly Action _cancel;
    private readonly Action<FilterResultAction, IReadOnlyList<FilterMatch>> _resultAction;
    private readonly Action<FilterProfile> _saveProfile;
    private readonly string _documentIdentity;
    private FilterScope _scope = FilterScope.WholeModel;
    private FilterJoin _join = FilterJoin.And;
    private bool _isBusy;
    private bool _isExternalActionPending;
    private bool _isCancellationRequested;
    private string _statusText = "Chọn phạm vi và điều kiện rồi bấm Chạy lọc.";
    private string _validationText = string.Empty;
    private int _processedCount;
    private int _totalCount;
    private string _profileName = "Bộ lọc mới";
    private FilterParameterDescriptor? _groupParameter;

    public FilterViewModel(
        Action<FilterQuery> run,
        Action cancel,
        Action<FilterResultAction, IReadOnlyList<FilterMatch>> resultAction,
        Action<FilterProfile> saveProfile,
        string documentIdentity)
    {
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
        _resultAction = resultAction ?? throw new ArgumentNullException(nameof(resultAction));
        _saveProfile = saveProfile ?? throw new ArgumentNullException(nameof(saveProfile));
        _documentIdentity = documentIdentity ?? string.Empty;
        RunCommand = new RelayCommand(Run, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy && !IsCancellationRequested);
        AddRuleCommand = new RelayCommand(AddRule, () => !IsBusy);
        RemoveRuleCommand = new RelayCommand(RemoveRule, _ => !IsBusy && Rules.Count > 1);
        SelectAllResultsCommand = new RelayCommand(SelectAllResults, () => !IsBusy && Results.Count > 0);
        ClearResultsSelectionCommand = new RelayCommand(ClearResultsSelection, () => !IsBusy && Results.Count > 0);
        SelectAndShowCommand = ActionCommand(FilterResultAction.SelectAndShow);
        TemporaryIsolateCommand = ActionCommand(FilterResultAction.TemporaryIsolate);
        TemporaryHideCommand = ActionCommand(FilterResultAction.TemporaryHide);
        ResetTemporaryIsolationCommand = new RelayCommand(
            () => _resultAction(FilterResultAction.ResetTemporaryIsolation, Array.Empty<FilterMatch>()),
            () => !IsBusy);
        SaveSelectionSetCommand = ActionCommand(FilterResultAction.SaveSelectionSet);
        ExportExcelCommand = ActionCommand(FilterResultAction.ExportExcel);
        VisualizeCommand = ActionCommand(FilterResultAction.Visualize);
        SaveProfileCommand = new RelayCommand(SaveProfile, () => !IsBusy);
        Rules.Add(new FilterRuleRow(InputChanged));
    }

    public ObservableCollection<FilterCategoryRow> Categories { get; } = new();
    public ObservableCollection<FilterNameRow> Families { get; } = new();
    public ObservableCollection<FilterNameRow> Types { get; } = new();
    public ObservableCollection<FilterParameterDescriptor> Parameters { get; } = new();
    public ObservableCollection<FilterRuleRow> Rules { get; } = new();
    public ObservableCollection<FilterResultRow> Results { get; } = new();

    public RelayCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand AddRuleCommand { get; }
    public RelayCommand RemoveRuleCommand { get; }
    public RelayCommand SelectAllResultsCommand { get; }
    public RelayCommand ClearResultsSelectionCommand { get; }
    public RelayCommand SelectAndShowCommand { get; }
    public RelayCommand TemporaryIsolateCommand { get; }
    public RelayCommand TemporaryHideCommand { get; }
    public RelayCommand ResetTemporaryIsolationCommand { get; }
    public RelayCommand SaveSelectionSetCommand { get; }
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand VisualizeCommand { get; }
    public RelayCommand SaveProfileCommand { get; }

    public FilterScope Scope
    {
        get => _scope;
        set { if (SetProperty(ref _scope, value)) InputChanged(); }
    }

    public FilterJoin Join
    {
        get => _join;
        set { if (SetProperty(ref _join, value)) InputChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RaisePropertyChanged(nameof(CanClose));
            RaiseCommandStates();
        }
    }

    public bool IsExternalActionPending
    {
        get => _isExternalActionPending;
        set
        {
            if (!SetProperty(ref _isExternalActionPending, value)) return;
            RaisePropertyChanged(nameof(CanClose));
        }
    }

    public bool IsCancellationRequested
    {
        get => _isCancellationRequested;
        private set
        {
            if (!SetProperty(ref _isCancellationRequested, value)) return;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    public string ValidationText
    {
        get => _validationText;
        private set => SetProperty(ref _validationText, value ?? string.Empty);
    }

    public int ProcessedCount
    {
        get => _processedCount;
        private set { if (SetProperty(ref _processedCount, value)) RaisePropertyChanged(nameof(ProgressText)); }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set { if (SetProperty(ref _totalCount, value)) RaisePropertyChanged(nameof(ProgressText)); }
    }

    public string ProgressText => TotalCount <= 0 ? string.Empty : $"{ProcessedCount:N0} / {TotalCount:N0}";
    public bool CanClose => !IsBusy && !IsExternalActionPending;

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value ?? string.Empty);
    }

    public FilterParameterDescriptor? GroupParameter
    {
        get => _groupParameter;
        set => SetProperty(ref _groupParameter, value);
    }

    public void SetCatalog(FilterCatalog catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        Categories.Clear();
        Families.Clear();
        Types.Clear();
        Parameters.Clear();
        foreach (var category in catalog.Categories) Categories.Add(new FilterCategoryRow(category, InputChanged));
        foreach (var family in catalog.FamilyNames) Families.Add(new FilterNameRow(family, InputChanged));
        foreach (var type in catalog.TypeNames) Types.Add(new FilterNameRow(type, InputChanged));
        foreach (var parameter in catalog.Parameters) Parameters.Add(parameter);
        if (Rules.Count == 0) Rules.Add(new FilterRuleRow(InputChanged));
        foreach (var rule in Rules)
        {
            var stableId = rule.SelectedParameter?.StableId;
            rule.SelectedParameter = Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.StableId, stableId, StringComparison.OrdinalIgnoreCase)) ??
                                     Parameters.FirstOrDefault();
        }
        var groupStableId = GroupParameter?.StableId;
        GroupParameter = Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.StableId, groupStableId, StringComparison.OrdinalIgnoreCase)) ??
                         Parameters.FirstOrDefault();
        StatusText = $"Đã nạp {Categories.Count} category và {Parameters.Count} parameter.";
        ValidationText = string.Empty;
    }

    public bool TryBuildQuery(out FilterQuery? query)
    {
        var rules = Rules.Select(row => row.ToRule()).ToArray();
        if (rules.Any(rule => rule == null))
        {
            ValidationText = "Mỗi điều kiện phải chọn một parameter.";
            query = null;
            return false;
        }
        query = new FilterQuery(
            Scope,
            Categories.Where(row => row.IsSelected).Select(row => row.Id).ToArray(),
            Families.Where(row => row.IsSelected).Select(row => row.Name).ToArray(),
            Types.Where(row => row.IsSelected).Select(row => row.Name).ToArray(),
            rules.Cast<FilterRule>().ToArray(),
            Join);
        var errors = FilterRuleEvaluator.Validate(query);
        if (errors.Count == 0)
        {
            ValidationText = string.Empty;
            return true;
        }
        ValidationText = errors.Any(error => error.IndexOf("OPERAND", StringComparison.Ordinal) >= 0)
            ? "Điều kiện có giá trị so sánh không hợp lệ. Số phải nhập theo dạng 12.5."
            : "Điều kiện lọc chưa hợp lệ: " + string.Join(", ", errors);
        query = null;
        return false;
    }

    public void SetProgress(int processed, int total)
    {
        ProcessedCount = Math.Max(0, processed);
        TotalCount = Math.Max(0, total);
        StatusText = $"Đang lọc {ProgressText} phần tử…";
    }

    public void SetResults(IReadOnlyList<FilterMatch> matches)
    {
        if (matches == null) throw new ArgumentNullException(nameof(matches));
        Results.Clear();
        foreach (var match in matches) Results.Add(new FilterResultRow(match, ResultSelectionChanged));
        IsBusy = false;
        IsCancellationRequested = false;
        ProcessedCount = TotalCount;
        StatusText = $"Đã tìm thấy {Results.Count:N0} phần tử phù hợp.";
        RaiseCommandStates();
    }

    public void CompleteCancelled()
    {
        IsBusy = false;
        IsCancellationRequested = false;
        StatusText = "Đã hủy bộ lọc tại điểm an toàn.";
    }

    public void Fail(string message)
    {
        IsBusy = false;
        IsCancellationRequested = false;
        ValidationText = message ?? string.Empty;
        StatusText = "Không thể hoàn tất bộ lọc.";
    }

    public bool IsCurrentDocument(string documentIdentity) =>
        string.Equals(_documentIdentity, documentIdentity ?? string.Empty, StringComparison.Ordinal);

    public bool RequestClose()
    {
        if (CanClose) return true;
        StatusText = "Đang chờ Revit xử lý; hãy Hủy và chờ dừng ở điểm an toàn trước khi đóng.";
        return false;
    }

    public FilterProfile? CreateProfile()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            ValidationText = "Hãy nhập tên profile.";
            return null;
        }
        if (!TryBuildQuery(out var query)) return null;
        return new FilterProfile(
            FilterProfile.CurrentSchemaVersion,
            ProfileName.Trim(),
            query!,
            GroupParameter?.StableId ?? string.Empty,
            DefaultPalette);
    }

    public void LoadProfile(FilterProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        ProfileName = profile.Name;
        Scope = profile.Query.Scope;
        Join = profile.Query.Join;
        foreach (var row in Categories) row.IsSelected = profile.Query.CategoryIds.Contains(row.Id);
        foreach (var row in Families) row.IsSelected = profile.Query.FamilyNames.Contains(row.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var row in Types) row.IsSelected = profile.Query.TypeNames.Contains(row.Name, StringComparer.OrdinalIgnoreCase);
        Rules.Clear();
        foreach (var rule in profile.Query.Rules)
        {
            var row = new FilterRuleRow(InputChanged)
            {
                SelectedParameter = Parameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.StableId, rule.ParameterStableId, StringComparison.OrdinalIgnoreCase)),
                SelectedOperator = rule.Operator,
                Operand = rule.Operand ?? string.Empty
            };
            Rules.Add(row);
        }
        if (Rules.Count == 0) Rules.Add(new FilterRuleRow(InputChanged));
        GroupParameter = Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.StableId, profile.GroupParameterStableId, StringComparison.OrdinalIgnoreCase));
        ValidationText = string.Empty;
        StatusText = $"Đã nạp profile “{ProfileName}”.";
        RaiseCommandStates();
    }

    private void Run()
    {
        if (!TryBuildQuery(out var query)) return;
        IsCancellationRequested = false;
        IsBusy = true;
        Results.Clear();
        ProcessedCount = 0;
        TotalCount = 0;
        StatusText = "Đang chuẩn bị dữ liệu lọc…";
        try
        {
            _run(query!);
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void Cancel()
    {
        IsCancellationRequested = true;
        StatusText = "Đã ghi nhận yêu cầu hủy; đang chờ điểm dừng an toàn…";
        _cancel();
    }

    private void AddRule()
    {
        var row = new FilterRuleRow(InputChanged);
        if (Parameters.Count > 0) row.SelectedParameter = Parameters[0];
        Rules.Add(row);
        RaiseCommandStates();
    }

    private void RemoveRule(object? parameter)
    {
        if (Rules.Count <= 1 || parameter is not FilterRuleRow row) return;
        Rules.Remove(row);
        RaiseCommandStates();
    }

    private void SelectAllResults()
    {
        foreach (var row in Results) row.IsSelected = true;
        ResultSelectionChanged();
    }

    private void ClearResultsSelection()
    {
        foreach (var row in Results) row.IsSelected = false;
        ResultSelectionChanged();
    }

    private RelayCommand ActionCommand(FilterResultAction action) =>
        new(() => _resultAction(action, SelectedMatches()), CanAct);

    private bool CanAct() => !IsBusy && Results.Any(row => row.IsSelected);

    private IReadOnlyList<FilterMatch> SelectedMatches() =>
        Results.Where(row => row.IsSelected).Select(row => row.Match).ToArray();

    private void SaveProfile()
    {
        var profile = CreateProfile();
        if (profile != null) _saveProfile(profile);
    }

    private void InputChanged()
    {
        ValidationText = string.Empty;
        RunCommand.RaiseCanExecuteChanged();
        SaveProfileCommand.RaiseCanExecuteChanged();
    }

    private void ResultSelectionChanged() => RaiseCommandStates();

    private void RaiseCommandStates()
    {
        RunCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        AddRuleCommand.RaiseCanExecuteChanged();
        RemoveRuleCommand.RaiseCanExecuteChanged();
        SelectAllResultsCommand.RaiseCanExecuteChanged();
        ClearResultsSelectionCommand.RaiseCanExecuteChanged();
        SelectAndShowCommand.RaiseCanExecuteChanged();
        TemporaryIsolateCommand.RaiseCanExecuteChanged();
        TemporaryHideCommand.RaiseCanExecuteChanged();
        ResetTemporaryIsolationCommand.RaiseCanExecuteChanged();
        SaveSelectionSetCommand.RaiseCanExecuteChanged();
        ExportExcelCommand.RaiseCanExecuteChanged();
        VisualizeCommand.RaiseCanExecuteChanged();
        SaveProfileCommand.RaiseCanExecuteChanged();
    }
}
