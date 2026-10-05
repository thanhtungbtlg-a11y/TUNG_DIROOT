using System;

namespace TungDiroot.Core.Links;

public sealed record LinkMigrationResult(LinkRecord Record, bool CanWrite, string? WarningCode);

public static class LinkSchemaMigrator
{
    public static LinkRecord ToCurrent(LinkRecord source)
    {
        var result = TryToCurrent(source);
        if (!result.CanWrite) throw new NotSupportedException(result.WarningCode);
        return result.Record;
    }

    public static LinkMigrationResult TryToCurrent(LinkRecord source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (source.SchemaVersion > LinkRecord.CurrentSchemaVersion)
            return new LinkMigrationResult(source, false, "SCHEMA_NEWER_THAN_ADDIN");
        if (source.SchemaVersion < 1)
            return new LinkMigrationResult(source, false, "SCHEMA_TOO_OLD");
        var dpi = Enum.IsDefined(typeof(TungDiroot.Core.Import.ImageDpi), source.Dpi) ? source.Dpi : TungDiroot.Core.Import.ImageDpi.Dpi300;
        var renderer = Enum.IsDefined(typeof(TungDiroot.Core.Import.ImageRendererEngine), source.RendererEngine)
            ? source.RendererEngine : TungDiroot.Core.Import.ImageRendererEngine.Automatic;
        var updateBehavior = Enum.IsDefined(typeof(TungDiroot.Core.Import.UpdateBehavior), source.UpdateBehavior)
            ? source.UpdateBehavior : TungDiroot.Core.Import.UpdateBehavior.Recreate;
        return new LinkMigrationResult(source with
        {
            SchemaVersion = LinkRecord.CurrentSchemaVersion,
            Dpi = dpi,
            RendererEngine = renderer,
            UpdateBehavior = updateBehavior
        }, true, null);
    }
}
