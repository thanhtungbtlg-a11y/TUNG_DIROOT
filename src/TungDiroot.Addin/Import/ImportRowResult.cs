using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using TungDiroot.Core.Excel;

namespace TungDiroot.Addin.Import;

public sealed record ImportRowResult(
    bool Succeeded,
    string ResultCode,
    ElementId? ViewId,
    string? Message,
    IReadOnlyList<WorkbookReadWarning> Warnings)
{
    public static ImportRowResult Success(ElementId viewId, IReadOnlyList<WorkbookReadWarning> warnings) =>
        new ImportRowResult(true, "SUCCESS", viewId, null, warnings);

    public static ImportRowResult Failure(string code, Exception exception) =>
        new ImportRowResult(false, code, null, exception.Message, Array.Empty<WorkbookReadWarning>());
}
