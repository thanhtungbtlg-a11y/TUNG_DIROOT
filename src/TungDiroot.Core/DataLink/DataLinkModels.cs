using System.Collections.Generic;
using System.Linq;

namespace TungDiroot.Core.DataLink;

public enum DataLinkScope
{
    WholeModel,
    ActiveView,
    CurrentSelection
}

public enum DataLinkChangeStatus
{
    Ready,
    NoChange,
    DifferentDocument,
    ElementMissing,
    ParameterMissing,
    ReadOnly,
    UnsupportedStorage,
    InvalidValue,
    StaleModelValue,
    TypeConflict,
    NotEditable
}

public sealed record DataLinkDocumentStamp(string Identity);

public sealed record DataLinkQuery(
    DataLinkScope Scope,
    IReadOnlyList<long> CategoryIds,
    IReadOnlyList<DataLinkParameterSelection> Parameters);

public sealed record DataLinkParameterSelection(string StableId, bool IsType);

public sealed record DataLinkCategoryOption(long Id, string Name);

public sealed record DataLinkParameterOption(
    DataLinkParameterSelection Selection,
    string Name,
    string StorageKind);

public sealed record DataLinkCatalog(
    IReadOnlyList<DataLinkCategoryOption> Categories,
    IReadOnlyList<DataLinkParameterOption> Parameters);

public sealed record DataLinkParameterKey(
    string StableId,
    string Name,
    bool IsType,
    string StorageKind,
    string? SpecId);

public sealed record DataLinkElementRow(
    string ElementUniqueId,
    long ElementId,
    string? TypeUniqueId,
    string Category,
    string Family,
    string TypeName,
    DataLinkParameterKey Parameter,
    string OriginalInternal,
    string OriginalDisplay,
    string EditedDisplay,
    bool IsReadOnly = false,
    bool IncludeInImport = true);

public sealed record DataLinkWorkbook(
    int SchemaVersion,
    DataLinkDocumentStamp DocumentStamp,
    string CultureName,
    IReadOnlyList<DataLinkElementRow> Rows);

public sealed record DataLinkValueAddress(
    string ElementUniqueId,
    string ParameterStableId,
    bool IsType)
{
    public static DataLinkValueAddress From(DataLinkElementRow row) =>
        new(row.ElementUniqueId, row.Parameter.StableId, row.Parameter.IsType);
}

public sealed record DataLinkCurrentValue(
    bool ElementExists,
    bool ParameterExists,
    bool IsReadOnly,
    bool IsEditable,
    string StorageKind,
    string InternalValue)
{
    public static DataLinkCurrentValue Available(string storageKind, string internalValue) =>
        new(true, true, false, true, storageKind, internalValue);
}

public sealed record DataLinkCandidateChange(
    DataLinkElementRow Row,
    DataLinkChangeStatus Status,
    string? ParsedInternal,
    string Message);

public sealed record DataLinkChangePlan(DataLinkDocumentStamp DocumentStamp, IReadOnlyList<DataLinkCandidateChange> Changes)
{
    public IReadOnlyList<DataLinkCandidateChange> Ready =>
        Changes.Where(item => item.Status == DataLinkChangeStatus.Ready).ToArray();
}
