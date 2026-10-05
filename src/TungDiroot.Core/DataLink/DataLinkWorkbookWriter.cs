using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.DataLink;

public static class DataLinkWorkbookWriter
{
    public static void Write(string path, DataLinkWorkbook workbook)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Đường dẫn Excel không hợp lệ.", nameof(path));
        if (workbook == null) throw new ArgumentNullException(nameof(workbook));

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        var dataPart = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        var sheetView = new SheetView { WorkbookViewId = 0U };
        sheetView.Append(new Pane
        {
            VerticalSplit = 1D,
            TopLeftCell = "A2",
            ActivePane = PaneValues.BottomLeft,
            State = PaneStateValues.Frozen
        });
        dataPart.Worksheet = new Worksheet(new SheetViews(sheetView), data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(dataPart), SheetId = 1, Name = DataLinkWorkbookSchema.DataSheet });

        var metadataPart = workbookPart.AddNewPart<WorksheetPart>();
        var metadata = new SheetData();
        metadataPart.Worksheet = new Worksheet(metadata, new SheetProtection { Sheet = true, Objects = true, Scenarios = true });
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(metadataPart),
            SheetId = 2,
            Name = DataLinkWorkbookSchema.MetadataSheet,
            State = SheetStateValues.VeryHidden
        });

        data.Append(CreateRow(1, DataLinkWorkbookSchema.DataHeaders));
        metadata.Append(CreateRow(1, DataLinkWorkbookSchema.MetadataHeaders));

        for (var index = 0; index < workbook.Rows.Count; index++)
        {
            var source = workbook.Rows[index];
            var rowIndex = (uint)index + 2;
            var visible = new[]
            {
                source.IncludeInImport ? "TRUE" : "FALSE", source.ElementId.ToString(System.Globalization.CultureInfo.InvariantCulture), source.Category,
                source.Family, source.TypeName, source.Parameter.Name, source.OriginalDisplay, source.EditedDisplay, string.Empty
            };
            var protectedMetadata = new[]
            {
                workbook.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), workbook.DocumentStamp.Identity,
                workbook.CultureName, source.ElementUniqueId, source.TypeUniqueId ?? string.Empty, source.Parameter.StableId,
                source.Parameter.IsType ? "TRUE" : "FALSE", source.Parameter.StorageKind, source.Parameter.SpecId ?? string.Empty,
                source.OriginalInternal, source.IsReadOnly ? "TRUE" : "FALSE"
            };
            var checksum = DataLinkWorkbookSchema.ComputeChecksum(protectedMetadata, visible.Skip(1).Take(6).ToArray());
            data.Append(CreateRow(rowIndex, visible));
            metadata.Append(CreateRow(rowIndex, protectedMetadata.Take(10).Concat(new[] { checksum, protectedMetadata[10] }).ToArray()));
        }

        dataPart.Worksheet.Append(new AutoFilter { Reference = "A1:I" + Math.Max(1, workbook.Rows.Count + 1) });

        dataPart.Worksheet.Save();
        metadataPart.Worksheet.Save();
        workbookPart.Workbook.Save();
    }

    private static Row CreateRow(uint index, string[] values)
    {
        var row = new Row { RowIndex = index };
        for (var column = 0; column < values.Length; column++)
        {
            row.Append(new Cell
            {
                CellReference = ColumnName(column + 1) + index,
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
