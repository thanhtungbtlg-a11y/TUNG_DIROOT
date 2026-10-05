using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.Parameters;

public enum ParameterManagerScope { Project, Family }
public enum ParameterManagerAction { CreateShared, Update, Skip }
public enum ParameterManagerPreviewStatus { Ready, NoChange, Skipped, Invalid, Conflict }

public sealed record ParameterManagerRow(
    bool Apply,
    ParameterManagerAction Action,
    ParameterManagerScope Scope,
    string Identity,
    string Name,
    string SharedGroup,
    Guid? Guid,
    ParameterBindingKind? BindingKind,
    string ParameterGroupId,
    IReadOnlyList<string> CategoryKeys,
    bool IsBuiltIn);

public sealed record ParameterManagerWorkbook(ParameterManagerScope Scope, IReadOnlyList<ParameterManagerRow> Rows)
{
    public static void Write(string path, ParameterManagerScope scope, IReadOnlyList<ParameterManagerRow> source, int blankRows = 10,
        IReadOnlyList<string>? parameterGroupIds = null, IReadOnlyList<string>? categoryKeys = null) =>
        ParameterManagerWorkbookFile.Write(path, scope, source, blankRows, parameterGroupIds, categoryKeys);

    public static ParameterManagerWorkbook Read(string path) => ParameterManagerWorkbookFile.Read(path);
}

public sealed record ParameterManagerReferenceCatalog(
    ParameterManagerScope Scope,
    IReadOnlyList<ParameterManagerRow> CurrentRows,
    IReadOnlyList<string> CategoryKeys,
    IReadOnlyList<string> ParameterGroupIds,
    IReadOnlyList<SharedParameterDefinitionModel> SharedDefinitions);

public sealed record ParameterManagerPreviewItem(
    int ExcelRow,
    ParameterManagerRow Row,
    ParameterManagerRow? Current,
    ParameterManagerPreviewStatus Status,
    string Code,
    string Message,
    IReadOnlyList<string> EffectiveCategoryKeys);

public sealed record ParameterManagerPreviewPlan(IReadOnlyList<ParameterManagerPreviewItem> Items)
{
    public int ReadyCount => Items.Count(item => item.Status == ParameterManagerPreviewStatus.Ready);
    public bool HasErrors => Items.Any(item => item.Status is ParameterManagerPreviewStatus.Invalid or ParameterManagerPreviewStatus.Conflict);
}

