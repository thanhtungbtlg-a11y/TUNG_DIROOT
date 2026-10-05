using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Links;
using TungDiroot.Presentation.Links;

namespace TungDiroot.Addin.Links;

public sealed class RevitTableGenLinkSource : ITableGenLinkSource
{
    private readonly Document _document;
    private readonly ILinkRegistry _registry;
    private readonly IWorkbookReader _reader;

    public RevitTableGenLinkSource(Document document, ILinkRegistry? registry = null, IWorkbookReader? reader = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _registry = registry ?? new RevitLinkRegistry();
        _reader = reader ?? new OpenXmlWorkbookReader();
    }

    public IReadOnlyList<TableGenLinkItem> Load()
    {
        var rows = new List<TableGenLinkItem>();
        foreach (var link in _registry.Load(_document).Links)
        {
            var status = LinkStatus.Ready;
            string? code = link.LastResultCode;
            string resolved;
            try
            {
                resolved = LinkPathResolver.ResolveStoredPath(link.SourcePath, _document.PathName);
                if (!File.Exists(resolved)) { status = LinkStatus.Missing; code = "FILE_MISSING"; }
                else
                {
                    var range = ExcelRangeReference.Parse(link.RegionReference);
                    var model = _reader.ReadRange(resolved, link.WorksheetName, range,
                        new RangeReadOptions(link.IncludeHiddenRowsAndColumns));
                    var current = SourceFingerprint.Create(resolved, link.WorksheetName, range, model);
                    if (SourceChangeDetector.Compare(link.LastFingerprint, current) == SourceChange.Changed) status = LinkStatus.Changed;
                }
            }
            catch
            {
                resolved = link.SourcePath.Value;
                status = LinkStatus.Error;
                code = "SOURCE_CHECK_FAILED";
            }
            var view = _document.GetElement(link.TargetViewUniqueId) as View;
            rows.Add(new TableGenLinkItem(link.LinkId, resolved, link.WorksheetName, link.RegionReference,
                view?.Name ?? "<view deleted>", link.ImportMode, link.TargetViewKind, link.Dpi,
                link.AutoSync, view == null ? LinkStatus.Error : status, view == null ? "VIEW_DELETED" : code,
                link.RendererEngine, link.LastRendererKind, link.UpdateBehavior));
        }
        return rows.AsReadOnly();
    }
}
