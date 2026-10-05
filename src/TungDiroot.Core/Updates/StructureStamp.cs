using System;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Updates;

public static class StructureStamp
{
    public static string Create(WorkbookRangeModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        return "R:" + string.Join(",", model.Rows.Select(row => row.Address)) +
            "|C:" + string.Join(",", model.Columns.Select(column => column.Address)) +
            "|M:" + string.Join(",", model.MergedRanges.Select(range => range.ToA1()).OrderBy(value => value, StringComparer.Ordinal));
    }
}