public static class ParameterManagerPlanner
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    public static ParameterManagerPreviewPlan Plan(ParameterManagerWorkbook workbook, ParameterManagerReferenceCatalog catalog)
    {
        if (workbook == null) throw new ArgumentNullException(nameof(workbook));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        var activeNames = new HashSet<string>(workbook.Rows.Where(IsActive).Select(row => row.Name.Trim()).Where(name => name.Length > 0)
            .GroupBy(name => name, Names).Where(group => group.Count() > 1).Select(group => group.Key), Names);
        var result = new List<ParameterManagerPreviewItem>();
        for (var index = 0; index < workbook.Rows.Count; index++)
            result.Add(Validate(workbook.Rows[index], index + 2, workbook.Scope, catalog, activeNames));
        return new ParameterManagerPreviewPlan(result);
    }

    private static ParameterManagerPreviewItem Validate(
        ParameterManagerRow row,
        int excelRow,
        ParameterManagerScope workbookScope,
        ParameterManagerReferenceCatalog catalog,
        HashSet<string> duplicateNames)
    {
        if (!IsActive(row)) return Item(excelRow, row, null, ParameterManagerPreviewStatus.Skipped, "SKIPPED", "Dòng không được chọn Apply.", row.CategoryKeys);
        var errors = new List<(string Code, string Message, bool Conflict)>();
        if (row.Scope != workbookScope || row.Scope != catalog.Scope)
            errors.Add(("SCOPE_MISMATCH", "SCOPE không khớp document đang mở.", false));
        if (string.IsNullOrWhiteSpace(row.Name)) errors.Add(("PARAMETER_NAME_REQUIRED", "Thiếu NAME.", false));
        if (duplicateNames.Contains(row.Name.Trim())) errors.Add(("PARAMETER_NAME_DUPLICATE", "NAME bị lặp trong workbook.", true));
        if (row.BindingKind == null) errors.Add(("BINDING_REQUIRED", "BINDING phải là INSTANCE hoặc TYPE.", false));
        if (string.IsNullOrWhiteSpace(row.ParameterGroupId) || !catalog.ParameterGroupIds.Contains(row.ParameterGroupId.Trim(), Names))
            errors.Add(("PARAMETER_GROUP_INVALID", "PARAMETER_GROUP_ID không tồn tại hoặc không được phép.", false));

        var invalidCategories = row.Scope == ParameterManagerScope.Project
            ? row.CategoryKeys.Where(key => !catalog.CategoryKeys.Contains(key, Names)).Distinct(Names).ToArray()
            : Array.Empty<string>();
        if (invalidCategories.Length > 0)
            errors.Add(("CATEGORY_INVALID", "Category không hợp lệ: " + string.Join(", ", invalidCategories) + ".", false));

        ParameterManagerRow? current = null;
        IReadOnlyList<string> effective = row.CategoryKeys.Distinct(Names).OrderBy(value => value, Names).ToArray();
        if (row.Action == ParameterManagerAction.CreateShared)
        {
            if (string.IsNullOrWhiteSpace(row.SharedGroup)) errors.Add(("SHARED_GROUP_REQUIRED", "Thiếu SHARED_GROUP.", false));
            var definitions = catalog.SharedDefinitions.Where(definition =>
                Names.Equals(definition.GroupName, row.SharedGroup.Trim()) && Names.Equals(definition.Name, row.Name.Trim())).ToArray();
            if (definitions.Length != 1)
                errors.Add(("SHARED_DEFINITION_NOT_FOUND", "Không tìm thấy đúng một shared definition theo SHARED_GROUP/NAME.", false));
            else if (row.Guid.HasValue && row.Guid.Value != definitions[0].Guid)
                errors.Add(("SHARED_GUID_MISMATCH", "GUID không khớp shared definition.", true));
            if (catalog.CurrentRows.Any(existing =>
                    (row.Guid.HasValue && existing.Guid == row.Guid) || Names.Equals(existing.Name, row.Name)))
                errors.Add(("PARAMETER_ALREADY_EXISTS", "Parameter cùng tên hoặc GUID đã tồn tại.", true));
            if (row.Scope == ParameterManagerScope.Project && row.CategoryKeys.Count == 0)
                errors.Add(("CATEGORY_REQUIRED", "Project parameter mới cần ít nhất một category.", false));
        }
        else if (row.Action == ParameterManagerAction.Update)
        {
            current = catalog.CurrentRows.FirstOrDefault(item => Names.Equals(item.Identity, row.Identity));
            if (current == null)
                errors.Add(("PARAMETER_UPDATE_NOT_FOUND", "IDENTITY không còn tồn tại trong document.", false));
            else
            {
                if (current.BindingKind != row.BindingKind)
                    errors.Add(("BINDING_KIND_CONFLICT", "Không tự chuyển binding INSTANCE ↔ TYPE.", true));
                if (!Names.Equals(current.Name, row.Name))
                {
                    if (row.Scope == ParameterManagerScope.Project)
                        errors.Add(("PROJECT_PARAMETER_RENAME_BLOCKED", "Không đổi tên project parameter qua BindingMap.", true));
                    else if (current.Guid.HasValue)
                        errors.Add(("SHARED_PARAMETER_RENAME_BLOCKED", "Không đổi tên shared family parameter.", true));
                    else if (current.IsBuiltIn)
                        errors.Add(("BUILTIN_PARAMETER_RENAME_BLOCKED", "Không đổi tên built-in family parameter.", true));
                }
                if (row.Scope == ParameterManagerScope.Project)
                    effective = current.CategoryKeys.Concat(row.CategoryKeys).Distinct(Names).OrderBy(value => value, Names).ToArray();
            }
        }
        else errors.Add(("ACTION_INVALID", "ACTION không được hỗ trợ.", false));

        if (errors.Count > 0)
            return Item(excelRow, row, current,
                errors.Any(error => error.Conflict) ? ParameterManagerPreviewStatus.Conflict : ParameterManagerPreviewStatus.Invalid,
                string.Join(";", errors.Select(error => error.Code).Distinct()),
                string.Join(" ", errors.Select(error => error.Message).Distinct()), effective);

        if (current != null && SameConfiguration(row, current, effective))
            return Item(excelRow, row, current, ParameterManagerPreviewStatus.NoChange, "NO_CHANGE", "Không có thay đổi.", effective);
        var message = row.Action == ParameterManagerAction.CreateShared
            ? "Sẵn sàng tạo shared parameter."
            : row.Scope == ParameterManagerScope.Project
                ? "Sẵn sàng cập nhật group và merge category; category hiện có luôn được giữ."
                : "Sẵn sàng cập nhật tên/group được API cho phép.";
        return Item(excelRow, row, current, ParameterManagerPreviewStatus.Ready, "READY", message, effective);
    }

    private static bool SameConfiguration(ParameterManagerRow row, ParameterManagerRow current, IReadOnlyList<string> effective) =>
        Names.Equals(row.Name, current.Name) && row.BindingKind == current.BindingKind &&
        Names.Equals(row.ParameterGroupId, current.ParameterGroupId) &&
        new HashSet<string>(effective, Names).SetEquals(current.CategoryKeys);

    private static bool IsActive(ParameterManagerRow row) => row.Apply && row.Action != ParameterManagerAction.Skip;

    private static ParameterManagerPreviewItem Item(int excelRow, ParameterManagerRow row, ParameterManagerRow? current,
        ParameterManagerPreviewStatus status, string code, string message, IReadOnlyList<string> effective) =>
        new(excelRow, row, current, status, code, message, effective);
}

