using System;

namespace TungDiroot.Core.Import;

public sealed record ImportSizeEstimate(long CellCount, long EstimatedElementCount, bool RequiresConfirmation);

public static class ImportSizeEstimator
{
    public const long ConfirmationThreshold = 5000L;

    public static ImportSizeEstimate Estimate(int rows, int columns, ImportMode mode)
    {
        if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var cellCount = checked((long)rows * columns);
        var elementCount = mode == ImportMode.Image ? 2L : checked(cellCount * 3L + rows + columns);
        return new ImportSizeEstimate(cellCount, elementCount, mode == ImportMode.RevitTable && cellCount > ConfirmationThreshold);
    }
}
