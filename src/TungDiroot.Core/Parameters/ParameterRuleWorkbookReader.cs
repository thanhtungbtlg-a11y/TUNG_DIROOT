using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.Parameters;

public sealed class ParameterRuleWorkbookReader
{
    private const string WorksheetName = "PARAMETER_LOADER";
    private static readonly string[] RequiredHeaders =
        { "ENABLED", "PARAMETER_GROUP", "PARAMETER_NAME", "BINDING", "REVIT_GROUP", "CATEGORIES" };
    private static readonly HashSet<string> EnabledTokens =
        new(new[] { "TRUE", "YES", "1", "X" }, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ParameterRuleDraft> Read(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        ValidateExtension(path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("Workbook không có workbook part.");
        var sheet = (workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>())
            .FirstOrDefault(candidate => string.Equals(candidate.Name?.Value, WorksheetName, StringComparison.OrdinalIgnoreCase));
        if (sheet?.Id?.Value == null)
            throw new InvalidDataException($"Không tìm thấy worksheet {WorksheetName}.");
        if (!(workbookPart.GetPartById(sheet.Id.Value) is WorksheetPart worksheetPart))
            throw new InvalidDataException($"Worksheet {WorksheetName} không hợp lệ.");

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var rows = worksheetPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToArray() ?? Array.Empty<Row>();
        var headerRow = rows.FirstOrDefault(row => (row.RowIndex?.Value ?? 0U) == 1U) ?? rows.FirstOrDefault();
        if (headerRow == null) throw new InvalidDataException($"Worksheet {WorksheetName} không có hàng header.");
        var headerMap = ReadHeaderMap(headerRow, sharedStrings);
        foreach (var required in RequiredHeaders)
            if (!headerMap.ContainsKey(required)) throw new InvalidDataException($"Thiếu header bắt buộc {required} trong worksheet {WorksheetName}.");

        var result = new List<ParameterRuleDraft>();
        foreach (var row in rows.Where(row => (row.RowIndex?.Value ?? 0U) > (headerRow.RowIndex?.Value ?? 1U)))
        {
            var values = ReadRow(row, sharedStrings);
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            var rowNumber = checked((int)(row.RowIndex?.Value ?? 0U));
            result.Add(ParseRule(rowNumber, headerMap, values));
        }
        return result;
    }

    private static ParameterRuleDraft ParseRule(
        int rowNumber,
        IReadOnlyDictionary<string, int> headerMap,
        IReadOnlyDictionary<int, string> values)
    {
        string Get(string header) => values.TryGetValue(headerMap[header], out var value) ? value.Trim() : string.Empty;

        var enabled = EnabledTokens.Contains(Get("ENABLED"));
        var definitionGroup = Get("PARAMETER_GROUP");
        var parameterName = Get("PARAMETER_NAME");
        var bindingText = Get("BINDING");
        var revitGroup = Get("REVIT_GROUP").ToUpperInvariant();
        var categories = DistinctKeys(Get("CATEGORIES"));
        var issues = new List<ParameterConfigurationIssue>();

        if (definitionGroup.Length == 0) issues.Add(new("PARAMETER_GROUP_REQUIRED", "PARAMETER_GROUP không được để trống."));
        if (parameterName.Length == 0) issues.Add(new("PARAMETER_NAME_REQUIRED", "PARAMETER_NAME không được để trống."));
        ParameterBindingKind? bindingKind = null;
        if (string.Equals(bindingText, "INSTANCE", StringComparison.OrdinalIgnoreCase)) bindingKind = ParameterBindingKind.Instance;
        else if (string.Equals(bindingText, "TYPE", StringComparison.OrdinalIgnoreCase)) bindingKind = ParameterBindingKind.Type;
        else issues.Add(new("BINDING_INVALID", "BINDING phải là INSTANCE hoặc TYPE."));
        if (revitGroup.Length == 0) issues.Add(new("REVIT_GROUP_REQUIRED", "REVIT_GROUP không được để trống."));
        if (categories.Count == 0) issues.Add(new("CATEGORIES_REQUIRED", "CATEGORIES phải có ít nhất một mã OST_*."));

        return new ParameterRuleDraft(rowNumber, enabled, definitionGroup, parameterName, bindingKind,
            revitGroup, categories, issues);
    }

    private static IReadOnlyList<string> DistinctKeys(string value)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var item in value.Split(';').Select(part => part.Trim()).Where(part => part.Length > 0))
            if (seen.Add(item)) result.Add(item);
        return result;
    }

    private static Dictionary<string, int> ReadHeaderMap(Row row, SharedStringTable? sharedStrings)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.Elements<Cell>())
        {
            var index = GetColumnIndex(cell.CellReference?.Value);
            var header = GetText(cell, sharedStrings).Trim().ToUpperInvariant();
            if (header.Length == 0) continue;
            if (result.ContainsKey(header)) throw new InvalidDataException("Header bị trùng: " + header + ".");
            result[header] = index;
        }
        return result;
    }

    private static Dictionary<int, string> ReadRow(Row row, SharedStringTable? sharedStrings)
    {
        var result = new Dictionary<int, string>();
        foreach (var cell in row.Elements<Cell>())
            result[GetColumnIndex(cell.CellReference?.Value)] = GetText(cell, sharedStrings);
        return result;
    }

    private static string GetText(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? string.Empty;
        var raw = cell.CellValue?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString)
            return int.TryParse(raw, out var index) && sharedStrings != null && index >= 0 && index < sharedStrings.Count()
                ? sharedStrings.ElementAt(index).InnerText
                : string.Empty;
        if (cell.DataType?.Value == CellValues.Boolean) return raw == "1" ? "TRUE" : raw == "0" ? "FALSE" : raw;
        return raw;
    }

    private static int GetColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new InvalidDataException("Cell không có địa chỉ cột.");
        var result = 0;
        foreach (var character in reference!)
        {
            if (!char.IsLetter(character)) break;
            result = checked(result * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        if (result == 0) throw new InvalidDataException("Địa chỉ cell không hợp lệ: " + reference + ".");
        return result;
    }

    private static void ValidateExtension(string path)
    {
        var extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Parameter Loader chỉ hỗ trợ workbook .xlsx và .xlsm.");
    }
}
