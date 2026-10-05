using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Layout;

public static class TableLayoutPlanner
{
    public static TableLayoutPlan Create(WorkbookRangeModel model, LayoutOptions options)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var rows = model.Rows.OrderBy(row => row.Address).ToArray();
        var columns = model.Columns.OrderBy(column => column.Address).ToArray();
        var rowOffsets = BuildOffsets(rows.Select(row => row.HeightPoints));
        var columnWidths = columns.Select(column => Math.Max(1D, column.WidthCharacters * 7D)).ToArray();
        var columnOffsets = BuildOffsets(columnWidths);
        var rowIndex = rows.Select((row, index) => new { row.Address, Index = index }).ToDictionary(x => x.Address, x => x.Index);
        var columnIndex = columns.Select((column, index) => new { column.Address, Index = index }).ToDictionary(x => x.Address, x => x.Index);
        var texts = new List<TextPlacement>();
        var borders = new List<LinePlacement>();
        var fills = new List<FillPlacement>();

        foreach (var cell in model.Cells.OrderBy(cell => cell.Address.Row).ThenBy(cell => cell.Address.Column))
        {
            if (!rowIndex.TryGetValue(cell.Address.Row, out var row) || !columnIndex.TryGetValue(cell.Address.Column, out var column)) continue;
            var merge = model.MergedRanges.FirstOrDefault(candidate => Contains(candidate, cell.Address));
            if (merge != null && cell.Address != merge.Start) continue;
            var bounds = merge == null
                ? new LayoutRectangle(columnOffsets[column], rowOffsets[row], columnWidths[column], rows[row].HeightPoints)
                : BoundsFor(merge, rowIndex, columnIndex, rowOffsets, columnOffsets, rows, columnWidths);
            var style = options.BlackAndWhite ? BlackAndWhiteTransform.Apply(cell.Style, options.PreserveGrayscale) : cell.Style;
            if (!string.IsNullOrEmpty(cell.DisplayText)) texts.Add(new TextPlacement(cell.Address.ToA1(), cell.DisplayText, bounds, style, CellStyleKey.Create(cell.Style)));
            if (!string.Equals(style.Fill.Pattern, "none", StringComparison.OrdinalIgnoreCase) && style.Fill.ForegroundColor != null)
                fills.Add(new FillPlacement(bounds, style.Fill.ForegroundColor));
            AddBorders(borders, bounds, style.Border, cell.Style.Border, merge != null ? BorderRole.Outer : BorderRole.Inner);
        }

        return new TableLayoutPlan(
            texts,
            BorderSegmentCoalescer.Coalesce(borders),
            FillRectangleCoalescer.Coalesce(fills),
            columnWidths.Sum(),
            rows.Sum(row => row.HeightPoints),
            model.Warnings);
    }

    private static double[] BuildOffsets(IEnumerable<double> sizes)
    {
        var values = sizes.ToArray();
        var result = new double[values.Length];
        var offset = 0D;
        for (var index = 0; index < values.Length; index++) { result[index] = offset; offset += values[index]; }
        return result;
    }

    private static LayoutRectangle BoundsFor(
        ExcelRangeReference merge,
        IReadOnlyDictionary<int, int> rowIndex,
        IReadOnlyDictionary<int, int> columnIndex,
        IReadOnlyList<double> rowOffsets,
        IReadOnlyList<double> columnOffsets,
        IReadOnlyList<WorkbookRowModel> rows,
        IReadOnlyList<double> widths)
    {
        var startRow = rowIndex[merge.Start.Row];
        var endRow = rowIndex[merge.End.Row];
        var startColumn = columnIndex[merge.Start.Column];
        var endColumn = columnIndex[merge.End.Column];
        return new LayoutRectangle(
            columnOffsets[startColumn],
            rowOffsets[startRow],
            Enumerable.Range(startColumn, endColumn - startColumn + 1).Sum(index => widths[index]),
            Enumerable.Range(startRow, endRow - startRow + 1).Sum(index => rows[index].HeightPoints));
    }

    private static bool Contains(ExcelRangeReference range, ExcelCellAddress address) =>
        address.Column >= range.Start.Column && address.Column <= range.End.Column &&
        address.Row >= range.Start.Row && address.Row <= range.End.Row;

    private static void AddBorders(ICollection<LinePlacement> output, LayoutRectangle bounds, BorderModel border, BorderModel original, BorderRole role)
    {
        Add(output, bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y, border.Top, original.Top, role);
        Add(output, bounds.X, bounds.Y + bounds.Height, bounds.X + bounds.Width, bounds.Y + bounds.Height, border.Bottom, original.Bottom, role);
        Add(output, bounds.X, bounds.Y, bounds.X, bounds.Y + bounds.Height, border.Left, original.Left, role);
        Add(output, bounds.X + bounds.Width, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height, border.Right, original.Right, role);
    }

    private static void Add(ICollection<LinePlacement> output, double x1, double y1, double x2, double y2, BorderSideModel side, BorderSideModel original, BorderRole role)
    {
        if (!string.Equals(side.Style, "none", StringComparison.OrdinalIgnoreCase))
            output.Add(new LinePlacement(x1, y1, x2, y2, side.Style, side.Color, role, LineStyleKey.Create(original.Style, original.Color)));
    }
}
