using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.SheetTools;

public static class SheetLinkWorkbookReader
{
    public static SheetLinkWorkbook Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy file SheetLink.", path);

        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("SHEETLINK_WORKBOOK_INVALID: Workbook không hợp lệ.");
        var sheetsData = Data(workbookPart, SheetLinkWorkbookSchema.SheetsSheet, SheetLinkWorkbookSchema.SheetHeaders);
        var viewsData = Data(workbookPart, SheetLinkWorkbookSchema.ViewsSheet, SheetLinkWorkbookSchema.ViewHeaders);
        var sheets = new List<SheetLinkSheetRow>();
        var views = new List<SheetLinkViewRow>();
        var revisions = new List<SheetLinkRevisionRow>();
        var sheetSourceRows = new List<int>();
        var viewSourceRows = new List<int>();
        var revisionSourceRows = new List<int>();

        foreach (var row in sheetsData.Elements<Row>().Where(item => (item.RowIndex?.Value ?? 0U) > 1U))
        {
            var values = Values(workbookPart, row, SheetLinkWorkbookSchema.SheetHeaders.Length);
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            sheets.Add(new SheetLinkSheetRow(ParseBoolean(values[0]), ParseAction(values[1], "SHEETS", row.RowIndex?.Value),
                EmptyToNull(values[2]), values[3].Trim(), values[4].Trim(), values[5].Trim(), values[6].Trim()));
            sheetSourceRows.Add(checked((int)row.RowIndex!.Value));
        }

        foreach (var row in viewsData.Elements<Row>().Where(item => (item.RowIndex?.Value ?? 0U) > 1U))
        {
            var values = Values(workbookPart, row, SheetLinkWorkbookSchema.ViewHeaders.Length);
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            views.Add(new SheetLinkViewRow(ParseBoolean(values[0]), ParseAction(values[1], "VIEWS", row.RowIndex?.Value),
                EmptyToNull(values[2]), values[3].Trim(), ParseViewKind(values[4], row.RowIndex?.Value), values[5].Trim(),
                values[6].Trim(), values[7].Trim(), ParseNumber(values[8], "CENTER_X_MM", row.RowIndex?.Value),
                ParseNumber(values[9], "CENTER_Y_MM", row.RowIndex?.Value)));
            viewSourceRows.Add(checked((int)row.RowIndex!.Value));
        }

