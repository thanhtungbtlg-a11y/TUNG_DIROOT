using System;
using System.Collections.Generic;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;

namespace TungDiroot.Core.Updates;

public enum UpdateAction
{
    None,
    DataOnly,
    Recreate
}

public sealed record CellValueChange(string CellKey, string BeforeText, string AfterText);

public sealed record UpdatePlan(
    UpdateAction Action,
    WorkbookRangeModel After,
    IReadOnlyList<CellValueChange> Changes,
    string? ReasonCode)
{
    public static UpdatePlan NoChange(WorkbookRangeModel after) =>
        new(UpdateAction.None, after, Array.Empty<CellValueChange>(), null);
}

public sealed record UpdatePlanInput(
    bool SourceChanged,
    bool StructureCompatible,
    UpdateBehavior RequestedBehavior,
    WorkbookRangeModel Before,
    WorkbookRangeModel After,
    string? StructureReasonCode = null,
    ImportMode ImportMode = ImportMode.RevitTable);
