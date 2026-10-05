using System;
using System.IO;
using System.Security.Cryptography;

namespace TungDiroot.Core.Documents;

public sealed record DocumentFileFingerprint(long Length, long LastWriteUtcTicks, string Sha256Hex)
{
    public static DocumentFileFingerprint Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("SOURCE_PATH_REQUIRED", nameof(path));
        var file = new FileInfo(path);
        file.Refresh();
        if (!file.Exists) throw new FileNotFoundException("SOURCE_NOT_FOUND", path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sha = SHA256.Create();
        var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        file.Refresh();
        return new DocumentFileFingerprint(file.Length, file.LastWriteTimeUtc.Ticks, hash);
    }

    public bool SameContent(DocumentFileFingerprint? other) =>
        other != null && string.Equals(Sha256Hex, other.Sha256Hex, StringComparison.OrdinalIgnoreCase);
}
