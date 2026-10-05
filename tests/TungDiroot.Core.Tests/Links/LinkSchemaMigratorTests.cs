using System;
using TungDiroot.Core.Import;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class LinkSchemaMigratorTests
{
    [Fact]
    public void V1_record_round_trips_without_data_loss()
    {
        var source = Sample(1);
        var migrated = LinkSchemaMigrator.ToCurrent(source);
        Assert.Equal(source.LinkId, migrated.LinkId);
        Assert.Equal(1, migrated.SchemaVersion);
        Assert.Equal("PCCC", migrated.WorksheetName);
        Assert.Null(migrated.StyleProfileName);
    }

    [Fact]
    public void Unsupported_future_schema_is_read_only()
    {
        var result = LinkSchemaMigrator.TryToCurrent(Sample(99));
        Assert.False(result.CanWrite);
        Assert.Equal("SCHEMA_NEWER_THAN_ADDIN", result.WarningCode);
    }

    [Fact]
    public void Invalid_optional_enum_values_fall_back_to_safe_defaults()
    {
        var source = Sample(1) with
        {
            Dpi = (ImageDpi)123,
            RendererEngine = (ImageRendererEngine)123,
            UpdateBehavior = (UpdateBehavior)123
        };

        var migrated = LinkSchemaMigrator.ToCurrent(source);

        Assert.Equal(ImageDpi.Dpi300, migrated.Dpi);
        Assert.Equal(ImageRendererEngine.Automatic, migrated.RendererEngine);
        Assert.Equal(UpdateBehavior.Recreate, migrated.UpdateBehavior);
    }

    private static LinkRecord Sample(int version) => new LinkRecord(
        version, Guid.Parse("11111111-1111-1111-1111-111111111111"), "view", new StoredPath(@"C:\A.xlsx", PathMode.Absolute),
        "PCCC", "A1:D4", ImportMode.RevitTable, TargetViewKind.Drafting, UpdateBehavior.Recreate, false,
        SourceFingerprint.Parse("1:1:" + new string('A', 64)), null, null);
}
