using System;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;

namespace TungDiroot.Core.Links;

public static class LinkDuplicateRequestFactory
{
    public static WorkbookImportRequest Create(LinkRecord link, string resolvedSourcePath, string currentViewName)
    {
        if (link == null) throw new ArgumentNullException(nameof(link));
        if (string.IsNullOrWhiteSpace(resolvedSourcePath)) throw new ArgumentException("Source path is required.", nameof(resolvedSourcePath));
        if (string.IsNullOrWhiteSpace(currentViewName)) throw new ArgumentException("View name is required.", nameof(currentViewName));
        return new WorkbookImportRequest(
            resolvedSourcePath,
            link.WorksheetName,
            ExcelRangeReference.Parse(link.RegionReference),
            link.ImportMode,
            link.TargetViewKind,
            link.UpdateBehavior,
            link.Dpi,
            link.IncludeHiddenRowsAndColumns,
            link.BlackAndWhite,
            currentViewName.Trim() + " - Copy",
            link.RendererEngine,
            link.StyleProfileName);
    }
}
