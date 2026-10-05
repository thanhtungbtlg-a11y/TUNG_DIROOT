using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TungDiroot.Core.SheetTools;
using Xunit;

namespace TungDiroot.Core.Tests.SheetTools;

public sealed class SheetLinkWorkbookRoundTripTests
{
    [Fact]
    public void Round_trip_preserves_sheet_view_and_numeric_coordinates()
    {
        WithPath(path =>
        {
            var source = new SheetLinkWorkbook(
                new[] { new SheetLinkSheetRow(true, SheetLinkAction.Update, "sheet-uid", "A101", "Mặt bằng", "A1 Metric", "1; 3") },
                new[] { new SheetLinkViewRow(true, SheetLinkAction.Create, null, "Mặt bằng tầng 1", SheetLinkViewKind.FloorPlan,
                    "Level 1", "MEP", "A101", 420.5, 297.25) });

            SheetLinkWorkbookWriter.Write(path, source, blankRows: 0);
            var actual = SheetLinkWorkbookReader.Read(path);

            Assert.Equal(source.Sheets.Single(), actual.Sheets.Single());
            Assert.Equal(source.Views.Single(), actual.Views.Single());
            Assert.Equal(420.5, actual.Views.Single().CenterXMillimeters);
        });
    }

    [Fact]
    public void Writer_adds_create_templates_and_professional_sheet_controls()
    {
        WithPath(path =>
        {
            SheetLinkWorkbookWriter.Write(path, new SheetLinkWorkbook(Array.Empty<SheetLinkSheetRow>(), Array.Empty<SheetLinkViewRow>()), blankRows: 3);

            using var document = SpreadsheetDocument.Open(path, false);
            var workbookPart = document.WorkbookPart!;
            foreach (var name in new[] { SheetLinkWorkbookSchema.SheetsSheet, SheetLinkWorkbookSchema.ViewsSheet })
            {
                var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == name);
                var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                Assert.Equal(4, part.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().Count());
                Assert.Equal(PaneStateValues.Frozen, part.Worksheet.Descendants<Pane>().Single().State?.Value);
                Assert.NotNull(part.Worksheet.GetFirstChild<AutoFilter>());
                Assert.True(part.Worksheet.Descendants<Cell>().First().StyleIndex?.Value > 0);
            }

            var imported = SheetLinkWorkbookReader.Read(path);
            Assert.Equal(3, imported.Sheets.Count);
            Assert.All(imported.Sheets, row => { Assert.False(row.Apply); Assert.Equal(SheetLinkAction.Create, row.Action); });
        });
    }

    [Fact]
    public void Reader_accepts_shared_strings_written_by_excel()
    {
        WithPath(path =>
        {
            SheetLinkWorkbookWriter.Write(path,
                new SheetLinkWorkbook(new[] { new SheetLinkSheetRow(true, SheetLinkAction.Create, null, "A101", "Old", "A1", "") },
                    Array.Empty<SheetLinkViewRow>()), blankRows: 0);
            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var workbookPart = document.WorkbookPart!;
                var shared = workbookPart.AddNewPart<SharedStringTablePart>();
                shared.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("Tên từ Excel")));
                shared.SharedStringTable.Save();
                var part = Worksheet(workbookPart, SheetLinkWorkbookSchema.SheetsSheet);
                var cell = part.Worksheet.Descendants<Cell>().Single(item => item.CellReference == "E2");
                cell.InlineString = null;
                cell.DataType = CellValues.SharedString;
                cell.CellValue = new CellValue("0");
                part.Worksheet.Save();
            }

            Assert.Equal("Tên từ Excel", SheetLinkWorkbookReader.Read(path).Sheets.Single().SheetName);
        });
    }

    [Fact]
    public void Invalid_action_is_rejected_with_stable_code()
    {
        WithPath(path =>
        {
            SheetLinkWorkbookWriter.Write(path,
                new SheetLinkWorkbook(new[] { new SheetLinkSheetRow(true, SheetLinkAction.Create, null, "A101", "Tên", "A1", "") },
                    Array.Empty<SheetLinkViewRow>()), blankRows: 0);
            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var part = Worksheet(document.WorkbookPart!, SheetLinkWorkbookSchema.SheetsSheet);
                var cell = part.Worksheet.Descendants<Cell>().Single(item => item.CellReference == "B2");
                cell.InlineString = new InlineString(new Text("DELETE"));
                cell.CellValue = null;
                cell.DataType = CellValues.InlineString;
                part.Worksheet.Save();
            }

            var error = Assert.Throws<InvalidDataException>(() => SheetLinkWorkbookReader.Read(path));
            Assert.Contains("SHEETLINK_ACTION_INVALID", error.Message);
        });
    }

    private static WorksheetPart Worksheet(WorkbookPart workbookPart, string name)
    {
        var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == name);
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
    }

    private static void WithPath(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-SheetLink-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try { action(path); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
