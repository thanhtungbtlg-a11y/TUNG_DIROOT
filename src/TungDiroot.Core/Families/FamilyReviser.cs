using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.Families;

public enum FamilyReviserAction { RenameFamily, RenameType, Skip }
public enum FamilyReviserPreviewStatus { Ready, NoChange, Skipped, Invalid, Conflict }

public sealed record FamilyReviserRow(
    bool Apply,
    FamilyReviserAction Action,
    string UniqueId,
    string Category,
    string CurrentFamily,
    string TargetFamily,
    string CurrentType,
    string TargetType,
    bool IsNested,
    bool IsInPlace,
    bool IsEditable);

public sealed record FamilyReviserPreviewItem(
    int ExcelRow,
    FamilyReviserRow Row,
    FamilyReviserRow? Current,
    FamilyReviserPreviewStatus Status,
    string Code,
    string Message);

public sealed record FamilyReviserPlan(IReadOnlyList<FamilyReviserPreviewItem> Items)
{
    public int ReadyCount => Items.Count(item => item.Status == FamilyReviserPreviewStatus.Ready);
    public bool HasErrors => Items.Any(item => item.Status is FamilyReviserPreviewStatus.Invalid or FamilyReviserPreviewStatus.Conflict);
}

public static class FamilyReviserPlanner
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;
    private static readonly char[] InvalidNameCharacters = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

    public static FamilyReviserPlan RevalidateConfirmed(FamilyReviserPlan confirmed,
        IReadOnlyList<FamilyReviserRow> current, string? namingPattern)
    {
        if (confirmed == null) throw new ArgumentNullException(nameof(confirmed));
        var approved = confirmed.Items.Where(item => item.Status == FamilyReviserPreviewStatus.Ready).ToArray();
        var checkedPlan = Plan(approved.Select(item => item.Row).ToArray(), current, namingPattern);
        return new FamilyReviserPlan(checkedPlan.Items.Select((item, index) =>
            item with { ExcelRow = approved[index].ExcelRow }).ToArray());
    }

    public static FamilyReviserPlan Plan(
        IReadOnlyList<FamilyReviserRow> edited,
        IReadOnlyList<FamilyReviserRow> current,
        string? namingPattern)
    {
        if (edited == null) throw new ArgumentNullException(nameof(edited));
        if (current == null) throw new ArgumentNullException(nameof(current));

        Regex? namingRule = null;
        string? regexError = null;
        if (!string.IsNullOrWhiteSpace(namingPattern))
        {
            try { namingRule = new Regex(namingPattern!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException exception) { regexError = exception.Message; }
        }

        var currentById = current.GroupBy(row => row.UniqueId, Names)
            .ToDictionary(group => group.Key, group => group.First(), Names);
        var currentFamilies = current.Where(row => row.Action == FamilyReviserAction.RenameFamily).ToArray();
        var currentTypes = current.Where(row => row.Action == FamilyReviserAction.RenameType).ToArray();
        var active = edited.Where(IsActive).ToArray();
        var duplicateIdentities = new HashSet<string>(active.GroupBy(row => row.UniqueId, Names)
            .Where(group => group.Count() > 1).Select(group => group.Key), Names);

        var duplicateFamilyTargets = new HashSet<string>(active.Where(row => row.Action == FamilyReviserAction.RenameFamily)
            .GroupBy(row => Clean(row.TargetFamily), Names).Where(group => group.Key.Length > 0 && group.Count() > 1)
            .Select(group => group.Key), Names);
        var duplicateTypeTargets = new HashSet<string>(active.Where(row => row.Action == FamilyReviserAction.RenameType)
            .GroupBy(row => Clean(row.CurrentFamily) + "\u001f" + Clean(row.TargetType), Names)
            .Where(group => group.Count() > 1).Select(group => group.Key), Names);

        var items = new List<FamilyReviserPreviewItem>();
        for (var index = 0; index < edited.Count; index++)
        {
            var row = edited[index];
            var excelRow = index + 2;
            if (!IsActive(row))
            {
                items.Add(Item(excelRow, row, null, FamilyReviserPreviewStatus.Skipped, "SKIPPED", "Dòng không được chọn Apply."));
                continue;
            }

            if (duplicateIdentities.Contains(row.UniqueId))
            {
                items.Add(Item(excelRow, row, null, FamilyReviserPreviewStatus.Conflict, "IDENTITY_DUPLICATE",
                    "Một Family hoặc Type có nhiều dòng Apply. Chỉ chọn một dòng cho mỗi UNIQUE_ID."));
                continue;
            }

            if (!currentById.TryGetValue(row.UniqueId, out var original))
            {
                items.Add(Item(excelRow, row, null, FamilyReviserPreviewStatus.Conflict, "ELEMENT_STALE", "Family hoặc type không còn tồn tại trong model."));
                continue;
            }
            if (row.Action != original.Action)
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Invalid, "ACTION_CHANGED", "Không được đổi ACTION của dòng đã xuất."));
                continue;
            }
            if (!Names.Equals(Clean(row.CurrentFamily), Clean(original.CurrentFamily)) ||
                !Names.Equals(Clean(row.CurrentType), Clean(original.CurrentType)))
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Conflict, "CURRENT_NAME_STALE", "Tên hiện tại trong workbook không còn khớp model."));
                continue;
            }
            if (row.Action == FamilyReviserAction.RenameFamily && original.IsInPlace)
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Invalid, "INPLACE_FAMILY_RENAME_BLOCKED", "Không đổi tên in-place family bằng FamilyReviser."));
                continue;
            }
            if (!original.IsEditable)
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Invalid, "ELEMENT_NOT_EDITABLE", "Family hoặc type không cho phép đổi tên."));
                continue;
            }
            if (regexError != null)
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Invalid, "NAMING_RULE_INVALID", "Regex quy ước tên không hợp lệ: " + regexError));
                continue;
            }

            var target = row.Action == FamilyReviserAction.RenameFamily ? row.TargetFamily : row.TargetType;
            var invalidCode = ValidateName(target);
            if (invalidCode != null)
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Invalid, invalidCode, "Tên đích rỗng, quá dài, có khoảng trắng thừa hoặc chứa ký tự cấm."));
                continue;
            }
            if (namingRule != null && !namingRule.IsMatch(target))
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Invalid, "NAMING_RULE_MISMATCH", "Tên đích không khớp quy ước công ty."));
                continue;
            }
            var currentName = row.Action == FamilyReviserAction.RenameFamily ? original.CurrentFamily : original.CurrentType;
            if (Names.Equals(Clean(target), Clean(currentName)))
            {
                items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.NoChange, "NO_CHANGE", "Tên đích giống tên hiện tại."));
                continue;
            }

            if (row.Action == FamilyReviserAction.RenameFamily)
            {
                if (duplicateFamilyTargets.Contains(Clean(target)) || currentFamilies.Any(other =>
                        !Names.Equals(other.UniqueId, row.UniqueId) && Names.Equals(Clean(other.CurrentFamily), Clean(target))))
                {
                    items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Conflict, "FAMILY_NAME_DUPLICATE", "Tên family đích đã tồn tại hoặc bị lặp trong workbook."));
                    continue;
                }
            }
            else
            {
                var targetKey = Clean(row.CurrentFamily) + "\u001f" + Clean(target);
                if (duplicateTypeTargets.Contains(targetKey) || currentTypes.Any(other =>
                        !Names.Equals(other.UniqueId, row.UniqueId) &&
                        Names.Equals(Clean(other.CurrentFamily), Clean(original.CurrentFamily)) &&
                        Names.Equals(Clean(other.CurrentType), Clean(target))))
                {
                    items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Conflict, "TYPE_NAME_DUPLICATE", "Tên type đích đã tồn tại trong family hoặc bị lặp trong workbook."));
                    continue;
                }
            }

            items.Add(Item(excelRow, row, original, FamilyReviserPreviewStatus.Ready, "READY", "Sẵn sàng đổi tên."));
        }
        return new FamilyReviserPlan(items);
    }

    private static bool IsActive(FamilyReviserRow row) => row.Apply && row.Action != FamilyReviserAction.Skip;
    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    private static string? ValidateName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value!.Length > 255) return "NAME_INVALID";
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)) return "NAME_INVALID_WHITESPACE";
        if (value.IndexOfAny(InvalidNameCharacters) >= 0 || value.Any(char.IsControl)) return "NAME_INVALID_CHARACTERS";
        return null;
    }

    private static FamilyReviserPreviewItem Item(int row, FamilyReviserRow edited, FamilyReviserRow? current,
        FamilyReviserPreviewStatus status, string code, string message) => new(row, edited, current, status, code, message);
}

