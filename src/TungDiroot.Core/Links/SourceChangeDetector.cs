using System;

namespace TungDiroot.Core.Links;

public enum SourceChange
{
    Unchanged,
    Changed
}

public static class SourceChangeDetector
{
    public static SourceChange Compare(SourceFingerprint before, SourceFingerprint after)
    {
        if (before == null)
        {
            throw new ArgumentNullException(nameof(before));
        }

        if (after == null)
        {
            throw new ArgumentNullException(nameof(after));
        }

        return string.Equals(before.Sha256Hex, after.Sha256Hex, StringComparison.OrdinalIgnoreCase)
            ? SourceChange.Unchanged
            : SourceChange.Changed;
    }
}
