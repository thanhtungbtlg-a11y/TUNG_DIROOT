namespace TungDiroot.Core.Excel;

public sealed record CellModel(
    ExcelCellAddress Address,
    string DisplayText,
    string? Formula,
    CellStyleModel Style,
    bool HasCachedFormulaValue,
    string? RawNumericValue = null);
