using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

if (args.Length != 1) throw new ArgumentException("Usage: TungDiroot.ParameterSample <output.xlsx>");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
if (File.Exists(output)) File.Delete(output);

using var document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
var workbookPart = document.AddWorkbookPart();
document.ChangeIdOfPart(workbookPart, "rIdWorkbook");
workbookPart.Workbook = new Workbook();
var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
workbookPart.ChangeIdOfPart(stylesPart, "rIdStyles");
stylesPart.Stylesheet = CreateStyles();
stylesPart.Stylesheet.Save();

var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
workbookPart.ChangeIdOfPart(worksheetPart, "rIdSheet1");
var columns = new Columns(
    Width(1, 12), Width(2, 22), Width(3, 28), Width(4, 14), Width(5, 18), Width(6, 48));
var sheetData = new SheetData(
    Row(1,
        Cell("A1", "ENABLED", 1),
        Cell("B1", "PARAMETER_GROUP", 1),
        Cell("C1", "PARAMETER_NAME", 1),
        Cell("D1", "BINDING", 1),
        Cell("E1", "REVIT_GROUP", 1),
        Cell("F1", "CATEGORIES", 1)),
    Row(2,
        Cell("A2", "FALSE"),
        Cell("B2", "Identity"),
        Cell("C2", "Asset_Code"),
        Cell("D2", "INSTANCE"),
        Cell("E2", "PG_DATA"),
        Cell("F2", "OST_Doors;OST_Windows")),
    Row(3,
        Cell("A3", "FALSE"),
        Cell("B3", "Product"),
        Cell("C3", "Manufacturer"),
        Cell("D3", "TYPE"),
        Cell("E3", "PG_IDENTITY_DATA"),
        Cell("F3", "OST_Doors;OST_Windows;OST_Furniture")));
worksheetPart.Worksheet = new Worksheet(columns, sheetData, new AutoFilter { Reference = "A1:F3" });
worksheetPart.Worksheet.Save();

var sheets = workbookPart.Workbook.AppendChild(new Sheets());
sheets.Append(new Sheet
{
    Id = workbookPart.GetIdOfPart(worksheetPart),
    SheetId = 1U,
    Name = "PARAMETER_LOADER"
});
workbookPart.Workbook.Save();
document.Dispose();
NormalizePackage(output);
Console.WriteLine(output);

static Column Width(uint index, double width) => new()
{
    Min = index,
    Max = index,
    Width = width,
    CustomWidth = true
};

static Row Row(uint index, params Cell[] cells) => new(cells) { RowIndex = index };

static Cell Cell(string reference, string text, uint style = 0) => new()
{
    CellReference = reference,
    DataType = CellValues.InlineString,
    StyleIndex = style,
    InlineString = new InlineString(new Text(text))
};

static Stylesheet CreateStyles() => new(
    new Fonts(new Font(), new Font(new Bold(), new Color { Rgb = "FFFFFFFF" })) { Count = 2U },
    new Fills(
        new Fill(new PatternFill { PatternType = PatternValues.None }),
        new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
        new Fill(new PatternFill(new ForegroundColor { Rgb = "FF7030A0" }, new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid })) { Count = 3U },
    new Borders(new Border()) { Count = 1U },
    new CellStyleFormats(new CellFormat()) { Count = 1U },
    new CellFormats(new CellFormat(), new CellFormat
    {
        FontId = 1U,
        FillId = 2U,
        ApplyFont = true,
        ApplyFill = true,
        ApplyAlignment = true,
        Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center }
    }) { Count = 2U });

static void NormalizePackage(string path)
{
    var temporary = path + ".normalized";
    using (var sourceStream = File.OpenRead(path))
    using (var source = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: false))
    using (var targetStream = File.Create(temporary))
    using (var target = new ZipArchive(targetStream, ZipArchiveMode.Create, leaveOpen: false))
    {
        foreach (var entry in source.Entries.OrderBy(item => item.FullName, StringComparer.Ordinal))
        {
            var copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            copy.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            copy.ExternalAttributes = entry.ExternalAttributes;
            using var input = entry.Open();
            using var output = copy.Open();
            input.CopyTo(output);
        }
    }

    File.Delete(path);
    File.Move(temporary, path);
}