        // REVISION was added after the original two-tab workbook contract.
        if (workbookPart.Workbook.Sheets?.Elements<Sheet>().Any(item =>
                string.Equals(item.Name?.Value, SheetLinkWorkbookSchema.RevisionsSheet, StringComparison.OrdinalIgnoreCase)) == true)
        {
            var data = Data(workbookPart, SheetLinkWorkbookSchema.RevisionsSheet, SheetLinkWorkbookSchema.RevisionHeaders);
            foreach (var row in data.Elements<Row>().Where(item => (item.RowIndex?.Value ?? 0U) > 1U))
            {
                var values = Values(workbookPart, row, SheetLinkWorkbookSchema.RevisionHeaders.Length);
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                int? sequence = null;
                if (!string.IsNullOrWhiteSpace(values[4]))
                {
                    if (!int.TryParse(values[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                        throw new InvalidDataException("SHEETLINK_REVISION_SEQUENCE_INVALID: REVISION dòng " + row.RowIndex + ".");
                    sequence = parsed;
                }
                var visibility = values[10].Trim().ToUpperInvariant() switch
                {
                    "CLOUD_AND_TAG" => SheetLinkRevisionVisibility.CloudAndTag,
                    "TAG_ONLY" => SheetLinkRevisionVisibility.TagOnly,
                    "HIDDEN" => SheetLinkRevisionVisibility.Hidden,
                    _ => throw new InvalidDataException("SHEETLINK_REVISION_VISIBILITY_INVALID: REVISION dòng " + row.RowIndex + ".")
                };
                revisions.Add(new SheetLinkRevisionRow(ParseBoolean(values[0]), ParseAction(values[1], "REVISION", row.RowIndex?.Value),
                    EmptyToNull(values[2]), values[3].Trim(), sequence, values[5].Trim(), values[6].Trim(), ParseBoolean(values[7]),
                    values[8].Trim(), values[9].Trim(), visibility));
                revisionSourceRows.Add(checked((int)row.RowIndex!.Value));
            }
        }
        return new SheetLinkWorkbook(sheets, views, revisions)
        {
            SheetSourceRows = sheetSourceRows.ToArray(), ViewSourceRows = viewSourceRows.ToArray(),
            RevisionSourceRows = revisionSourceRows.ToArray()
        };
    }

    private static SheetData Data(WorkbookPart workbookPart, string name, string[] headers)
    {
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>()
            .SingleOrDefault(item => string.Equals(item.Name?.Value, name, StringComparison.OrdinalIgnoreCase));
        if (sheet?.Id?.Value == null)
            throw new InvalidDataException("SHEETLINK_SHEET_MISSING: Thiếu worksheet " + name + ".");
        var data = ((WorksheetPart)workbookPart.GetPartById(sheet.Id.Value)).Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidDataException("SHEETLINK_SHEET_INVALID: Worksheet " + name + " không có dữ liệu.");
        var header = data.Elements<Row>().FirstOrDefault(item => (item.RowIndex?.Value ?? 0U) == 1U)
            ?? throw new InvalidDataException("SHEETLINK_HEADER_MISSING: Worksheet " + name + " thiếu header.");
        var actual = Values(workbookPart, header, headers.Length);
        if (!actual.SequenceEqual(headers, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("SHEETLINK_HEADER_INVALID: Header worksheet " + name + " đã bị đổi.");
        return data;
    }

    private static string[] Values(WorkbookPart workbookPart, Row row, int count)
    {
        var result = new string[count];
        foreach (var cell in row.Elements<Cell>())
        {
            var column = ColumnIndex(cell.CellReference?.Value);
            if (column >= 0 && column < count) result[column] = CellText(workbookPart, cell);
        }
        for (var index = 0; index < result.Length; index++) result[index] ??= string.Empty;
        return result;
    }

    private static string CellText(WorkbookPart workbookPart, Cell cell)
    {
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText
                ?? string.Empty;
        }
        return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
    }

    private static SheetLinkAction ParseAction(string value, string sheet, uint? row)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "CREATE": return SheetLinkAction.Create;
            case "UPDATE": return SheetLinkAction.Update;
            case "SKIP": return SheetLinkAction.Skip;
            default: throw new InvalidDataException("SHEETLINK_ACTION_INVALID: " + sheet + " dòng " + row + " có ACTION không hợp lệ.");
        }
    }

    private static SheetLinkViewKind ParseViewKind(string value, uint? row)
    {
        switch (value.Trim().Replace(" ", "_").ToUpperInvariant())
        {
            case "DRAFTING": return SheetLinkViewKind.Drafting;
            case "FLOOR_PLAN":
            case "FLOORPLAN": return SheetLinkViewKind.FloorPlan;
            case "CEILING_PLAN":
            case "CEILINGPLAN": return SheetLinkViewKind.CeilingPlan;
            default: throw new InvalidDataException("SHEETLINK_VIEW_KIND_INVALID: VIEWS dòng " + row + " có VIEW_KIND không hợp lệ.");
        }
    }

    private static double? ParseNumber(string value, string column, uint? row)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)) return invariant;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current)) return current;
        throw new InvalidDataException("SHEETLINK_NUMBER_INVALID: VIEWS dòng " + row + " có " + column + " không phải số.");
    }

    private static bool ParseBoolean(string value) =>
        string.Equals(value.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value.Trim(), "YES", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value.Trim(), "X", StringComparison.OrdinalIgnoreCase) || value.Trim() == "1";

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
}
