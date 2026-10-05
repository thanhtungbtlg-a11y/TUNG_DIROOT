using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TungDiroot.Core.Excel;

public sealed record WorkbookRowModel(int Address, double HeightPoints, bool IsHidden);

public sealed record WorkbookColumnModel(int Address, double WidthCharacters, bool IsHidden);

public sealed record WorkbookReadWarning(string Code, string Message);

public sealed class WorkbookRangeModel
{
    private readonly IReadOnlyDictionary<string, CellModel> _cellsByAddress;

    public WorkbookRangeModel(
        IEnumerable<WorkbookRowModel> rows,
        IEnumerable<WorkbookColumnModel> columns,
        IEnumerable<CellModel> cells,
        IEnumerable<ExcelRangeReference> mergedRanges,
        IEnumerable<WorkbookReadWarning> warnings,
        WorkbookDrawingModel drawings)
    {
        Rows = new ReadOnlyList<WorkbookRowModel>(rows);
        Columns = new ReadOnlyList<WorkbookColumnModel>(columns);
        Cells = new ReadOnlyList<CellModel>(cells);
        MergedRanges = new ReadOnlyList<ExcelRangeReference>(mergedRanges);
        Warnings = new ReadOnlyList<WorkbookReadWarning>(warnings);
        Drawings = drawings ?? throw new ArgumentNullException(nameof(drawings));
        _cellsByAddress = new Dictionary<string, CellModel>(
            Cells.ToDictionary(cell => cell.Address.ToA1(), StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WorkbookRowModel> Rows { get; }

    public IReadOnlyList<WorkbookColumnModel> Columns { get; }

    public IReadOnlyList<CellModel> Cells { get; }

    public IReadOnlyList<ExcelRangeReference> MergedRanges { get; }

    public IReadOnlyList<WorkbookReadWarning> Warnings { get; }

    public WorkbookDrawingModel Drawings { get; }

    public CellModel Cell(string address)
    {
        if (address == null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        var normalized = ExcelCellAddress.Parse(address).ToA1();
        if (!_cellsByAddress.TryGetValue(normalized, out var cell))
        {
            throw new ArgumentOutOfRangeException(nameof(address), "The address is not part of this range.");
        }

        return cell;
    }
}

internal sealed class ReadOnlyList<T> : IReadOnlyList<T>
{
    private readonly T[] _items;

    public ReadOnlyList(IEnumerable<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        _items = items.ToArray();
    }

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)_items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
