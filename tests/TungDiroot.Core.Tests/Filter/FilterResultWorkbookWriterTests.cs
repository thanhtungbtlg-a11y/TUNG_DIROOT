using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TungDiroot.Core.Filter;
using Xunit;

namespace TungDiroot.Core.Tests.Filter;

public sealed class FilterResultWorkbookWriterTests
{
    [Fact]
    public void Writer_creates_flat_filterable_results_with_identity_and_requested_values()
    {
        WithWorkbook(path =>
        {
            var match = Match(new Dictionary<string, FilterValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["instance:builtin:comments"] = new(true, FilterValueKind.String, "Ghi chú", "Ghi chú"),
                ["type:builtin:type_mark"] = new(true, FilterValueKind.String, "T-01", "T-01")
            });
            var columns = new[]
            {
                new FilterExportColumn("instance:builtin:comments", "Comments"),
                new FilterExportColumn("type:builtin:type_mark", "Type Mark")
            };

            FilterResultWorkbookWriter.Write(path, new[] { match }, columns);

            using var document = SpreadsheetDocument.Open(path, false);
            var part = ResultsPart(document);
            var rows = part.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
            Assert.Equal(new[]
            {
                "ElementId", "UniqueId", "Category", "Family", "Type", "Level", "Comments", "Type Mark"
            }, Values(rows[0]));
            Assert.Equal(new[]
            {
                "42", "uid-42", "Pipes", "Pipe", "Standard", "Level 1", "Ghi chú", "T-01"
            }, Values(rows[1]));
            Assert.Equal("A1:H2", part.Worksheet.GetFirstChild<AutoFilter>()?.Reference?.Value);
            Assert.Equal(PaneStateValues.Frozen, part.Worksheet.Descendants<Pane>().Single().State?.Value);
        });
    }

    [Fact]
    public void Writer_uses_internal_value_when_display_is_missing_and_empty_for_missing_parameter()
    {
        WithWorkbook(path =>
        {
            var match = Match(new Dictionary<string, FilterValue>
            {
                ["instance:number"] = new(true, FilterValueKind.Number, null, "12.5"),
                ["instance:missing"] = FilterValue.Missing(FilterValueKind.String)
            });
            FilterResultWorkbookWriter.Write(path, new[] { match }, new[]
            {
                new FilterExportColumn("instance:number", "Number"),
                new FilterExportColumn("instance:missing", "Missing")
            });

            using var document = SpreadsheetDocument.Open(path, false);
            var row = ResultsPart(document).Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ElementAt(1);
            Assert.Equal("12.5", Values(row)[6]);
            Assert.Equal(string.Empty, Values(row)[7]);
        });
    }

    [Fact]
    public void Writer_rejects_duplicate_parameter_columns()
    {
        WithWorkbook(path =>
        {
            var duplicate = new[]
            {
                new FilterExportColumn("instance:mark", "Mark"),
                new FilterExportColumn("INSTANCE:MARK", "Mark again")
            };
            Assert.Throws<ArgumentException>(() =>
                FilterResultWorkbookWriter.Write(path, new[] { Match(new Dictionary<string, FilterValue>()) }, duplicate));
        });
    }

    private static FilterMatch Match(IReadOnlyDictionary<string, FilterValue> values)
    {
        var snapshot = new FilterElementSnapshot(
            "uid-42", 42, 100, "Pipes", "Pipe", "Standard", "Level 1", values);
        return new FilterMatch(snapshot, Array.Empty<FilterRule>());
    }

    private static WorksheetPart ResultsPart(SpreadsheetDocument document)
    {
        var workbook = document.WorkbookPart!;
        var sheet = workbook.Workbook.Sheets!.Elements<Sheet>()
            .Single(item => item.Name == FilterResultWorkbookWriter.ResultsSheet);
        return (WorksheetPart)workbook.GetPartById(sheet.Id!);
    }

    private static string[] Values(Row row) => row.Elements<Cell>()
        .Select(cell => cell.InlineString?.Text?.Text ?? string.Empty)
        .ToArray();

    private static void WithWorkbook(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG_FILTER-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            action(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
