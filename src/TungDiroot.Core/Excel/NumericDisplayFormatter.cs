using System;
using System.Globalization;
using System.Linq;
using TungDiroot.Core.Layout;

namespace TungDiroot.Core.Excel;

public static class NumericDisplayFormatter
{
    public static WorkbookRangeModel Apply(WorkbookRangeModel model, NumericDisplayOptions options)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (options.KeepExcelFormat) return model;
        var cells = model.Cells.Select(cell => cell with { DisplayText = Format(cell, options) }).ToArray();
        return new WorkbookRangeModel(
            model.Rows,
            model.Columns,
            cells,
            model.MergedRanges,
            model.Warnings,
            model.Drawings);
    }

    public static string Format(CellModel cell, NumericDisplayOptions options)
    {
        if (cell == null) throw new ArgumentNullException(nameof(cell));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (options.KeepExcelFormat || string.IsNullOrWhiteSpace(cell.RawNumericValue) || IsFormulaError(cell.DisplayText))
            return cell.DisplayText;
        if (!double.TryParse(cell.RawNumericValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return cell.DisplayText;

        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = options.DecimalSymbol;
        culture.NumberFormat.NumberGroupSeparator = options.ThousandsSeparator;
        var format = (options.UseThousandsSeparator ? "N" : "F") + options.DecimalPlaces.ToString(CultureInfo.InvariantCulture);
        return value.ToString(format, culture);
    }

    private static bool IsFormulaError(string value) =>
        value.StartsWith("#", StringComparison.Ordinal) && value.EndsWith("!", StringComparison.Ordinal) ||
        string.Equals(value, "#N/A", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "#NAME?", StringComparison.OrdinalIgnoreCase);
}
