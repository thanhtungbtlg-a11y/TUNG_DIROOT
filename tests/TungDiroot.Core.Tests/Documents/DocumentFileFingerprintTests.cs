using System;
using System.IO;
using TungDiroot.Core.Documents;
using Xunit;

namespace TungDiroot.Core.Tests.Documents;

public sealed class DocumentFileFingerprintTests
{
    [Fact]
    public void Content_changes_are_detected_even_when_length_is_unchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllText(path, "AAAA");
            var before = DocumentFileFingerprint.Create(path);
            File.WriteAllText(path, "BBBB");
            File.SetLastWriteTimeUtc(path, new DateTime(before.LastWriteUtcTicks, DateTimeKind.Utc));

            var after = DocumentFileFingerprint.Create(path);

            Assert.NotEqual(before.Sha256Hex, after.Sha256Hex);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