internal static class ParameterManagerWorkbookFile
{
    public const string SheetName = "PARAMETER_MANAGER";
    private static readonly string[] Headers =
    {
        "APPLY", "ACTION", "SCOPE", "IDENTITY", "NAME", "SHARED_GROUP", "GUID", "BINDING",
        "PARAMETER_GROUP_ID", "CATEGORIES", "IS_BUILT_IN", "STATUS"
    };

    public static void Write(string path, ParameterManagerScope scope, IReadOnlyList<ParameterManagerRow> source, int blankRows = 10,
        IReadOnlyList<string>? parameterGroupIds = null, IReadOnlyList<string>? categoryKeys = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Đường dẫn Excel không hợp lệ.", nameof(path));
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (blankRows < 0) throw new ArgumentOutOfRangeException(nameof(blankRows));
        var rows = new List<ParameterManagerRow>(source);
        for (var index = 0; index < blankRows; index++)
            rows.Add(new ParameterManagerRow(false, ParameterManagerAction.CreateShared, scope, string.Empty, string.Empty,
                string.Empty, null, ParameterBindingKind.Instance, string.Empty, Array.Empty<string>(), false));

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
        var columns = new Columns();
        var widths = new[] { 10D, 16D, 12D, 38D, 30D, 22D, 38D, 14D, 40D, 38D, 14D, 42D };
        for (var index = 0; index < widths.Length; index++)
            columns.Append(new Column { Min = (uint)index + 1U, Max = (uint)index + 1U, Width = widths[index], CustomWidth = true });
        var data = new SheetData();
        part.Worksheet = new Worksheet(new SheetViews(view), columns, data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1U, Name = SheetName });
        data.Append(CreateRow(1U, Headers, true));
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            data.Append(CreateRow((uint)index + 2U, new[]
            {
                row.Apply ? "TRUE" : "FALSE", Action(row.Action), row.Scope.ToString().ToUpperInvariant(), row.Identity,
                row.Name, row.SharedGroup, row.Guid?.ToString("D") ?? string.Empty,
                row.BindingKind?.ToString().ToUpperInvariant() ?? string.Empty, row.ParameterGroupId,
                string.Join(";", row.CategoryKeys), row.IsBuiltIn ? "TRUE" : "FALSE",
                row.Action == ParameterManagerAction.Update ? "Chỉnh group/category an toàn" : "Nhập shared definition rồi bật APPLY"
            }, false));
        }
        part.Worksheet.Append(new AutoFilter { Reference = "A1:L" + Math.Max(1, rows.Count + 1) });
        part.Worksheet.Save();
        AddReferenceSheet(workbookPart, sheets, 2U, "GROUPS", "PARAMETER_GROUP_ID",
            parameterGroupIds ?? Array.Empty<string>(), "Copy ID vào cột PARAMETER_GROUP_ID.");
        AddReferenceSheet(workbookPart, sheets, 3U, "CATEGORIES", "CATEGORY_KEY",
            categoryKeys ?? Array.Empty<string>(), "Project mode: ngăn nhiều key bằng dấu chấm phẩy.");
        workbookPart.Workbook.Save();
    }

    public static ParameterManagerWorkbook Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Không tìm thấy workbook ParaManager.", path);
        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("PARAMANAGER_WORKBOOK_INVALID");
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().SingleOrDefault(item =>
            string.Equals(item.Name?.Value, SheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("PARAMANAGER_SHEET_MISSING: Thiếu worksheet " + SheetName + ".");
        var data = ((WorksheetPart)workbookPart.GetPartById(sheet.Id!)).Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidDataException("PARAMANAGER_SHEET_INVALID");
        var rawRows = data.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>().ToArray();
        if (rawRows.Length == 0 || !Values(workbookPart, rawRows[0], Headers.Length).SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("PARAMANAGER_HEADER_INVALID: Header đã bị đổi.");
        var rows = new List<ParameterManagerRow>();
        ParameterManagerScope? workbookScope = null;
        foreach (var raw in rawRows.Skip(1))
        {
            var values = Values(workbookPart, raw, Headers.Length);
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            var scope = Enum.TryParse<ParameterManagerScope>(values[2], true, out var parsedScope) ? parsedScope
                : throw new InvalidDataException("PARAMANAGER_SCOPE_INVALID: Dòng " + raw.RowIndex + ".");
            if (workbookScope.HasValue && workbookScope.Value != scope)
                throw new InvalidDataException("PARAMANAGER_SCOPE_MIXED: Workbook không được trộn PROJECT/FAMILY.");
            workbookScope = scope;
            Guid? guid = null;
            if (!string.IsNullOrWhiteSpace(values[6]))
            {
                if (!Guid.TryParse(values[6], out var parsedGuid)) throw new InvalidDataException("PARAMANAGER_GUID_INVALID: Dòng " + raw.RowIndex + ".");
                guid = parsedGuid;
            }
            rows.Add(new ParameterManagerRow(Boolean(values[0]), ParseAction(values[1], raw.RowIndex), scope, values[3].Trim(),
                values[4].Trim(), values[5].Trim(), guid, ParseBinding(values[7], raw.RowIndex), values[8].Trim(),
                Split(values[9]), Boolean(values[10])));
        }
        if (!workbookScope.HasValue) throw new InvalidDataException("PARAMANAGER_SCOPE_MISSING");
        return new ParameterManagerWorkbook(workbookScope.Value, rows);
    }

    private static ParameterManagerAction ParseAction(string value, UInt32Value? row) => value.Trim().ToUpperInvariant() switch
    {
        "CREATE_SHARED" => ParameterManagerAction.CreateShared,
        "UPDATE" => ParameterManagerAction.Update,
        "SKIP" => ParameterManagerAction.Skip,
        _ => throw new InvalidDataException("PARAMANAGER_ACTION_INVALID: Dòng " + row + ".")
    };

    private static ParameterBindingKind? ParseBinding(string value, UInt32Value? row)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Trim().Equals("INSTANCE", StringComparison.OrdinalIgnoreCase)) return ParameterBindingKind.Instance;
        if (value.Trim().Equals("TYPE", StringComparison.OrdinalIgnoreCase)) return ParameterBindingKind.Type;
        throw new InvalidDataException("PARAMANAGER_BINDING_INVALID: Dòng " + row + ".");
    }

    private static string Action(ParameterManagerAction action) => action switch
    {
        ParameterManagerAction.CreateShared => "CREATE_SHARED",
        ParameterManagerAction.Update => "UPDATE",
        _ => "SKIP"
    };

    private static IReadOnlyList<string> Split(string value) => value.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static DocumentFormat.OpenXml.Spreadsheet.Row CreateRow(uint index, string[] values, bool header)
    {
        var row = new DocumentFormat.OpenXml.Spreadsheet.Row { RowIndex = index, Height = header ? 25D : 20D, CustomHeight = true };
        for (var column = 0; column < values.Length; column++)
            row.Append(new Cell { CellReference = ColumnName(column + 1) + index, DataType = CellValues.InlineString,
                StyleIndex = header ? 1U : 2U, InlineString = new InlineString(new Text(values[column] ?? string.Empty)) });
        return row;
    }

    private static void AddReferenceSheet(WorkbookPart workbookPart, DocumentFormat.OpenXml.Spreadsheet.Sheets sheets,
        uint sheetId, string name, string keyHeader, IReadOnlyList<string> values, string note)
    {
        var part = workbookPart.AddNewPart<WorksheetPart>();
        var view = new SheetView { WorkbookViewId = 0U, ShowGridLines = false };
        view.Append(new Pane { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen });
        var data = new SheetData();
        part.Worksheet = new Worksheet(new SheetViews(view),
            new Columns(new Column { Min = 1U, Max = 1U, Width = 54D, CustomWidth = true },
                new Column { Min = 2U, Max = 2U, Width = 48D, CustomWidth = true }), data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = sheetId, Name = name });
        data.Append(CreateRow(1U, new[] { keyHeader, "HOW_TO_USE" }, true));
        for (var index = 0; index < values.Count; index++)
            data.Append(CreateRow((uint)index + 2U, new[] { values[index], note }, false));
        part.Worksheet.Append(new AutoFilter { Reference = "A1:B" + Math.Max(1, values.Count + 1) });
        part.Worksheet.Save();
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

    private static bool Boolean(string value) => value.Trim() == "1" || value.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) || value.Trim().Equals("X", StringComparison.OrdinalIgnoreCase);

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
