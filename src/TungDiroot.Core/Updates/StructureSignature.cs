using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Updates;

public sealed class StructureSignature : IEquatable<StructureSignature>
{
    public StructureSignature(int rowCount, int columnCount, IEnumerable<string> mergedRanges)
    {
        if (rowCount < 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
        if (columnCount < 0) throw new ArgumentOutOfRangeException(nameof(columnCount));
        RowCount = rowCount;
        ColumnCount = columnCount;
        MergedRanges = new ReadOnlyList<string>((mergedRanges ?? throw new ArgumentNullException(nameof(mergedRanges)))
            .Select(range => ExcelRangeReference.Parse(range).ToA1())
            .OrderBy(range => range, StringComparer.Ordinal));
    }

    public int RowCount { get; }
    public int ColumnCount { get; }
    public IReadOnlyList<string> MergedRanges { get; }

    public static StructureSignature From(WorkbookRangeModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        return new StructureSignature(model.Rows.Count, model.Columns.Count, model.MergedRanges.Select(range => range.ToA1()));
    }

    public bool Equals(StructureSignature? other) => other != null &&
        RowCount == other.RowCount && ColumnCount == other.ColumnCount &&
        MergedRanges.SequenceEqual(other.MergedRanges, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as StructureSignature);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (RowCount * 397) ^ ColumnCount;
            foreach (var merge in MergedRanges) hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(merge);
            return hash;
        }
    }
}
