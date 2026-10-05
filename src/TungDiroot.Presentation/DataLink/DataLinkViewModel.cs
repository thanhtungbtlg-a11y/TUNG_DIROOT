using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TungDiroot.Core.DataLink;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Presentation.DataLink;

public sealed class DataLinkViewModel : BindableBase
{
    private readonly Action<DataLinkQuery> _export;
    private readonly Action _import;
    private readonly Action<DataLinkChangePlan> _apply;
    private DataLinkDocumentStamp? _documentStamp;
    private bool _isBusy;
    private bool _isExternalActionPending;
    private string _statusText = "Chọn phạm vi và parameter để xuất dữ liệu.";
    private DataLinkScope _scope = DataLinkScope.WholeModel;

    public DataLinkViewModel(
        Action<DataLinkQuery> export,
        Action<DataLinkChangePlan> apply,
        Action? import = null)
    {
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _import = import ?? (() => { });
        ExportCommand = new RelayCommand(Export, () => CanExport);
        ImportCommand = new RelayCommand(() => _import(), () => CanImport);
        ApplyCommand = new RelayCommand(ApplySelected, () => CanApply);
        SelectAllReadyCommand = new RelayCommand(SelectAllReady, () => !IsBusy);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => !IsBusy);
    }

    public ObservableCollection<DataLinkPreviewRow> Rows { get; } = new();
    public ObservableCollection<DataLinkCategoryRow> Categories { get; } = new();
    public ObservableCollection<DataLinkParameterRow> Parameters { get; } = new();
    public ObservableCollection<long> SelectedCategoryIds { get; } = new();
    public ObservableCollection<DataLinkParameterSelection> SelectedParameters { get; } = new();
    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand SelectAllReadyCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }

    public DataLinkScope Scope
    {
        get => _scope;
        set => SetProperty(ref _scope, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RaiseCommandState();
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

    public bool CanExport => !IsBusy && (Parameters.Any(row => row.IsSelected) ||
        (Parameters.Count == 0 && SelectedParameters.Count > 0));
    public bool CanImport => !IsBusy;
    public bool CanApply => !IsBusy && Rows.Any(row => row.CanSelect && row.IsSelected);
    public bool CanClose => !IsExternalActionPending;

    public void SetCatalog(DataLinkCatalog catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        Categories.Clear();
        Parameters.Clear();
        foreach (var category in catalog.Categories) Categories.Add(new DataLinkCategoryRow(category, CatalogSelectionChanged));
        foreach (var parameter in catalog.Parameters) Parameters.Add(new DataLinkParameterRow(parameter, CatalogSelectionChanged));
        StatusText = $"Đã nạp {Categories.Count} category và {Parameters.Count} parameter.";
        CatalogSelectionChanged();
    }

    public void SetPlan(DataLinkChangePlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        _documentStamp = plan.DocumentStamp;
        Rows.Clear();
        foreach (var change in plan.Changes) Rows.Add(new DataLinkPreviewRow(change, SelectionChanged));
        StatusText = $"Đã kiểm tra {Rows.Count} giá trị; sẵn sàng {Rows.Count(row => row.CanSelect)}.";
        SelectionChanged();
    }

    public bool RequestClose()
    {
        if (CanClose) return true;
        StatusText = "Đang chờ Revit xử lý; chưa thể đóng cửa sổ.";
        return false;
    }

    private void Export()
    {
        var categories = Categories.Count > 0
            ? Categories.Where(row => row.IsSelected).Select(row => row.Id).ToArray()
            : SelectedCategoryIds.ToArray();
        var parameters = Parameters.Count > 0
            ? Parameters.Where(row => row.IsSelected).Select(row => row.Option.Selection).ToArray()
            : SelectedParameters.ToArray();
        _export(new DataLinkQuery(Scope, categories, parameters));
    }

    private void ApplySelected()
    {
        if (_documentStamp == null) return;
        var changes = Rows.Where(row => row.CanSelect && row.IsSelected).Select(row => row.Change).ToArray();
        _apply(new DataLinkChangePlan(_documentStamp, changes));
    }

    private void SelectAllReady()
    {
        foreach (var row in Rows) row.IsSelected = row.CanSelect;
        SelectionChanged();
    }

    private void ClearSelection()
    {
        foreach (var row in Rows) row.IsSelected = false;
        SelectionChanged();
    }

    private void SelectionChanged()
    {
        RaisePropertyChanged(nameof(CanApply));
        ApplyCommand.RaiseCanExecuteChanged();
    }

    private void CatalogSelectionChanged()
    {
        RaisePropertyChanged(nameof(CanExport));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCommandState()
    {
        RaisePropertyChanged(nameof(CanExport));
        RaisePropertyChanged(nameof(CanImport));
        RaisePropertyChanged(nameof(CanApply));
        ExportCommand.RaiseCanExecuteChanged();
        ImportCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
        SelectAllReadyCommand.RaiseCanExecuteChanged();
        ClearSelectionCommand.RaiseCanExecuteChanged();
    }
}
