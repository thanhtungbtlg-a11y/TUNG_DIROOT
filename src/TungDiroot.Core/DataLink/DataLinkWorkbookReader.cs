using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.DataLink;

public static class DataLinkWorkbookReader
{
    public static DataLinkWorkbook Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy file DataLink.", path);

        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("Workbook không hợp lệ.");
        var data = GetSheetData(workbookPart, DataLinkWorkbookSchema.DataSheet);
        var metadata = GetSheetData(workbookPart, DataLinkWorkbookSchema.MetadataSheet);
        var dataRows = data.Elements<Row>().Where(row => (row.RowIndex?.Value ?? 0) > 1).ToDictionary(row => row.RowIndex!.Value);
        var metadataRows = metadata.Elements<Row>().Where(row => (row.RowIndex?.Value ?? 0) > 1).ToArray();
        if (metadataRows.GroupBy(row => row.RowIndex?.Value ?? 0).Any(group => group.Key == 0 || group.Count() > 1))
            throw new InvalidDataException("DATALINK_METADATA_DUPLICATE: Metadata có dòng trùng hoặc thiếu RowIndex.");
        var rows = new List<DataLinkElementRow>(metadataRows.Length);
        var version = DataLinkWorkbookSchema.CurrentVersion;
        var stamp = string.Empty;
        var culture = CultureInfo.InvariantCulture.Name;

        foreach (var metadataRow in metadataRows)
        {
            var values = Values(metadataRow, 12);
            version = int.Parse(values[0], CultureInfo.InvariantCulture);
            if (version != DataLinkWorkbookSchema.CurrentVersion)
                throw new InvalidDataException("DATALINK_SCHEMA_UNSUPPORTED: Phiên bản workbook không được hỗ trợ.");
            stamp = values[1];
            culture = values[2];
            if (!dataRows.TryGetValue(metadataRow.RowIndex!.Value, out var dataRow))
                throw new InvalidDataException("Thiếu dòng dữ liệu tương ứng với metadata.");
            var visible = Values(dataRow, 9);
            var protectedMetadata = values.Take(10).Concat(new[] { values[11] }).ToArray();
            var expectedChecksum = DataLinkWorkbookSchema.ComputeChecksum(protectedMetadata, visible.Skip(1).Take(6).ToArray());
            if (!string.Equals(expectedChecksum, values[10], StringComparison.Ordinal))
                throw new InvalidDataException("DATALINK_CHECKSUM_MISMATCH: Metadata hoặc cột định danh đã bị thay đổi.");
            var isReadOnly = ParseBoolean(values[11]);
            if (isReadOnly && !string.Equals(visible[6], visible[7], StringComparison.Ordinal))
                throw new InvalidDataException("DATALINK_READ_ONLY_EDIT: Dòng chỉ đọc đã bị chỉnh sửa.");
            var parameter = new DataLinkParameterKey(values[5], visible[5], ParseBoolean(values[6]), values[7], EmptyToNull(values[8]));
            rows.Add(new DataLinkElementRow(values[3], long.Parse(visible[1], CultureInfo.InvariantCulture), EmptyToNull(values[4]),
                visible[2], visible[3], visible[4], parameter, values[9], visible[6], visible[7], isReadOnly,
                ParseBoolean(visible[0])));
        }

        return new DataLinkWorkbook(version, new DataLinkDocumentStamp(stamp), culture, rows);
    }

    private static SheetData GetSheetData(WorkbookPart workbookPart, string name)
    {
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>()
            .SingleOrDefault(item => string.Equals(item.Name?.Value, name, StringComparison.Ordinal));
        if (sheet?.Id?.Value == null)
        {
            var code = string.Equals(name, DataLinkWorkbookSchema.MetadataSheet, StringComparison.Ordinal)
                ? "DATALINK_METADATA_MISSING"
                : "DATALINK_DATA_MISSING";
            throw new InvalidDataException(code + ": Thiếu worksheet " + name + ".");
        }
        return ((WorksheetPart)workbookPart.GetPartById(sheet.Id.Value)).Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidDataException("Worksheet " + name + " không có dữ liệu.");
    }

    private static string[] Values(Row row, int count)
    {
        var result = new string[count];
        foreach (var cell in row.Elements<Cell>())
        {
            var column = ColumnIndex(cell.CellReference?.Value);
            if (column >= 0 && column < result.Length) result[column] = Text(cell);
        }
        for (var index = 0; index < result.Length; index++) result[index] ??= string.Empty;
        return result;
    }

    private static string Text(Cell cell) =>
        cell.InlineString?.Text?.Text ?? cell.CellValue?.Text ?? string.Empty;

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return -1;
        var value = 0;
        foreach (var character in reference!)
        {
            if (!char.IsLetter(character)) break;
            value = value * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }
        return value - 1;
    }

    private static bool ParseBoolean(string value) =>
        string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
}
