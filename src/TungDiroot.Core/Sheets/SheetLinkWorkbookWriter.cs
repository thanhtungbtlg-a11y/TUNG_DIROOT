using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.SheetTools;

public static class SheetLinkWorkbookWriter
{
    private const uint HeaderStyle = 1U;
    private const uint TextStyle = 2U;
    private const uint NumberStyle = 3U;

    public static void Write(string path, SheetLinkWorkbook workbook, int blankRows = 10)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Đường dẫn Excel không hợp lệ.", nameof(path));
        if (workbook == null) throw new ArgumentNullException(nameof(workbook));
        if (blankRows < 0) throw new ArgumentOutOfRangeException(nameof(blankRows));

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook(new BookViews(new WorkbookView()));
        AddStyles(workbookPart);
        var sheets = workbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());

        var sheetRows = new List<SheetLinkSheetRow>(workbook.Sheets);
        var viewRows = new List<SheetLinkViewRow>(workbook.Views);
        var revisionRows = new List<SheetLinkRevisionRow>(workbook.Revisions);
        for (var index = 0; index < blankRows; index++)
        {
            sheetRows.Add(new SheetLinkSheetRow(false, SheetLinkAction.Create, null, string.Empty, string.Empty, string.Empty, string.Empty));
            viewRows.Add(new SheetLinkViewRow(false, SheetLinkAction.Create, null, string.Empty, SheetLinkViewKind.Drafting,
                string.Empty, string.Empty, string.Empty, null, null));
            revisionRows.Add(new SheetLinkRevisionRow(false, SheetLinkAction.Create, null, string.Empty, null,
                string.Empty, string.Empty, false, string.Empty, string.Empty, SheetLinkRevisionVisibility.CloudAndTag));
        }

        AddSheet(workbookPart, sheets, 1U, SheetLinkWorkbookSchema.SheetsSheet,
            SheetLinkWorkbookSchema.SheetHeaders, SheetWidths(), SheetValues(sheetRows), 7);
        AddSheet(workbookPart, sheets, 2U, SheetLinkWorkbookSchema.ViewsSheet,
            SheetLinkWorkbookSchema.ViewHeaders, ViewWidths(), ViewValues(viewRows), 10);
        AddSheet(workbookPart, sheets, 3U, SheetLinkWorkbookSchema.RevisionsSheet,
            SheetLinkWorkbookSchema.RevisionHeaders, new[] { 10D, 12D, 34D, 22D, 20D, 40D, 22D, 12D, 24D, 24D, 20D },
            RevisionValues(revisionRows), 11);
        workbookPart.Workbook.Save();
    }

    private static IEnumerable<CellValueSpec[]> SheetValues(IEnumerable<SheetLinkSheetRow> rows)
    {
        foreach (var row in rows)
        {
            yield return new[]
            {
                Text(row.Apply ? "TRUE" : "FALSE"), Text(Action(row.Action)), Text(row.UniqueId), Text(row.SheetNumber),
                Text(row.SheetName), Text(row.TitleBlockType), Text(row.RevisionSequences)
            };
        }
    }

    private static IEnumerable<CellValueSpec[]> ViewValues(IEnumerable<SheetLinkViewRow> rows)
    {
        foreach (var row in rows)
        {
            yield return new[]
            {
                Text(row.Apply ? "TRUE" : "FALSE"), Text(Action(row.Action)), Text(row.UniqueId), Text(row.ViewName),
                Text(ViewKind(row.ViewKind)), Text(row.LevelName), Text(row.ViewTemplate), Text(row.SheetNumber),
                Number(row.CenterXMillimeters), Number(row.CenterYMillimeters)
            };
        }
    }

    private static IEnumerable<CellValueSpec[]> RevisionValues(IEnumerable<SheetLinkRevisionRow> rows)
    {
        foreach (var row in rows)
            yield return new[]
            {
                Text(row.Apply ? "TRUE" : "FALSE"), Text(Action(row.Action)), Text(row.UniqueId), Text(row.RevisionKey),
                Number(row.SequenceNumber), Text(row.Description), Text(row.RevisionDate), Text(row.Issued ? "TRUE" : "FALSE"),
                Text(row.IssuedBy), Text(row.IssuedTo), Text(row.Visibility switch
                {
                    SheetLinkRevisionVisibility.TagOnly => "TAG_ONLY",
                    SheetLinkRevisionVisibility.Hidden => "HIDDEN",
                    _ => "CLOUD_AND_TAG"
                })
            };
    }

    private static void AddSheet(
        WorkbookPart workbookPart,
        DocumentFormat.OpenXml.Spreadsheet.Sheets sheets,
        uint sheetId,
        string name,
        string[] headers,
        double[] widths,
        IEnumerable<CellValueSpec[]> values,
        int columnCount)
    {
        var part = workbookPart.AddNewPart<WorksheetPart>();
        var view = new SheetView { WorkbookViewId = 0U, ShowGridLines = false };
        view.Append(new Pane
        {
            VerticalSplit = 1D,
            TopLeftCell = "A2",
            ActivePane = PaneValues.BottomLeft,
            State = PaneStateValues.Frozen
        });
        var columns = new Columns();
        for (var index = 0; index < widths.Length; index++)
            columns.Append(new Column { Min = (uint)index + 1U, Max = (uint)index + 1U, Width = widths[index], CustomWidth = true });
        var data = new SheetData();
        part.Worksheet = new Worksheet(new SheetViews(view), columns, data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = sheetId, Name = name });

        var header = new Row { RowIndex = 1U, Height = 25D, CustomHeight = true };
        for (var column = 0; column < headers.Length; column++)
            header.Append(CreateTextCell(column + 1, 1U, headers[column], HeaderStyle));
        data.Append(header);

        uint rowIndex = 2U;
        foreach (var rowValues in values)
        {
            var row = new Row { RowIndex = rowIndex };
            for (var column = 0; column < rowValues.Length; column++)
            {
                var value = rowValues[column];
                row.Append(value.IsNumber
                    ? CreateNumberCell(column + 1, rowIndex, value.Value)
                    : CreateTextCell(column + 1, rowIndex, value.Value, TextStyle));
            }
            data.Append(row);
            rowIndex++;
        }

        part.Worksheet.Append(new AutoFilter { Reference = "A1:" + ColumnName(columnCount) + Math.Max(1U, rowIndex - 1U) });
        part.Worksheet.Save();
    }

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var part = workbookPart.AddNewPart<WorkbookStylesPart>();
        var fonts = new Fonts(
            new Font(new FontSize { Val = 11D }, new FontName { Val = "Aptos" }),
            new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }, new FontSize { Val = 11D }, new FontName { Val = "Aptos" }));
        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FF7030A0" }) { PatternType = PatternValues.Solid }));
        var borders = new Borders(new Border(), new Border(
            new LeftBorder { Style = BorderStyleValues.Thin }, new RightBorder { Style = BorderStyleValues.Thin },
            new TopBorder { Style = BorderStyleValues.Thin }, new BottomBorder { Style = BorderStyleValues.Thin }, new DiagonalBorder()));
        var formats = new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1U, FillId = 2U, BorderId = 1U, ApplyFont = true, ApplyFill = true, ApplyBorder = true,
                Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } },
            new CellFormat { BorderId = 1U, ApplyBorder = true, Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } },
            new CellFormat { BorderId = 1U, ApplyBorder = true, NumberFormatId = 4U, ApplyNumberFormat = true,
                Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } });
        part.Stylesheet = new Stylesheet(fonts, fills, borders, new CellStyleFormats(new CellFormat()), formats);
        part.Stylesheet.Save();
    }

    private static Cell CreateTextCell(int column, uint row, string? value, uint style) => new()
    {
        CellReference = ColumnName(column) + row,
        DataType = CellValues.InlineString,
        StyleIndex = style,
        InlineString = new InlineString(new Text(value ?? string.Empty))
    };

    private static Cell CreateNumberCell(int column, uint row, string value) => new()
    {
        CellReference = ColumnName(column) + row,
        DataType = CellValues.Number,
        StyleIndex = NumberStyle,
        CellValue = new CellValue(value)
    };

    private static CellValueSpec Text(string? value) => new(false, value ?? string.Empty);
    private static CellValueSpec Number(double? value) => value.HasValue
        ? new CellValueSpec(true, value.Value.ToString("0.###############", CultureInfo.InvariantCulture))
        : Text(string.Empty);

    private static string Action(SheetLinkAction action) => action switch
    {
        SheetLinkAction.Create => "CREATE",
        SheetLinkAction.Update => "UPDATE",
        _ => "SKIP"
    };

    private static string ViewKind(SheetLinkViewKind kind) => kind switch
    {
        SheetLinkViewKind.FloorPlan => "FLOOR_PLAN",
        SheetLinkViewKind.CeilingPlan => "CEILING_PLAN",
        _ => "DRAFTING"
    };

    private static double[] SheetWidths() => new[] { 10D, 12D, 34D, 18D, 34D, 30D, 24D };
    private static double[] ViewWidths() => new[] { 10D, 12D, 34D, 34D, 18D, 24D, 30D, 18D, 18D, 18D };

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

    private sealed record CellValueSpec(bool IsNumber, string Value);
}
