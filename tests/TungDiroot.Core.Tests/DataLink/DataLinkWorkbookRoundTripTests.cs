using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TungDiroot.Core.DataLink;
using Xunit;

namespace TungDiroot.Core.Tests.DataLink;

public sealed class DataLinkWorkbookRoundTripTests
{
    [Fact]
    public void Apply_column_round_trips_as_an_import_filter()
    {
        WithWorkbook((path, _) =>
        {
            SetInlineText(path, "DATA", "A2", "FALSE");

            var imported = DataLinkWorkbookReader.Read(path);

            Assert.False(Assert.Single(imported.Rows).IncludeInImport);
        });
    }

    [Fact]
    public void Round_trip_preserves_hidden_identity_and_edited_visible_value()
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-DataLink-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            var parameter = new DataLinkParameterKey("builtin:comments", "Comments", false, "String", null);
            var row = new DataLinkElementRow("unique-1", 42, null, "Pipes", "Pipe", "Standard", parameter, "Original", "Original", "Original");
            var source = new DataLinkWorkbook(1, new DataLinkDocumentStamp("model-a"), "en-US", new[] { row });

            DataLinkWorkbookWriter.Write(path, source);
            SetInlineText(path, "DATA", "H2", "Edited");
            var imported = DataLinkWorkbookReader.Read(path);

            var actual = Assert.Single(imported.Rows);
            Assert.Equal("unique-1", actual.ElementUniqueId);
            Assert.Equal("builtin:comments", actual.Parameter.StableId);
            Assert.Equal("Edited", actual.EditedDisplay);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Writer_hides_and_protects_metadata_and_freezes_filterable_data_header()
    {
        WithWorkbook((path, _) =>
        {
            using var document = SpreadsheetDocument.Open(path, false);
            var workbookPart = document.WorkbookPart!;
            var metadataSheet = workbookPart.Workbook.Sheets!.Elements<Sheet>()
                .Single(item => item.Name == DataLinkWorkbookSchema.MetadataSheet);
            var metadataPart = (WorksheetPart)workbookPart.GetPartById(metadataSheet.Id!);
            var dataSheet = workbookPart.Workbook.Sheets!.Elements<Sheet>()
                .Single(item => item.Name == DataLinkWorkbookSchema.DataSheet);
            var dataPart = (WorksheetPart)workbookPart.GetPartById(dataSheet.Id!);

            Assert.Equal(SheetStateValues.VeryHidden, metadataSheet.State?.Value);
            Assert.True(metadataPart.Worksheet.GetFirstChild<SheetProtection>()?.Sheet?.Value);
            Assert.Equal(PaneStateValues.Frozen, dataPart.Worksheet.Descendants<Pane>().Single().State?.Value);
            Assert.Equal("A1:I2", dataPart.Worksheet.GetFirstChild<AutoFilter>()?.Reference?.Value);
        });
    }

    [Fact]
    public void Missing_metadata_sheet_is_rejected_with_stable_code()
    {
        WithWorkbook((path, _) =>
        {
            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var workbookPart = document.WorkbookPart!;
                var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>()
                    .Single(item => item.Name == DataLinkWorkbookSchema.MetadataSheet);
                var part = workbookPart.GetPartById(sheet.Id!);
                sheet.Remove();
                workbookPart.DeletePart(part);
                workbookPart.Workbook.Save();
            }

            var error = Assert.Throws<InvalidDataException>(() => DataLinkWorkbookReader.Read(path));
            Assert.Contains("DATALINK_METADATA_MISSING", error.Message);
        });
    }

    [Fact]
    public void Duplicate_metadata_row_is_rejected()
    {
        WithWorkbook((path, _) =>
        {
            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var metadata = SheetData(document.WorkbookPart!, DataLinkWorkbookSchema.MetadataSheet);
                metadata.Append((Row)metadata.Elements<Row>().Single(row => row.RowIndex?.Value == 2U).CloneNode(true));
                metadata.Ancestors<Worksheet>().Single().Save();
            }

            var error = Assert.Throws<InvalidDataException>(() => DataLinkWorkbookReader.Read(path));
            Assert.Contains("DATALINK_METADATA_DUPLICATE", error.Message);
        });
    }

    [Fact]
    public void Mismatched_row_checksum_is_rejected()
    {
        WithWorkbook((path, _) =>
        {
            SetInlineText(path, DataLinkWorkbookSchema.MetadataSheet, "K2", "tampered");

            var error = Assert.Throws<InvalidDataException>(() => DataLinkWorkbookReader.Read(path));
            Assert.Contains("DATALINK_CHECKSUM_MISMATCH", error.Message);
        });
    }

    [Fact]
    public void Unsupported_future_schema_is_rejected()
    {
        WithWorkbook((path, _) =>
        {
            SetInlineText(path, DataLinkWorkbookSchema.MetadataSheet, "A2", "99");

            var error = Assert.Throws<InvalidDataException>(() => DataLinkWorkbookReader.Read(path));
            Assert.Contains("DATALINK_SCHEMA_UNSUPPORTED", error.Message);
        });
    }

    [Fact]
    public void Edited_read_only_row_is_rejected()
    {
        WithWorkbook((path, _) =>
        {
            SetInlineText(path, DataLinkWorkbookSchema.DataSheet, "H2", "Edited");

            var error = Assert.Throws<InvalidDataException>(() => DataLinkWorkbookReader.Read(path));
            Assert.Contains("DATALINK_READ_ONLY_EDIT", error.Message);
        }, isReadOnly: true);
    }

    private static void SetInlineText(string path, string sheetName, string cellReference, string value)
    {
        using var document = SpreadsheetDocument.Open(path, true);
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == sheetName);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var cell = worksheetPart.Worksheet.Descendants<Cell>().Single(item => item.CellReference == cellReference);
        cell.DataType = CellValues.InlineString;
        cell.CellValue = null;
        cell.InlineString = new InlineString(new Text(value));
        worksheetPart.Worksheet.Save();
    }

    private static void WithWorkbook(Action<string, DataLinkWorkbook> test, bool isReadOnly = false)
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-DataLink-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            var parameter = new DataLinkParameterKey("builtin:comments", "Comments", false, "String", null);
            var row = new DataLinkElementRow("unique-1", 42, null, "Pipes", "Pipe", "Standard", parameter,
                "Original", "Original", "Original", isReadOnly);
            var workbook = new DataLinkWorkbook(1, new DataLinkDocumentStamp("model-a"), "en-US", new[] { row });
            DataLinkWorkbookWriter.Write(path, workbook);
            test(path, workbook);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static SheetData SheetData(WorkbookPart workbookPart, string name)
    {
        var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == name);
        return ((WorksheetPart)workbookPart.GetPartById(sheet.Id!)).Worksheet.GetFirstChild<SheetData>()!;
    }
}
