using System;
using System.Collections.Generic;
using TungDiroot.Core.Import;

namespace TungDiroot.Presentation.Links;

public enum LinkStatus
{
    Ready,
    Changed,
    Missing,
    Error
}

public sealed record TableGenLinkItem(
    Guid LinkId,
    string SourcePath,
    string WorksheetName,
    string RegionReference,
    string TargetViewName,
    ImportMode ImportMode,
    TargetViewKind TargetViewKind,
    ImageDpi Dpi,
    bool AutoSync,
    LinkStatus Status,
    string? ResultCode,
    ImageRendererEngine RendererEngine = ImageRendererEngine.Automatic,
    string? LastRendererKind = null,
    UpdateBehavior UpdateBehavior = UpdateBehavior.Recreate);

public interface ITableGenLinkSource
{
    IReadOnlyList<TableGenLinkItem> Load();
}
