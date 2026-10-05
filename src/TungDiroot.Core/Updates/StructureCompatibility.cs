using System;
using System.Linq;

namespace TungDiroot.Core.Updates;

public sealed record StructureCompatibilityResult(bool CanUpdateDataOnly, string? ReasonCode);

public static class StructureCompatibility
{
    public static StructureCompatibilityResult Compare(StructureSignature before, StructureSignature after)
    {
        if (before == null) throw new ArgumentNullException(nameof(before));
        if (after == null) throw new ArgumentNullException(nameof(after));
        if (before.RowCount != after.RowCount) return new StructureCompatibilityResult(false, "ROW_COUNT_CHANGED");
        if (before.ColumnCount != after.ColumnCount) return new StructureCompatibilityResult(false, "COLUMN_COUNT_CHANGED");
        if (!before.MergedRanges.SequenceEqual(after.MergedRanges, StringComparer.Ordinal))
            return new StructureCompatibilityResult(false, "MERGE_STRUCTURE_CHANGED");
        return new StructureCompatibilityResult(true, null);
    }
}
