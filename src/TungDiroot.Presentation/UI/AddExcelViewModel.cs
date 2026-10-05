using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.RevitPlanning;

namespace TungDiroot.Presentation.UI;

public sealed record ValidationMessage(string Code, string Message);

public sealed class AddExcelRowViewModel : BindableBase
{
    private bool _isSelected = true;
    private string? _styleProfileName;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string FilePath { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public string RangeReference { get; set; } = "A1:A1";
    public string RequestedViewName { get; set; } = string.Empty;
    public ImportMode ImportMode { get; set; } = ImportMode.RevitTable;
    public TargetViewKind TargetViewKind { get; set; } = TargetViewKind.Drafting;
    public bool LegendSeedAvailable { get; set; }
    public ImageDpi Dpi { get; set; } = ImageDpi.Dpi300;
    public ImageRendererEngine RendererEngine { get; set; } = ImageRendererEngine.Automatic;
    public UpdateBehavior UpdateBehavior { get; set; } = UpdateBehavior.Recreate;
    public bool IncludeHiddenRowsAndColumns { get; set; } = true;
    public bool BlackAndWhite { get; set; }
    public string? StyleProfileName { get => _styleProfileName; set => SetProperty(ref _styleProfileName, value); }
    public int EstimatedRows { get; set; }
    public int EstimatedColumns { get; set; }
}

public sealed class AddExcelViewModel : BindableBase
{
    private readonly HashSet<AddExcelRowViewModel> _observedRows = new();
    private bool _isImporting;
    private bool _isPreviewing;
    private AddExcelRowViewModel? _selectedRow;
    private bool _suppressSelectionNotifications;

    public AddExcelViewModel(IEnumerable<AddExcelRowViewModel>? rows = null)
    {
        Rows = new ObservableCollection<AddExcelRowViewModel>(rows ?? Array.Empty<AddExcelRowViewModel>());
        Rows.CollectionChanged += OnRowsChanged;
        SyncRowSubscriptions();
    }

    public ObservableCollection<AddExcelRowViewModel> Rows { get; }
    public int SelectedCount => Rows.Count(row => row.IsSelected);
    public string SelectionSummary => $"Đã chọn {SelectedCount}/{Rows.Count}";
    public bool CanImport => SelectedCount > 0 && !IsImporting;
    public bool CanPreview => SelectedRow != null && !IsImporting && !IsPreviewing;
    public AddExcelRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetProperty(ref _selectedRow, value)) return;
            RaisePropertyChanged(nameof(CanPreview));
        }
    }
    public bool IsPreviewing
    {
        get => _isPreviewing;
        set
        {
            if (!SetProperty(ref _isPreviewing, value)) return;
            RaisePropertyChanged(nameof(CanPreview));
        }
    }
    public bool IsImporting
    {
        get => _isImporting;
        set
        {
            if (!SetProperty(ref _isImporting, value)) return;
            RaisePropertyChanged(nameof(CanImport));
            RaisePropertyChanged(nameof(CanPreview));
        }
    }

    public void SetAllSelected(bool selected)
    {
        _suppressSelectionNotifications = true;
        try
        {
            foreach (var row in Rows) row.IsSelected = selected;
        }
        finally
        {
            _suppressSelectionNotifications = false;
            RaiseSelectionStateChanged();
        }
    }

    public IReadOnlyList<ValidationMessage> Validate()
    {
        var messages = Rows.Where(row => row.IsSelected).SelectMany(ValidateRow).ToList();
        messages.AddRange(Rows.Where(row => row.IsSelected && !string.IsNullOrWhiteSpace(row.RequestedViewName))
            .GroupBy(row => row.RequestedViewName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new ValidationMessage("DUPLICATE_VIEW_NAME", $"Tên view bị trùng: {group.Key}")));
        return messages.AsReadOnly();
    }

    public IReadOnlyList<WorkbookImportRequest> BuildRequests()
    {
        var errors = Validate();
        if (errors.Count > 0) throw new InvalidOperationException(errors[0].Code);
        return Rows.Where(row => row.IsSelected).Select(row => new WorkbookImportRequest(
            row.FilePath,
            row.WorksheetName,
            ExcelRangeReference.Parse(row.RangeReference),
            row.ImportMode,
            row.TargetViewKind,
            row.UpdateBehavior,
            row.Dpi,
            row.IncludeHiddenRowsAndColumns,
            row.BlackAndWhite,
            row.RequestedViewName,
            row.RendererEngine,
            row.StyleProfileName)).ToArray();
    }

    public static IReadOnlyList<ValidationMessage> ValidateRow(AddExcelRowViewModel row)
    {
        if (row == null) throw new ArgumentNullException(nameof(row));
        var messages = new List<ValidationMessage>();
        if (string.IsNullOrWhiteSpace(row.FilePath)) messages.Add(new ValidationMessage("FILE_REQUIRED", "Hãy chọn file Excel."));
        if (string.IsNullOrWhiteSpace(row.WorksheetName)) messages.Add(new ValidationMessage("WORKSHEET_REQUIRED", "Hãy chọn worksheet."));
        try { ExcelRangeReference.Parse(row.RangeReference); } catch (ArgumentException) { messages.Add(new ValidationMessage("RANGE_INVALID", "Vùng Excel không hợp lệ.")); }
        var viewPlan = ViewCreationPlanner.Plan(new ViewCreationRequest(row.ImportMode, row.TargetViewKind, row.LegendSeedAvailable, row.RequestedViewName));
        if (viewPlan.ErrorCode != null) messages.Add(new ValidationMessage(viewPlan.ErrorCode, viewPlan.ErrorCode));
        if (ImportSizeEstimator.Estimate(row.EstimatedRows, row.EstimatedColumns, row.ImportMode).RequiresConfirmation)
            messages.Add(new ValidationMessage("LARGE_REVIT_TABLE_CONFIRMATION_REQUIRED", "Bảng Revit vượt 5.000 ô."));
        return messages.AsReadOnly();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncRowSubscriptions();
        RaiseSelectionStateChanged();
    }

    private void SyncRowSubscriptions()
    {
        foreach (var row in _observedRows.Where(row => !Rows.Contains(row)).ToArray())
        {
            row.PropertyChanged -= OnRowPropertyChanged;
            _observedRows.Remove(row);
        }
        foreach (var row in Rows.Where(row => !_observedRows.Contains(row)))
        {
            row.PropertyChanged += OnRowPropertyChanged;
            _observedRows.Add(row);
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddExcelRowViewModel.IsSelected) && !_suppressSelectionNotifications)
            RaiseSelectionStateChanged();
    }

    private void RaiseSelectionStateChanged()
    {
        RaisePropertyChanged(nameof(SelectedCount));
        RaisePropertyChanged(nameof(SelectionSummary));
        RaisePropertyChanged(nameof(CanImport));
    }
}