public static class FamilyReviserWorkbook
{
    public const string SheetName = "FAMILY_REVISER";
    private static readonly string[] Headers =
    {
        "APPLY", "ACTION", "UNIQUE_ID", "CATEGORY", "CURRENT_FAMILY", "TARGET_FAMILY",
        "CURRENT_TYPE", "TARGET_TYPE", "IS_NESTED", "IS_IN_PLACE", "IS_EDITABLE", "STATUS"
    };

    public static void Write(string path, IReadOnlyList<FamilyReviserRow> rows)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Đường dẫn workbook không hợp lệ.", nameof(path));
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook(new BookViews(new WorkbookView()));
        AddStyles(workbookPart);
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var view = new SheetView { WorkbookViewId = 0U, ShowGridLines = false };
        view.Append(new Pane { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen });
        var columns = new Columns(
            Column(1, 10), Column(2, 20), Column(3, 42), Column(4, 24), Column(5, 30), Column(6, 30),
            Column(7, 30), Column(8, 30), Column(9, 12), Column(10, 12), Column(11, 12), Column(12, 34));
        var data = new SheetData();
        worksheetPart.Worksheet = new Worksheet(new SheetViews(view), columns, data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1U, Name = SheetName });
        data.Append(CreateRow(1U, Headers, true));
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            data.Append(CreateRow((uint)index + 2U, new[]
            {
                row.Apply ? "TRUE" : "FALSE", ActionText(row.Action), row.UniqueId, row.Category,
                row.CurrentFamily, row.TargetFamily, row.CurrentType, row.TargetType,
                row.IsNested ? "TRUE" : "FALSE", row.IsInPlace ? "TRUE" : "FALSE",
                row.IsEditable ? "TRUE" : "FALSE", "Chỉ chỉnh APPLY và TARGET_*"
            }, false));
        }
        worksheetPart.Worksheet.Append(new AutoFilter { Reference = "A1:L" + Math.Max(1, rows.Count + 1) });
        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
    }

    public static IReadOnlyList<FamilyReviserRow> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy workbook FamilyReviser.", path);
        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("FAMILY_REVISER_WORKBOOK_INVALID");
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>()
            .SingleOrDefault(item => string.Equals(item.Name?.Value, SheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("FAMILY_REVISER_SHEET_MISSING: Thiếu worksheet " + SheetName + ".");
        var data = ((WorksheetPart)workbookPart.GetPartById(sheet.Id!)).Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidDataException("FAMILY_REVISER_SHEET_INVALID");
        var rows = data.Elements<Row>().ToArray();
        if (rows.Length == 0 || !Values(workbookPart, rows[0], Headers.Length).SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("FAMILY_REVISER_HEADER_INVALID: Header đã bị đổi.");
        var result = new List<FamilyReviserRow>();
        foreach (var row in rows.Skip(1))
        {
            var values = Values(workbookPart, row, Headers.Length);
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            result.Add(new FamilyReviserRow(Boolean(values[0]), ParseAction(values[1], row.RowIndex), values[2].Trim(), values[3].Trim(),
                values[4].Trim(), values[5], values[6].Trim(), values[7], Boolean(values[8]), Boolean(values[9]), Boolean(values[10])));
        }
        return result;
    }

    private static FamilyReviserAction ParseAction(string value, UInt32Value? row) => value.Trim().ToUpperInvariant() switch
    {
        "RENAME_FAMILY" => FamilyReviserAction.RenameFamily,
        "RENAME_TYPE" => FamilyReviserAction.RenameType,
        "SKIP" => FamilyReviserAction.Skip,
        _ => throw new InvalidDataException("FAMILY_REVISER_ACTION_INVALID: Dòng " + row + ".")
    };

    private static string ActionText(FamilyReviserAction action) => action switch
    {
        FamilyReviserAction.RenameFamily => "RENAME_FAMILY",
        FamilyReviserAction.RenameType => "RENAME_TYPE",
        _ => "SKIP"
    };

    private static Row CreateRow(uint index, IReadOnlyList<string> values, bool header)
    {
        var row = new Row { RowIndex = index, Height = header ? 25D : 20D, CustomHeight = true };
        for (var column = 0; column < values.Count; column++)
            row.Append(new Cell { CellReference = ColumnName(column + 1) + index, DataType = CellValues.InlineString,
                StyleIndex = header ? 1U : 2U, InlineString = new InlineString(new Text(values[column] ?? string.Empty)) });
        return row;
    }

    private static string[] Values(WorkbookPart workbookPart, Row row, int count)
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

    private static bool Boolean(string value) => value.Trim() == "1" || value.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) || value.Trim().Equals("X", StringComparison.OrdinalIgnoreCase);
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
            new CellFormat { BorderId = 1U, ApplyBorder = true });
        part.Stylesheet = new Stylesheet(fonts, fills, borders, new CellStyleFormats(new CellFormat()), formats);
        part.Stylesheet.Save();
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return -1;
        var value = 0;
        foreach (var character in reference!) { if (!char.IsLetter(character)) break; value = value * 26 + char.ToUpperInvariant(character) - 'A' + 1; }
        return value - 1;
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0) { index--; name = (char)('A' + index % 26) + name; index /= 26; }
        return name;
    }
}
