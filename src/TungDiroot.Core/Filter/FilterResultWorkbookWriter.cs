using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.Filter;

public sealed record FilterExportColumn(string ParameterStableId, string Header);

public static class FilterResultWorkbookWriter
{
    public const string ResultsSheet = "RESULTS";

    private static readonly string[] IdentityHeaders =
    {
        "ElementId", "UniqueId", "Category", "Family", "Type", "Level"
    };

    public static void Write(
        string path,
        IReadOnlyList<FilterMatch> matches,
        IReadOnlyList<FilterExportColumn> columns)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Đường dẫn Excel không hợp lệ.", nameof(path));
        if (matches == null) throw new ArgumentNullException(nameof(matches));
        if (columns == null) throw new ArgumentNullException(nameof(columns));
        ValidateColumns(columns);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        var view = new SheetView { WorkbookViewId = 0U };
        view.Append(new Pane
        {
            VerticalSplit = 1D,
            TopLeftCell = "A2",
            ActivePane = PaneValues.BottomLeft,
            State = PaneStateValues.Frozen
        });
        worksheetPart.Worksheet = new Worksheet(new SheetViews(view), data);
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = ResultsSheet
        });

        data.Append(CreateRow(1U, IdentityHeaders.Concat(columns.Select(column => column.Header)).ToArray()));
        for (var index = 0; index < matches.Count; index++)
        {
            var element = matches[index].Element;
            var values = new List<string>(IdentityHeaders.Length + columns.Count)
            {
                element.ElementId.ToString(CultureInfo.InvariantCulture),
                element.UniqueId,
                element.Category,
                element.Family,
                element.TypeName,
                element.Level ?? string.Empty
            };
            values.AddRange(columns.Select(column => DisplayValue(element.Values, column.ParameterStableId)));
            data.Append(CreateRow((uint)index + 2U, values));
        }

        var lastColumn = ColumnName(IdentityHeaders.Length + columns.Count);
        worksheetPart.Worksheet.Append(new AutoFilter
        {
            Reference = "A1:" + lastColumn + Math.Max(1, matches.Count + 1).ToString(CultureInfo.InvariantCulture)
        });
        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
    }

    private static void ValidateColumns(IReadOnlyList<FilterExportColumn> columns)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            if (column == null || string.IsNullOrWhiteSpace(column.ParameterStableId))
                throw new ArgumentException("Cột xuất phải có mã parameter.", nameof(columns));
            if (string.IsNullOrWhiteSpace(column.Header))
                throw new ArgumentException("Cột xuất phải có tiêu đề.", nameof(columns));
            if (!ids.Add(column.ParameterStableId))
                throw new ArgumentException("Danh sách cột xuất chứa parameter trùng nhau.", nameof(columns));
        }
    }

    private static string DisplayValue(IReadOnlyDictionary<string, FilterValue> values, string stableId)
    {
        if (!values.TryGetValue(stableId, out var value))
        {
            value = values.FirstOrDefault(pair =>
                string.Equals(pair.Key, stableId, StringComparison.OrdinalIgnoreCase)).Value;
        }
        if (value == null || !value.Exists) return string.Empty;
        return value.Display ?? value.Internal ?? string.Empty;
    }

    private static Row CreateRow(uint index, IReadOnlyList<string> values)
    {
        var row = new Row { RowIndex = index };
        for (var column = 0; column < values.Count; column++)
        {
            row.Append(new Cell
            {
                CellReference = ColumnName(column + 1) + index.ToString(CultureInfo.InvariantCulture),
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(values[column] ?? string.Empty))
            });
        }
        return row;
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }
        return name;
    }
}
