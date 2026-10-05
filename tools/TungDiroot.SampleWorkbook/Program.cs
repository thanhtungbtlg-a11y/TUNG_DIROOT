using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

if (args.Length != 1) throw new ArgumentException("Usage: TungDiroot.SampleWorkbook <output.xlsx>");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
if (File.Exists(output)) File.Delete(output);

using var document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
var workbookPart = document.AddWorkbookPart();
document.ChangeIdOfPart(workbookPart, "rIdWorkbook");
workbookPart.Workbook = new Workbook();
var styles = workbookPart.AddNewPart<WorkbookStylesPart>();
workbookPart.ChangeIdOfPart(styles, "rIdStyles");
styles.Stylesheet = CreateStyles();
styles.Stylesheet.Save();
var sheets = workbookPart.Workbook.AppendChild(new Sheets());

var pccc = AddSheet(workbookPart, sheets, 1, "PCCC", new[]
{
    Row(1, Cell("A1", "DANH MỤC BẢN VẼ PHÒNG CHÁY CHỮA CHÁY", 1)),
    Row(2, Cell("A2", "STT", 2), Cell("B2", "SỐ HIỆU", 2), Cell("C2", "TÊN BẢN VẼ", 2)),
    Row(3, Cell("A3", "1"), Cell("B3", "FF-01"), Cell("C3", "Sơ đồ nguyên lý hệ thống PCCC")),
    Row(4, Cell("A4", "2"), Cell("B4", "FF-02"), Cell("C4", "Mặt bằng chữa cháy tầng 1"))
}, "A1:H25", new[] { "A1:C1" });

var equipment = AddSheet(workbookPart, sheets, 2, "Thiết bị", new[]
{
    Row(1, Cell("A1", "MÃ", 2), Cell("B1", "MÔ TẢ", 2), Cell("C1", "SỐ LƯỢNG", 2)),
    Row(2, Cell("A2", "TB-01"), Cell("B2", "Bơm chữa cháy"), Number("C2", "2")),
    HiddenRow(3, Cell("A3", "TB-02"), Cell("B3", "Van báo động"), Number("C3", "4"))
}, "A1:F18");
equipment.Worksheet.InsertAt(new Columns(new Column { Min = 4U, Max = 4U, Width = 14D, Hidden = true, CustomWidth = true }), 1);

AddSheet(workbookPart, sheets, 3, "Định dạng", new[]
{
    Row(1, Cell("A1", "ĐẬM / CĂN GIỮA / TÔ MÀU", 1)),
    Row(2, Cell("A2", "Viền và nền", 3), Cell("B2", "Unicode: Ống đứng – nhánh xả", 3)),
    Row(3, Number("A3", "0.005", 4), Cell("B3", "0,500%"))
}, "A1:J40", new[] { "A1:D1" });

var graphics = AddSheet(workbookPart, sheets, 4, "Đồ họa", new[]
{
    Row(1, Cell("A1", "Ảnh và shape marker", 1)),
    Row(4, Cell("A4", "Built-in: ảnh raster; Excel High Fidelity: chart/SmartArt"))
}, "A1:L30");
AddDrawing(graphics);

AddSheet(workbookPart, sheets, 5, "Công thức", new[]
{
    Row(1, Cell("A1", "Giá trị"), Cell("B1", "Có cache"), Cell("C1", "Thiếu cache")),
    Row(2, Number("A2", "10"), Formula("B2", "A2*2", "20"), Formula("C2", "A2*3", null))
}, "A1:F20");

workbookPart.Workbook.Append(new DefinedNames(
    new DefinedName { Name = "PCCC_Table", Text = "'PCCC'!$A$1:$C$4" },
    new DefinedName { Name = "Large_Table", Text = "'Định dạng'!$A$1:$Z$250" },
    new DefinedName { Name = "_xlnm.Print_Area", LocalSheetId = 0U, Text = "'PCCC'!$A$1:$H$25" },
    new DefinedName { Name = "_xlnm.Print_Area", LocalSheetId = 1U, Text = "'Thiết bị'!$A$1:$F$18" }));
workbookPart.Workbook.Save();
document.Dispose();
NormalizePackage(output);
Console.WriteLine(output);

static WorksheetPart AddSheet(WorkbookPart book, Sheets sheets, uint id, string name, IEnumerable<Row> rows, string dimension, IEnumerable<string>? merges = null)
{
    var part = book.AddNewPart<WorksheetPart>();
    book.ChangeIdOfPart(part, "rIdSheet" + id);
    var sheetData = new SheetData(rows);
    part.Worksheet = new Worksheet(new SheetDimension { Reference = dimension }, sheetData);
    if (merges != null) part.Worksheet.Append(new MergeCells(merges.Select(value => new MergeCell { Reference = value })));
    part.Worksheet.Save();
    sheets.Append(new Sheet { Id = book.GetIdOfPart(part), SheetId = id, Name = name });
    return part;
}

