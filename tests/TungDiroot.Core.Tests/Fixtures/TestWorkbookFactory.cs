using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace TungDiroot.Core.Tests.Fixtures;

internal static class TestWorkbookFactory
{
    public static TestWorkbookFixture CreateCatalogWorkbook()
    {
        return CreateCatalogWorkbook(SpreadsheetDocumentType.Workbook, ".xlsx");
    }

    public static TestWorkbookFixture CreateMacroEnabledCatalogWorkbook()
    {
        return CreateCatalogWorkbook(SpreadsheetDocumentType.MacroEnabledWorkbook, ".xlsm");
    }

    public static TestWorkbookFixture CreateCellReferenceCatalogWorkbook()
    {
        return CreateCatalogWorkbook(SpreadsheetDocumentType.Workbook, ".xlsx", includeDimensions: false);
    }

    public static TestWorkbookFixture CreateMultiAreaCatalogWorkbook()
    {
        return CreateCatalogWorkbook(SpreadsheetDocumentType.Workbook, ".xlsx", includeDimensions: true, multiplePrintAreas: true);
    }

    public static TestWorkbookFixture CreateParameterRuleWorkbook(
        string[] headers,
        string[][] rows,
        bool macroEnabled = false,
        string worksheetName = "PARAMETER_LOADER")
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var extension = macroEnabled ? ".xlsm" : ".xlsx";
        var path = Path.Combine(directory, "parameter-loader" + extension);
        var documentType = macroEnabled ? SpreadsheetDocumentType.MacroEnabledWorkbook : SpreadsheetDocumentType.Workbook;

