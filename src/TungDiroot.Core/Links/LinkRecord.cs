using System;
using TungDiroot.Core.Import;

namespace TungDiroot.Core.Links;

public sealed record LinkRecord(
    int SchemaVersion,
    Guid LinkId,
    string TargetViewUniqueId,
    StoredPath SourcePath,
    string WorksheetName,
    string RegionReference,
    ImportMode ImportMode,
    TargetViewKind TargetViewKind,
    UpdateBehavior UpdateBehavior,
    bool AutoSync,
    SourceFingerprint LastFingerprint,
    DateTimeOffset? LastUpdatedUtc,
    string? LastResultCode,
    ImageDpi Dpi = ImageDpi.Dpi300,
    bool BlackAndWhite = false,
    bool IncludeHiddenRowsAndColumns = true,
    string? LastStructureStamp = null,
    ImageRendererEngine RendererEngine = ImageRendererEngine.Automatic,
    string? LastRendererKind = null,
    string? StyleProfileName = null)
{
    public const int CurrentSchemaVersion = 1;
}
