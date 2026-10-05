using System;
using TungDiroot.Core.DataLink;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Presentation.DataLink;

public sealed class DataLinkCategoryRow : BindableBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public DataLinkCategoryRow(DataLinkCategoryOption option, Action selectionChanged)
    {
        Option = option ?? throw new ArgumentNullException(nameof(option));
        _selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
    }

    public DataLinkCategoryOption Option { get; }
    public long Id => Option.Id;
    public string Name => Option.Name;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) _selectionChanged(); }
    }
}

public sealed class DataLinkParameterRow : BindableBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public DataLinkParameterRow(DataLinkParameterOption option, Action selectionChanged)
    {
        Option = option ?? throw new ArgumentNullException(nameof(option));
        _selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
    }

    public DataLinkParameterOption Option { get; }
    public string Name => Option.Name;
    public string ScopeDisplay => Option.Selection.IsType ? "Type" : "Instance";
    public string StorageKind => Option.StorageKind;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) _selectionChanged(); }
    }
}

public sealed class DataLinkPreviewRow : BindableBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public DataLinkPreviewRow(DataLinkCandidateChange change, Action selectionChanged)
    {
        Change = change ?? throw new ArgumentNullException(nameof(change));
        _selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
    }

    public DataLinkCandidateChange Change { get; }
    public bool CanSelect => Change.Status == DataLinkChangeStatus.Ready;
    public DataLinkChangeStatus Status => Change.Status;
    public string StatusDisplay => Change.Status switch
    {
        DataLinkChangeStatus.Ready => "Sẵn sàng",
        DataLinkChangeStatus.NoChange => "Không đổi",
        DataLinkChangeStatus.DifferentDocument => "Khác model",
        DataLinkChangeStatus.ElementMissing => "Thiếu element",
        DataLinkChangeStatus.ParameterMissing => "Thiếu parameter",
        DataLinkChangeStatus.ReadOnly => "Chỉ đọc",
        DataLinkChangeStatus.UnsupportedStorage => "Không hỗ trợ",
        DataLinkChangeStatus.InvalidValue => "Sai giá trị",
        DataLinkChangeStatus.StaleModelValue => "Revit đã đổi",
        DataLinkChangeStatus.TypeConflict => "Xung đột Type",
        DataLinkChangeStatus.NotEditable => "Không có quyền sửa",
        _ => Change.Status.ToString()
    };
    public string ElementUniqueId => Change.Row.ElementUniqueId;
    public long ElementId => Change.Row.ElementId;
    public string Category => Change.Row.Category;
    public string Family => Change.Row.Family;
    public string TypeName => Change.Row.TypeName;
    public string ParameterName => Change.Row.Parameter.Name;
    public string OriginalDisplay => Change.Row.OriginalDisplay;
    public string EditedDisplay => Change.Row.EditedDisplay;
    public string Message => Change.Message;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var allowed = CanSelect && value;
            if (!SetProperty(ref _isSelected, allowed)) return;
            _selectionChanged();
        }
    }
}
