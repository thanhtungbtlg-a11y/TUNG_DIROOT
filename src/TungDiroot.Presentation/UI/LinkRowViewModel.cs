using System;
using TungDiroot.Core.Import;
using TungDiroot.Presentation.Links;

namespace TungDiroot.Presentation.UI;

public sealed class LinkRowViewModel : BindableBase
{
    private bool _isSelected;
    private bool _autoSync;

    public LinkRowViewModel(TableGenLinkItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        LinkId = item.LinkId;
        SourcePath = item.SourcePath;
        WorksheetName = item.WorksheetName;
        RegionReference = item.RegionReference;
        TargetViewName = item.TargetViewName;
        ImportMode = item.ImportMode;
        TargetViewKind = item.TargetViewKind;
        Dpi = item.Dpi;
        _autoSync = item.AutoSync;
        Status = item.Status;
        ResultCode = item.ResultCode;
        RendererEngine = item.RendererEngine;
        RendererDisplay = item.ImportMode == ImportMode.Image
            ? item.RendererEngine + " → " + (item.LastRendererKind ?? "chưa chạy")
            : "—";
        UpdateBehavior = item.UpdateBehavior;
    }

    public Guid LinkId { get; }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string SourcePath { get; }
    public string WorksheetName { get; }
    public string RegionReference { get; }
    public string TargetViewName { get; }
    public ImportMode ImportMode { get; }
    public TargetViewKind TargetViewKind { get; }
    public ImageDpi Dpi { get; }
    public bool AutoSync { get => _autoSync; set => SetProperty(ref _autoSync, value); }
    public LinkStatus Status { get; }
    public string? ResultCode { get; }
    public ImageRendererEngine RendererEngine { get; }
    public string RendererDisplay { get; }
    public UpdateBehavior UpdateBehavior { get; }
}
