using System;
using TungDiroot.Core.Import;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class LinkDuplicateRequestFactoryTests
{
    [Fact]
    public void Duplicate_request_preserves_import_configuration_but_uses_editable_copy_name()
    {
        var link = new LinkRecord(
            LinkRecord.CurrentSchemaVersion, Guid.NewGuid(), "view", new StoredPath(@"C:\Data\A.xlsx", PathMode.Absolute),
            "DATA", "B2:F9", ImportMode.Image, TargetViewKind.Legend, UpdateBehavior.Recreate, true,
            new SourceFingerprint(1, 2, new string('A', 64)), null, null,
            ImageDpi.Dpi600, true, false, "stamp", ImageRendererEngine.BuiltIn, "BuiltIn", "MEP");

        var request = LinkDuplicateRequestFactory.Create(link, @"C:\Data\A.xlsx", "Bảng thiết bị");

        Assert.Equal(@"C:\Data\A.xlsx", request.FilePath);
        Assert.Equal("DATA", request.WorksheetName);
        Assert.Equal("B2:F9", request.Range.ToA1());
        Assert.Equal(ImportMode.Image, request.Mode);
        Assert.Equal(TargetViewKind.Legend, request.TargetView);
        Assert.Equal(ImageDpi.Dpi600, request.Dpi);
        Assert.Equal("MEP", request.StyleProfileName);
        Assert.Equal("Bảng thiết bị - Copy", request.RequestedViewName);
    }
}
