using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TungDiroot.Core.Excel;

public enum RegionKind
{
    PrintArea,
    NamedRange,
    UsedRange
}

public sealed class WorkbookCatalog
{
    public WorkbookCatalog(
        IEnumerable<WorksheetCatalog> worksheets,
        IEnumerable<WorkbookRegion> regions)
    {
        Worksheets = new ReadOnlyCollection<WorksheetCatalog>(worksheets.ToArray());
        Regions = new ReadOnlyCollection<WorkbookRegion>(regions.ToArray());
    }

    public IReadOnlyList<WorksheetCatalog> Worksheets { get; }

    public IReadOnlyList<WorkbookRegion> Regions { get; }
}

public sealed record WorksheetCatalog(string Name);

public sealed record WorkbookRegion(
    RegionKind Kind,
    string WorksheetName,
    string? Name,
    ExcelRangeReference Reference);
