using System;
using TungDiroot.Core.Links;

namespace TungDiroot.Core.Documents;

public sealed record DocumentLinkRecord(
    int SchemaVersion,
    Guid LinkId,
    string TargetViewUniqueId,
    StoredPath SourcePath,
    DocumentSourceKind SourceKind,
    string PageExpression,
    DocumentImportDpi Dpi,
    int Columns,
    string CachedPdfPath,
    DocumentFileFingerprint LastFingerprint,
    DateTimeOffset LastUpdatedUtc,
    string? LastResultCode)
{
    public const int CurrentSchemaVersion = 1;
}
