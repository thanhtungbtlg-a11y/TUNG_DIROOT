using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Updates;

public static class CellValueDiff
{
    public static IReadOnlyList<CellValueChange> Create(WorkbookRangeModel before, WorkbookRangeModel after)
    {
        if (before == null) throw new ArgumentNullException(nameof(before));
        if (after == null) throw new ArgumentNullException(nameof(after));
        var oldValues = before.Cells.ToDictionary(cell => cell.Address.ToA1(), cell => cell.DisplayText, StringComparer.OrdinalIgnoreCase);
        return after.Cells
            .Select(cell => new { Key = cell.Address.ToA1(), cell.DisplayText })
            .Where(cell => !oldValues.TryGetValue(cell.Key, out var old) || !StringComparer.Ordinal.Equals(old, cell.DisplayText))
            .Select(cell => new CellValueChange(cell.Key, oldValues.TryGetValue(cell.Key, out var old) ? old : string.Empty, cell.DisplayText))
            .ToArray();
    }
}
