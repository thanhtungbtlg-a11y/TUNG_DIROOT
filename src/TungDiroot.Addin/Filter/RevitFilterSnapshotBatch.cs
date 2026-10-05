using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Filter;

namespace TungDiroot.Addin.Filter;

public enum FilterBatchStatus
{
    Ready,
    Running,
    Cancelled,
    Completed,
    StaleDocument
}

public sealed record FilterBatchSlice(
    FilterBatchStatus Status,
    IReadOnlyList<FilterElementSnapshot> Snapshots,
    int ProcessedCount,
    int TotalCount,
    bool HasMore);

public sealed class RevitFilterSnapshotBatch
{
    public const int BatchSize = 500;
    private readonly long[] _elementIds;
    private int _offset;

    private RevitFilterSnapshotBatch(string expectedDocumentIdentity, long[] elementIds)
    {
        ExpectedDocumentIdentity = expectedDocumentIdentity;
        _elementIds = elementIds;
        Status = elementIds.Length == 0 ? FilterBatchStatus.Completed : FilterBatchStatus.Ready;
    }

    public string ExpectedDocumentIdentity { get; }
    public FilterBatchStatus Status { get; private set; }
    public int ProcessedCount => _offset;
    public int TotalCount => _elementIds.Length;

    public static RevitFilterSnapshotBatch Create(UIDocument uiDocument, FilterQuery query)
    {
        if (uiDocument == null) throw new ArgumentNullException(nameof(uiDocument));
        if (query == null) throw new ArgumentNullException(nameof(query));
        var document = uiDocument.Document;
        if (document.IsLinked) throw new InvalidOperationException("TUNG Filter không đọc trực tiếp linked document.");
        var categories = new HashSet<long>(query.CategoryIds);
        var ids = RevitFilterCatalogReader.Collect(uiDocument, query.Scope)
            .Where(element => element.Category != null)
            .Where(element => categories.Count == 0 || categories.Contains(element.Category.Id.Value))
            .Select(element => element.Id.Value)
            .Distinct()
            .ToArray();
        return new RevitFilterSnapshotBatch(RevitFilterDocumentIdentity.Create(document), ids);
    }

    public FilterBatchSlice ProcessNext(
        UIApplication application,
        FilterQuery query,
        Func<bool>? isCancellationRequested = null)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (Status is FilterBatchStatus.Cancelled or FilterBatchStatus.StaleDocument or FilterBatchStatus.Completed)
            return Slice(Array.Empty<FilterElementSnapshot>());
        if (isCancellationRequested?.Invoke() == true)
        {
            Status = FilterBatchStatus.Cancelled;
            return Slice(Array.Empty<FilterElementSnapshot>());
        }

        var uiDocument = application.ActiveUIDocument;
        if (uiDocument == null || !string.Equals(
                ExpectedDocumentIdentity,
                RevitFilterDocumentIdentity.Create(uiDocument.Document),
                StringComparison.Ordinal))
        {
            Status = FilterBatchStatus.StaleDocument;
            return Slice(Array.Empty<FilterElementSnapshot>());
        }

        Status = FilterBatchStatus.Running;
        var ids = _elementIds.Skip(_offset).Take(BatchSize).ToArray();
        var snapshots = new RevitFilterSnapshotProjector().Project(uiDocument.Document, ids, query);
        _offset += ids.Length;
        Status = _offset >= _elementIds.Length ? FilterBatchStatus.Completed : FilterBatchStatus.Ready;
        return Slice(snapshots);
    }

    public void Cancel()
    {
        if (Status != FilterBatchStatus.Completed && Status != FilterBatchStatus.StaleDocument)
            Status = FilterBatchStatus.Cancelled;
    }

    private FilterBatchSlice Slice(IReadOnlyList<FilterElementSnapshot> snapshots) =>
        new(Status, snapshots, ProcessedCount, TotalCount, Status == FilterBatchStatus.Ready);
}

internal static class RevitFilterDocumentIdentity
{
    public static string Create(Document document)
    {
        var identity = document.ProjectInformation?.UniqueId;
        if (string.IsNullOrWhiteSpace(identity)) identity = document.PathName;
        if (string.IsNullOrWhiteSpace(identity)) identity = document.Title;
        return identity ?? string.Empty;
    }
}