        using (var document = SpreadsheetDocument.Create(path, documentType))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var shared = workbookPart.AddNewPart<SharedStringTablePart>();
            shared.SharedStringTable = new SharedStringTable();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            sheetData.Append(CreateParameterRow(1U, headers, shared.SharedStringTable, useSharedStrings: false));
            for (var index = 0; index < rows.Length; index++)
            {
                sheetData.Append(CreateParameterRow((uint)index + 2U, rows[index], shared.SharedStringTable, useSharedStrings: true));
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);
            workbookPart.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = worksheetName
            }));
            shared.SharedStringTable.Save();
            workbookPart.Workbook.Save();
        }

        return new TestWorkbookFixture(path, directory);
    }

    public static TestWorkbookFixture CreateInvalidParameterRuleWorkbook(string kind)
    {
        var headers = new[] { "ENABLED", "PARAMETER_GROUP", "PARAMETER_NAME", "BINDING", "REVIT_GROUP", "CATEGORIES" };
        if (string.Equals(kind, "missing-sheet", StringComparison.Ordinal))
            return CreateParameterRuleWorkbook(headers, Array.Empty<string[]>(), worksheetName: "OTHER");
        if (string.Equals(kind, "missing-header", StringComparison.Ordinal))
            return CreateParameterRuleWorkbook(headers.Take(5).ToArray(), Array.Empty<string[]>());
        throw new ArgumentOutOfRangeException(nameof(kind));
    }

    public static TestWorkbookFixture CreateFormattedWorkbook()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "formatted.xlsx");

        using (var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(
                new Fonts(
                    new Font(),
                    new Font(new Bold(), new FontSize { Val = 14D }, new FontName { Val = "Arial" }),
                    new Font(new Bold { Val = false }, new Italic { Val = false }, new Underline { Val = UnderlineValues.None }),
                    new Font(new Bold(), new Italic(), new Underline())) { Count = 4U },
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None })) { Count = 1U },
                new Borders(new Border()) { Count = 1U },
                new CellStyleFormats(new CellFormat()) { Count = 1U },
                new CellFormats(
                    new CellFormat(),
                    new CellFormat { FontId = 1U, ApplyFont = true, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center } },
                    new CellFormat { NumberFormatId = 10U, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 20U, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 14U, ApplyNumberFormat = true },
                    new CellFormat { FontId = 2U, ApplyFont = true },
                    new CellFormat { FontId = 3U, ApplyFont = true }) { Count = 7U });
            stylesPart.Stylesheet.Save();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(
                new Columns(new Column { Min = 3U, Max = 3U, Hidden = true, Width = 18D, CustomWidth = true }),
                new SheetData(
                    new Row(
                        new Cell { CellReference = "A1", DataType = CellValues.InlineString, StyleIndex = 1U, InlineString = new InlineString(new Text("DANH MỤC BẢN VẼ")) }) { RowIndex = 1U, Height = 25D, CustomHeight = true },
                    new Row(
                        new Cell { CellReference = "A2", DataType = CellValues.SharedString, CellValue = new CellValue("0") },
                        new Cell { CellReference = "B2", DataType = CellValues.Boolean, CellValue = new CellValue("1") },
                        new Cell { CellReference = "C2", StyleIndex = 2U, CellValue = new CellValue("0.25") },
                        new Cell { CellReference = "D2", CellFormula = new CellFormula("SUM(C2:C2)") },
                        new Cell { CellReference = "E2", StyleIndex = 3U, CellValue = new CellValue("0.5") },
                        new Cell { CellReference = "F2", StyleIndex = 4U, CellValue = new CellValue("45292") },
                        new Cell { CellReference = "G2", StyleIndex = 5U, CellValue = new CellValue("false-font") },
                        new Cell { CellReference = "H2", StyleIndex = 6U, CellValue = new CellValue("true-font") }),
                    new Row(new Cell { CellReference = "A3", CellFormula = new CellFormula("1+1"), CellValue = new CellValue("2") }) { RowIndex = 3U, Hidden = true }),
                new MergeCells(new MergeCell { Reference = "A1:D1" }));
            worksheetPart.Worksheet.InsertAt(new SheetDimension { Reference = "A1:D6" }, 0);

            var sharedStrings = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStrings.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("Shared value")));
            sharedStrings.SharedStringTable.Save();

            workbookPart.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = "PCCC"
            }));
            workbookPart.Workbook.Save();
        }

        return new TestWorkbookFixture(path, directory);
    }

    public static TestWorkbookFixture CreateWorkbookWithImageDrawing()
    {
        var fixture = CreateFormattedWorkbook();
        using (var document = SpreadsheetDocument.Open(fixture.Path, true))
        {
            var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
            var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
            var imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
            using (var imageStream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
            {
                var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLneQAAAABJRU5ErkJggg==");
                imageStream.Write(png, 0, png.Length);
            }

            var relationshipId = drawingsPart.GetIdOfPart(imagePart);
            drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing(
                new Xdr.TwoCellAnchor(
                    new Xdr.FromMarker(new Xdr.ColumnId("0"), new Xdr.ColumnOffset("0"), new Xdr.RowId("0"), new Xdr.RowOffset("0")),
                    new Xdr.ToMarker(new Xdr.ColumnId("1"), new Xdr.ColumnOffset("0"), new Xdr.RowId("1"), new Xdr.RowOffset("0")),
                    new Xdr.Picture(new Xdr.BlipFill(new A.Blip { Embed = relationshipId })),
                    new Xdr.ClientData()));
            drawingsPart.WorksheetDrawing.Save();
            worksheetPart.Worksheet.Append(new Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
            worksheetPart.Worksheet.Save();
        }

        return fixture;
    }

    public static TestWorkbookFixture CreateWorkbookWithOneCellImageDrawing(string zeroBasedColumn, string zeroBasedRow)
    {
        return CreateWorkbookWithImageAnchor(relationshipId => new Xdr.OneCellAnchor(
            new Xdr.FromMarker(new Xdr.ColumnId(zeroBasedColumn), new Xdr.ColumnOffset("0"), new Xdr.RowId(zeroBasedRow), new Xdr.RowOffset("0")),
            new Xdr.Extent { Cx = 1L, Cy = 1L },
            new Xdr.Picture(new Xdr.BlipFill(new A.Blip { Embed = relationshipId })),
            new Xdr.ClientData()));
    }

    public static TestWorkbookFixture CreateWorkbookWithDrawingExtensionList()
    {
        var fixture = CreateWorkbookWithImageDrawing();
        using (var document = SpreadsheetDocument.Open(fixture.Path, true))
        {
            var drawingsPart = document.WorkbookPart!.WorksheetParts.Single().DrawingsPart!;
            drawingsPart.WorksheetDrawing.Append(new OpenXmlUnknownElement("extLst", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"));
            drawingsPart.WorksheetDrawing.Save();
        }

        return fixture;
    }

    public static TestWorkbookFixture CreateWorkbookWithMalformedTwoCellImageDrawing()
    {
        return CreateWorkbookWithImageAnchor(relationshipId => new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(new Xdr.ColumnId("0"), new Xdr.ColumnOffset("0"), new Xdr.RowId("0"), new Xdr.RowOffset("0")),
            new Xdr.ToMarker(new Xdr.ColumnId("not-a-number"), new Xdr.ColumnOffset("0"), new Xdr.RowId("0"), new Xdr.RowOffset("0")),
            new Xdr.Picture(new Xdr.BlipFill(new A.Blip { Embed = relationshipId })),
            new Xdr.ClientData()));
    }

    public static TestWorkbookFixture CreateWorkbookWithShapeDrawings()
    {
        var fixture = CreateFormattedWorkbook();
        using (var document = SpreadsheetDocument.Open(fixture.Path, true))
        {
            var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
            var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
            var imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
            using (var imageStream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
            {
                var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLneQAAAABJRU5ErkJggg==");
                imageStream.Write(png, 0, png.Length);
            }

            var previewRelationshipId = drawingsPart.GetIdOfPart(imagePart);
            drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing(
                CreateShapeAnchor(0, 3, 1, 4, previewRelationshipId),
                CreateShapeAnchor(2, 3, 3, 4, null));
            drawingsPart.WorksheetDrawing.Save();
            worksheetPart.Worksheet.Append(new Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
            worksheetPart.Worksheet.Save();
        }

        return fixture;
    }

    private static Xdr.TwoCellAnchor CreateShapeAnchor(int fromColumn, int fromRow, int toColumn, int toRow, string? previewRelationshipId)
    {
        var properties = new Xdr.ShapeProperties();
        if (previewRelationshipId != null)
        {
            properties.Append(new A.BlipFill(new A.Blip { Embed = previewRelationshipId }));
        }

        return new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(new Xdr.ColumnId(fromColumn.ToString()), new Xdr.ColumnOffset("0"), new Xdr.RowId(fromRow.ToString()), new Xdr.RowOffset("0")),
            new Xdr.ToMarker(new Xdr.ColumnId(toColumn.ToString()), new Xdr.ColumnOffset("0"), new Xdr.RowId(toRow.ToString()), new Xdr.RowOffset("0")),
            new Xdr.Shape(properties),
            new Xdr.ClientData());
    }

    private static TestWorkbookFixture CreateWorkbookWithImageAnchor(Func<string, OpenXmlElement> createAnchor)
    {
        var fixture = CreateFormattedWorkbook();
        using (var document = SpreadsheetDocument.Open(fixture.Path, true))
        {
            var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
            var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
            var imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
            using (var imageStream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
            {
                var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLneQAAAABJRU5ErkJggg==");
                imageStream.Write(png, 0, png.Length);
            }

            drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing(createAnchor(drawingsPart.GetIdOfPart(imagePart)));
            drawingsPart.WorksheetDrawing.Save();
            worksheetPart.Worksheet.Append(new Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
            worksheetPart.Worksheet.Save();
        }

        return fixture;
    }

    private static TestWorkbookFixture CreateCatalogWorkbook(
        SpreadsheetDocumentType documentType,
        string extension,
        bool includeDimensions = true,
        bool multiplePrintAreas = false)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "catalog" + extension);

        using (var document = SpreadsheetDocument.Create(path, documentType))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var pccc = workbookPart.AddNewPart<WorksheetPart>();
            pccc.Worksheet = CreateWorksheet(includeDimensions ? "A1:H25" : null);

            var equipment = workbookPart.AddNewPart<WorksheetPart>();
            equipment.Worksheet = CreateWorksheet(includeDimensions ? "A2:F18" : null);

            workbookPart.Workbook.AppendChild(new Sheets(
                new Sheet
                {
                    Id = workbookPart.GetIdOfPart(pccc),
                    SheetId = 1U,
                    Name = "PCCC"
                },
                new Sheet
                {
                    Id = workbookPart.GetIdOfPart(equipment),
                    SheetId = 2U,
                    Name = "Thiết bị"
                }));

            workbookPart.Workbook.AppendChild(new DefinedNames(
                new DefinedName
                {
                    Name = "_xlnm.Print_Area",
                    LocalSheetId = 0U,
                    Text = multiplePrintAreas
                        ? "'PCCC'!$A$1:$B$2,'PCCC'!$D$4:$E$5"
                        : "'PCCC'!$A$1:$H$25"
                },
                new DefinedName
                {
                    Name = "Equipment",
                    Text = "'Thiết bị'!$A$2:$F$18"
                },
                new DefinedName
                {
                    Name = "SingleCell",
                    Text = "'Thiết bị'!$F$18"
                }));

            workbookPart.Workbook.Save();
        }

        return new TestWorkbookFixture(path, directory);
    }

    private static Row CreateParameterRow(uint rowIndex, string[] values, SharedStringTable sharedStrings, bool useSharedStrings)
    {
        var row = new Row { RowIndex = rowIndex };
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index] ?? string.Empty;
            var reference = ColumnName(index + 1) + rowIndex;
            if (useSharedStrings && index % 2 == 1)
            {
                var sharedIndex = sharedStrings.ChildElements.Count;
                sharedStrings.AppendChild(new SharedStringItem(new Text(value)));
                row.AppendChild(new Cell { CellReference = reference, DataType = CellValues.SharedString, CellValue = new CellValue(sharedIndex.ToString()) });
            }
            else if (string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                row.AppendChild(new Cell { CellReference = reference, DataType = CellValues.Boolean, CellValue = new CellValue(string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) ? "1" : "0") });
            }
            else
            {
                row.AppendChild(new Cell { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value)) });
            }
        }
        return row;
    }

    private static string ColumnName(int number)
    {
        var name = string.Empty;
        while (number > 0)
        {
            number--;
            name = (char)('A' + number % 26) + name;
            number /= 26;
        }
        return name;
    }

    private static Worksheet CreateWorksheet(string? dimension)
    {
        var sheetData = new SheetData();
        if (dimension == null)
        {
            sheetData.Append(
                new Row(new Cell { CellReference = "B3" }) { RowIndex = 3U },
                new Row(new Cell { CellReference = "D9" }) { RowIndex = 9U });
        }

        var worksheet = new Worksheet(sheetData);
        if (dimension != null)
        {
            worksheet.InsertAt(new SheetDimension { Reference = dimension }, 0);
        }

        return worksheet;
    }
}

internal sealed class TestWorkbookFixture : IDisposable
{
    private readonly string _directory;

    public TestWorkbookFixture(string path, string directory)
    {
        Path = path;
        _directory = directory;
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
