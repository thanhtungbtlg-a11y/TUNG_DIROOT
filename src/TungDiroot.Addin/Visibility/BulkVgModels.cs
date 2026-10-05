using System.Collections.Generic;
using TungDiroot.Core.Visibility;

namespace TungDiroot.Addin.Visibility;

public enum BulkVgItemKind
{
    Category,
    Filter
}

public sealed record BulkVgViewDescriptor(
    long Id,
    string DisplayName,
    BulkVgTargetKind Kind,
    IReadOnlyList<long> ViewportViewIds);

public sealed record BulkVgCatalog(
    IReadOnlyList<BulkVgViewDescriptor> Sources,
    IReadOnlyList<BulkVgViewDescriptor> Targets);

public sealed record BulkVgItemDescriptor(long Id, BulkVgItemKind Kind, string Name);
public sealed record BulkVgSelectedItem(long Id, BulkVgItemKind Kind, string Name);
public sealed record BulkVgModeDescriptor(BulkVgApplyMode Mode, string DisplayName, string Description);

public sealed record BulkVgRequest(
    long SourceViewId,
    IReadOnlyList<BulkVgTargetSelection> Targets,
    IReadOnlyList<BulkVgSelectedItem> Items,
    BulkVgApplyOptions Options,
    BulkVgApplyMode Mode);

public sealed record BulkVgPreviewResult(string TargetName, string ItemName, string Differences, string Status);
public sealed record BulkVgApplyResult(string TargetName, bool Succeeded, string Message, int SkippedFilters = 0);
