using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.Parameters;

public sealed record FamilyParameterOrderRow(
    string Key,
    string Name,
    string GroupKey,
    int CurrentOrderInGroup,
    int TargetOrderInGroup,
    bool IsShared,
    Guid? Guid,
    bool IsInstance);

public sealed record FamilyParameterOrderIssue(string Code, string Message, string? Key = null);

public sealed record FamilyParameterOrderChange(
    string Key,
    string Name,
    string GroupKey,
    int CurrentOrderInGroup,
    int TargetOrderInGroup);

public sealed record FamilyParameterOrderPlan(
    IReadOnlyList<string> OrderedKeys,
    IReadOnlyList<FamilyParameterOrderChange> Changes,
    IReadOnlyList<FamilyParameterOrderIssue> Issues)
{
    public bool IsReady => Issues.Count == 0;
}

public static class FamilyParameterOrderPlanner
{
    private static readonly StringComparer Keys = StringComparer.OrdinalIgnoreCase;

    public static FamilyParameterOrderPlan Plan(
        IReadOnlyList<FamilyParameterOrderRow> edited,
        IReadOnlyList<FamilyParameterOrderRow> current)
    {
        if (edited == null) throw new ArgumentNullException(nameof(edited));
        if (current == null) throw new ArgumentNullException(nameof(current));
        var issues = new List<FamilyParameterOrderIssue>();

        var duplicateKeys = edited.GroupBy(row => row.Key.Trim(), Keys).Where(group => group.Count() > 1).ToArray();
        foreach (var duplicate in duplicateKeys)
            issues.Add(new FamilyParameterOrderIssue("PARAMETER_KEY_DUPLICATE", "Parameter key bị lặp trong workbook.", duplicate.Key));

        var editedKeys = new HashSet<string>(edited.Select(row => row.Key.Trim()), Keys);
        var currentKeys = new HashSet<string>(current.Select(row => row.Key.Trim()), Keys);
        if (edited.Count != current.Count || !editedKeys.SetEquals(currentKeys))
            issues.Add(new FamilyParameterOrderIssue("PARAMETER_SET_CHANGED",
                "Workbook phải chứa đúng toàn bộ parameter hiện có của family, không thiếu và không thêm."));

        var currentByKey = current.GroupBy(row => row.Key.Trim(), Keys).ToDictionary(group => group.Key, group => group.First(), Keys);
        foreach (var row in edited)
        {
            if (!currentByKey.TryGetValue(row.Key.Trim(), out var original)) continue;
            if (!Keys.Equals(original.GroupKey.Trim(), row.GroupKey.Trim()))
                issues.Add(new FamilyParameterOrderIssue("PARAMETER_GROUP_CHANGED",
                    "ReOrdering không đổi parameter group; hãy dùng ParaManager.", row.Key));
            if (!Keys.Equals(original.Name.Trim(), row.Name.Trim()))
                issues.Add(new FamilyParameterOrderIssue("PARAMETER_NAME_CHANGED",
                    "ReOrdering không đổi tên parameter; hãy dùng ParaManager.", row.Key));
        }

        foreach (var group in edited.GroupBy(row => row.GroupKey.Trim(), Keys))
        {
            var targets = group.Select(row => row.TargetOrderInGroup).ToArray();
            if (targets.Distinct().Count() != targets.Length)
                issues.Add(new FamilyParameterOrderIssue("TARGET_ORDER_DUPLICATE",
                    "TARGET_ORDER_IN_GROUP bị trùng trong group " + group.Key + "."));
            var expected = Enumerable.Range(1, group.Count()).ToArray();
            if (!targets.OrderBy(value => value).SequenceEqual(expected))
                issues.Add(new FamilyParameterOrderIssue("TARGET_ORDER_NOT_CONTIGUOUS",
                    "TARGET_ORDER_IN_GROUP của " + group.Key + " phải đủ từ 1 đến " + group.Count() + "."));
        }

        if (issues.Count > 0)
            return new FamilyParameterOrderPlan(current.Select(row => row.Key).ToArray(), Array.Empty<FamilyParameterOrderChange>(), issues);

        var queues = edited.GroupBy(row => row.GroupKey.Trim(), Keys).ToDictionary(
            group => group.Key,
            group => new Queue<FamilyParameterOrderRow>(group.OrderBy(row => row.TargetOrderInGroup)), Keys);
        var ordered = current.Select(row => queues[row.GroupKey.Trim()].Dequeue().Key).ToArray();
        var changes = edited.Where(row => row.CurrentOrderInGroup != row.TargetOrderInGroup)
            .Select(row => new FamilyParameterOrderChange(row.Key, row.Name, row.GroupKey,
                row.CurrentOrderInGroup, row.TargetOrderInGroup)).ToArray();
        return new FamilyParameterOrderPlan(ordered, changes, Array.Empty<FamilyParameterOrderIssue>());
    }
}

