using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Import;

public sealed record WorkbookImportRequest(
    string FilePath,
    string WorksheetName,
    ExcelRangeReference Range,
    ImportMode Mode,
    TargetViewKind TargetView,
    UpdateBehavior UpdateBehavior,
    ImageDpi Dpi,
    bool IncludeHiddenRowsAndColumns,
    bool BlackAndWhite,
    string? RequestedViewName = null,
    ImageRendererEngine RendererEngine = ImageRendererEngine.Automatic,
    string? StyleProfileName = null);