static Row Row(uint index, params Cell[] cells) => new(cells) { RowIndex = index };
static Row HiddenRow(uint index, params Cell[] cells) => new(cells) { RowIndex = index, Hidden = true };
static Cell Cell(string reference, string text, uint style = 0) => new() { CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style, InlineString = new InlineString(new Text(text)) };
static Cell Number(string reference, string value, uint style = 0) => new() { CellReference = reference, StyleIndex = style, CellValue = new CellValue(value) };
static Cell Formula(string reference, string formula, string? cached) => new() { CellReference = reference, CellFormula = new CellFormula(formula), CellValue = cached == null ? null : new CellValue(cached) };

static Stylesheet CreateStyles() => new(
    new Fonts(new Font(), new Font(new Bold(), new FontSize { Val = 14D }, new FontName { Val = "Arial" })) { Count = 2U },
    new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill(new ForegroundColor { Rgb = "FFD9EAD3" }, new BackgroundColor { Indexed = 64U }) { PatternType = PatternValues.Solid })) { Count = 2U },
    new Borders(new Border(), new Border(new LeftBorder { Style = BorderStyleValues.Thin }, new RightBorder { Style = BorderStyleValues.Thin }, new TopBorder { Style = BorderStyleValues.Thin }, new BottomBorder { Style = BorderStyleValues.Thin })) { Count = 2U },
    new CellStyleFormats(new CellFormat()) { Count = 1U },
    new CellFormats(
        new CellFormat(),
        new CellFormat { FontId = 1U, FillId = 1U, BorderId = 1U, ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center } },
        new CellFormat { BorderId = 1U, ApplyBorder = true, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center } },
        new CellFormat { FillId = 1U, BorderId = 1U, ApplyFill = true, ApplyBorder = true },
        new CellFormat { NumberFormatId = 10U, ApplyNumberFormat = true }) { Count = 5U });

static void AddDrawing(WorksheetPart worksheet)
{
    var drawings = worksheet.AddNewPart<DrawingsPart>();
    worksheet.ChangeIdOfPart(drawings, "rIdDrawing");
    var image = drawings.AddImagePart(ImagePartType.Png);
    drawings.ChangeIdOfPart(image, "rIdImage");
    var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLneQAAAABJRU5ErkJggg==");
    using (var stream = image.GetStream(FileMode.Create, FileAccess.Write)) stream.Write(bytes);
    var relationshipId = drawings.GetIdOfPart(image);
    drawings.WorksheetDrawing = new Xdr.WorksheetDrawing(
        new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(new Xdr.ColumnId("0"), new Xdr.ColumnOffset("0"), new Xdr.RowId("1"), new Xdr.RowOffset("0")),
            new Xdr.ToMarker(new Xdr.ColumnId("2"), new Xdr.ColumnOffset("0"), new Xdr.RowId("3"), new Xdr.RowOffset("0")),
            new Xdr.Picture(new Xdr.BlipFill(new A.Blip { Embed = relationshipId })), new Xdr.ClientData()),
        new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(new Xdr.ColumnId("3"), new Xdr.ColumnOffset("0"), new Xdr.RowId("1"), new Xdr.RowOffset("0")),
            new Xdr.ToMarker(new Xdr.ColumnId("5"), new Xdr.ColumnOffset("0"), new Xdr.RowId("3"), new Xdr.RowOffset("0")),
            new Xdr.Shape(new Xdr.ShapeProperties()), new Xdr.ClientData()));
    drawings.WorksheetDrawing.Save();
    worksheet.Worksheet.Append(new Drawing { Id = worksheet.GetIdOfPart(drawings) });
    worksheet.Worksheet.Save();
}

static void NormalizePackage(string path)
{
    var temp = path + ".normalized";
    using (var sourceStream = File.OpenRead(path))
    using (var source = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: false))
    using (var targetStream = File.Create(temp))
    using (var target = new ZipArchive(targetStream, ZipArchiveMode.Create, leaveOpen: false))
    {
        foreach (var entry in source.Entries.OrderBy(item => item.FullName, StringComparer.Ordinal))
        {
            var copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            copy.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            copy.ExternalAttributes = entry.ExternalAttributes;
            using var input = entry.Open();
            using var outputStream = copy.Open();
            input.CopyTo(outputStream);
        }
    }
    File.Delete(path);
    File.Move(temp, path);
}