public static class FamilyParameterOrderWorkbook
{
    public const string SheetName = "FAMILY_PARAMETER_ORDER";
    private static readonly string[] Headers =
    {
        "KEY", "NAME", "GROUP", "CURRENT_ORDER_IN_GROUP", "TARGET_ORDER_IN_GROUP", "IS_SHARED", "GUID", "BINDING", "STATUS"
    };

    public static void Write(string path, IReadOnlyList<FamilyParameterOrderRow> rows)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Đường dẫn Excel không hợp lệ.", nameof(path));
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook(new BookViews(new WorkbookView()));
        AddStyles(workbookPart);
        var sheets = workbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());
        var part = workbookPart.AddNewPart<WorksheetPart>();
        var view = new SheetView { WorkbookViewId = 0U, ShowGridLines = false };
        view.Append(new Pane { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen });
        var columns = new Columns(
            Column(1, 32), Column(2, 30), Column(3, 24), Column(4, 24), Column(5, 24),
            Column(6, 12), Column(7, 38), Column(8, 14), Column(9, 34));
        var data = new SheetData();
        part.Worksheet = new Worksheet(new SheetViews(view), columns, data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1U, Name = SheetName });
        data.Append(Row(1U, Headers.Select(value => (value, false)).ToArray(), true));

        for (var index = 0; index < rows.Count; index++)
        {
            var item = rows[index];
            data.Append(Row((uint)index + 2U, new[]
            {
                (item.Key, false), (item.Name, false), (item.GroupKey, false),
                (item.CurrentOrderInGroup.ToString(CultureInfo.InvariantCulture), true),
                (item.TargetOrderInGroup.ToString(CultureInfo.InvariantCulture), true),
                (item.IsShared ? "TRUE" : "FALSE", false), (item.Guid?.ToString("D") ?? string.Empty, false),
                (item.IsInstance ? "INSTANCE" : "TYPE", false), ("Chỉ chỉnh TARGET_ORDER_IN_GROUP", false)
            }, false));
        }
        part.Worksheet.Append(new AutoFilter { Reference = "A1:I" + Math.Max(1, rows.Count + 1) });
        part.Worksheet.Save();
        workbookPart.Workbook.Save();
    }

    public static IReadOnlyList<FamilyParameterOrderRow> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy workbook ReOrdering.", path);
        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("REORDER_WORKBOOK_INVALID");
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>()
            .SingleOrDefault(item => string.Equals(item.Name?.Value, SheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("REORDER_SHEET_MISSING: Thiếu worksheet " + SheetName + ".");
        var data = ((WorksheetPart)workbookPart.GetPartById(sheet.Id!)).Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidDataException("REORDER_SHEET_INVALID");
        var header = Values(workbookPart, data.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>().First(), Headers.Length);
        if (!header.SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("REORDER_HEADER_INVALID: Header đã bị đổi.");
        var result = new List<FamilyParameterOrderRow>();
        foreach (var row in data.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>().Skip(1))
        {
            var values = Values(workbookPart, row, Headers.Length);
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            var guid = string.IsNullOrWhiteSpace(values[6]) ? (Guid?)null
                : Guid.TryParse(values[6], out var parsed) ? parsed
                : throw new InvalidDataException("REORDER_GUID_INVALID: Dòng " + row.RowIndex + ".");
            result.Add(new FamilyParameterOrderRow(values[0].Trim(), values[1].Trim(), values[2].Trim(),
                Integer(values[3], "CURRENT_ORDER_IN_GROUP", row.RowIndex), Integer(values[4], "TARGET_ORDER_IN_GROUP", row.RowIndex),
                Boolean(values[5]), guid, string.Equals(values[7].Trim(), "INSTANCE", StringComparison.OrdinalIgnoreCase)));
        }
        return result;
    }

    private static DocumentFormat.OpenXml.Spreadsheet.Row Row(uint index, (string Value, bool Number)[] values, bool header)
    {
        var row = new DocumentFormat.OpenXml.Spreadsheet.Row { RowIndex = index, Height = header ? 25D : 20D, CustomHeight = true };
        for (var column = 0; column < values.Length; column++)
        {
            var cell = new Cell { CellReference = ColumnName(column + 1) + index, StyleIndex = header ? 1U : values[column].Number ? 3U : 2U };
            if (values[column].Number)
            {
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(values[column].Value);
            }
            else
            {
                cell.DataType = CellValues.InlineString;
                cell.InlineString = new InlineString(new Text(values[column].Value ?? string.Empty));
            }
            row.Append(cell);
        }
        return row;
    }

    private static string[] Values(WorkbookPart workbookPart, DocumentFormat.OpenXml.Spreadsheet.Row row, int count)
    {
        var result = Enumerable.Repeat(string.Empty, count).ToArray();
        foreach (var cell in row.Elements<Cell>())
        {
            var column = ColumnIndex(cell.CellReference?.Value);
            if (column < 0 || column >= count) continue;
            if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(cell.CellValue?.Text, out var index))
                result[column] = workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>()
                    .ElementAtOrDefault(index)?.InnerText ?? string.Empty;
            else result[column] = cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
        }
        return result;
    }

    private static int Integer(string value, string column, UInt32Value? row)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0) return result;
        throw new InvalidDataException("REORDER_NUMBER_INVALID: " + column + " dòng " + row + ".");
    }

    private static bool Boolean(string value) => value.Trim() == "1" || value.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase);
    private static Column Column(uint index, double width) => new() { Min = index, Max = index, Width = width, CustomWidth = true };

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var part = workbookPart.AddNewPart<WorkbookStylesPart>();
        var fonts = new Fonts(new Font(new FontSize { Val = 11D }, new FontName { Val = "Aptos" }),
            new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }, new FontSize { Val = 11D }, new FontName { Val = "Aptos" }));
        var fills = new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FF7030A0" }) { PatternType = PatternValues.Solid }));
        var borders = new Borders(new Border(), new Border(new LeftBorder { Style = BorderStyleValues.Thin },
            new RightBorder { Style = BorderStyleValues.Thin }, new TopBorder { Style = BorderStyleValues.Thin },
            new BottomBorder { Style = BorderStyleValues.Thin }, new DiagonalBorder()));
        var formats = new CellFormats(new CellFormat(),
            new CellFormat { FontId = 1U, FillId = 2U, BorderId = 1U, ApplyFont = true, ApplyFill = true, ApplyBorder = true },
            new CellFormat { BorderId = 1U, ApplyBorder = true },
            new CellFormat { BorderId = 1U, ApplyBorder = true, NumberFormatId = 1U, ApplyNumberFormat = true });
        part.Stylesheet = new Stylesheet(fonts, fills, borders, new CellStyleFormats(new CellFormat()), formats);
        part.Stylesheet.Save();
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return -1;
        var value = 0;
        foreach (var character in reference!)
        {
            if (!char.IsLetter(character)) break;
            value = value * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }
        return value - 1;
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0) { index--; name = (char)('A' + index % 26) + name; index /= 26; }
        return name;
    }
}
