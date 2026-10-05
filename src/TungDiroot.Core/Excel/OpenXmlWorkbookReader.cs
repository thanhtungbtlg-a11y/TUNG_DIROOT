using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.Excel;

public sealed class OpenXmlWorkbookReader : IWorkbookReader
{
    private const FileShare RequiredFileShare = FileShare.ReadWrite | FileShare.Delete;
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const int MaximumExcelColumn = 16384;
    private const int MaximumExcelRow = 1048576;
    private const long MaximumMaterializedCellCount = 250_000L;
    private readonly Func<string, FileShare, FileStream> _openReadStream;

    public OpenXmlWorkbookReader()
        : this(OpenReadStream)
    {
    }

    internal OpenXmlWorkbookReader(Func<string, FileShare, FileStream> openReadStream)
    {
        _openReadStream = openReadStream ?? throw new ArgumentNullException(nameof(openReadStream));
    }

    public WorkbookCatalog ReadCatalog(string path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        ValidateWorkbookExtension(path);

        using var stream = _openReadStream(path, RequiredFileShare);
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("The workbook package has no workbook part.");
        var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>().ToArray() ?? Array.Empty<Sheet>();
        var worksheetSources = sheets.Select(sheet => GetWorksheetSource(workbookPart, sheet)).ToArray();
        var regions = new List<WorkbookRegion>();

        foreach (var worksheet in worksheetSources)
        {
            var usedRange = GetUsedRange(worksheet.Part);
            if (usedRange != null)
            {
                regions.Add(new WorkbookRegion(
                    RegionKind.UsedRange,
                    worksheet.Name,
                    null,
                    usedRange));
            }
        }

        foreach (var definedName in workbookPart.Workbook.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>())
        {
            var name = definedName.Name?.Value;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var kind = string.Equals(name, "_xlnm.Print_Area", StringComparison.Ordinal)
                ? RegionKind.PrintArea
                : RegionKind.NamedRange;
            foreach (var rangeText in SplitRangeList(definedName.InnerText))
            {
                if (TryParseQualifiedRange(rangeText, out var worksheetName, out var reference))
                {
                    regions.Add(new WorkbookRegion(kind, worksheetName, kind == RegionKind.NamedRange ? name : null, reference));
                }
            }
        }

        return new WorkbookCatalog(
            worksheetSources.Select(x => new WorksheetCatalog(x.Name)).ToArray(),
            regions.ToArray());
    }

    public WorkbookRangeModel ReadRange(string path, string worksheetName, ExcelRangeReference range, RangeReadOptions options)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (worksheetName == null)
        {
            throw new ArgumentNullException(nameof(worksheetName));
        }

        if (range == null)
        {
            throw new ArgumentNullException(nameof(range));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        range = new ExcelRangeReference(range.Start, range.End);
        ValidateMaterializationSize(range);

        ValidateWorkbookExtension(path);
        using var stream = _openReadStream(path, RequiredFileShare);
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("The workbook package has no workbook part.");
        var sheet = (workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>())
            .FirstOrDefault(candidate => string.Equals(candidate.Name?.Value, worksheetName, StringComparison.Ordinal));
        if (sheet == null)
        {
            throw new ArgumentException("The workbook does not contain the requested worksheet.", nameof(worksheetName));
        }

        var worksheetPart = GetWorksheetSource(workbookPart, sheet).Part;
        var warnings = new List<WorkbookReadWarning>();
        var styleReader = new OpenXmlStyleReader(workbookPart.WorkbookStylesPart);
        var formatter = new OpenXmlValueFormatter(workbookPart.Workbook.WorkbookProperties?.Date1904?.Value ?? false);
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var rowDefinitions = worksheetPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>()
            .Where(row => row.RowIndex is not null)
            .ToDictionary(row => (int)row.RowIndex!.Value) ?? new Dictionary<int, Row>();
        var columnDefinitions = worksheetPart.Worksheet.GetFirstChild<Columns>()?.Elements<Column>().ToArray() ?? Array.Empty<Column>();
        var rows = CreateRows(range, rowDefinitions, options.IncludeHiddenRowsAndColumns, worksheetPart.Worksheet.SheetFormatProperties?.DefaultRowHeight?.Value ?? 15D);
        var columns = CreateColumns(range, columnDefinitions, options.IncludeHiddenRowsAndColumns, worksheetPart.Worksheet.SheetFormatProperties?.DefaultColumnWidth?.Value ?? 8.43D);
        var sourceCells = worksheetPart.Worksheet.Descendants<Cell>()
            .Select(cell => new { Cell = cell, Address = TryGetAddress(cell) })
            .Where(item => item.Address.HasValue && Contains(range, item.Address.Value))
            .ToDictionary(item => item.Address!.Value, item => item.Cell);
        var cells = new List<CellModel>();
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                var address = new ExcelCellAddress(column.Address, row.Address);
                sourceCells.TryGetValue(address, out var sourceCell);
                cells.Add(ReadCell(sourceCell, address, styleReader, formatter, sharedStrings, warnings));
            }
        }

        var mergedRanges = (worksheetPart.Worksheet.GetFirstChild<MergeCells>()?.Elements<MergeCell>() ?? Enumerable.Empty<MergeCell>())
            .Select(merge => TryParseRange(merge.Reference?.Value))
            .Where(merge => merge != null && Intersects(range, merge!))
            .Select(merge => ClipToRange(range, merge!))
            .Cast<ExcelRangeReference>()
            .ToArray();
        var drawings = ReadDrawings(worksheetPart, warnings);
        return new WorkbookRangeModel(rows, columns, cells, mergedRanges, warnings, drawings);
    }

    private static void ValidateMaterializationSize(ExcelRangeReference range)
    {
        var rowCount = (long)range.End.Row - range.Start.Row + 1L;
        var columnCount = (long)range.End.Column - range.Start.Column + 1L;
        var cellCount = checked(rowCount * columnCount);
        if (cellCount > MaximumMaterializedCellCount)
        {
            throw new ArgumentException(
                $"The requested range requires materialization of {cellCount.ToString(CultureInfo.InvariantCulture)} cells, exceeding the safety limit of {MaximumMaterializedCellCount.ToString(CultureInfo.InvariantCulture)}.",
                nameof(range));
        }
    }

    private static IReadOnlyList<WorkbookRowModel> CreateRows(ExcelRangeReference range, IReadOnlyDictionary<int, Row> definitions, bool includeHidden, double defaultHeight)
    {
        var rows = new List<WorkbookRowModel>();
        for (var address = range.Start.Row; address <= range.End.Row; address++)
        {
            definitions.TryGetValue(address, out var definition);
            var isHidden = definition?.Hidden?.Value ?? false;
            if (includeHidden || !isHidden)
            {
                rows.Add(new WorkbookRowModel(address, definition?.Height?.Value ?? defaultHeight, isHidden));
            }
        }

        return rows;
    }

    private static IReadOnlyList<WorkbookColumnModel> CreateColumns(ExcelRangeReference range, IReadOnlyList<Column> definitions, bool includeHidden, double defaultWidth)
    {
        var columns = new List<WorkbookColumnModel>();
        for (var address = range.Start.Column; address <= range.End.Column; address++)
        {
            var definition = definitions.LastOrDefault(column =>
                column.Min is not null && column.Max is not null && column.Min.Value <= (uint)address && column.Max.Value >= (uint)address);
            var isHidden = definition?.Hidden?.Value ?? false;
            if (includeHidden || !isHidden)
            {
                columns.Add(new WorkbookColumnModel(address, definition?.Width?.Value ?? defaultWidth, isHidden));
            }
        }

        return columns;
    }

    private static CellModel ReadCell(
        Cell? cell,
        ExcelCellAddress address,
        OpenXmlStyleReader styleReader,
        OpenXmlValueFormatter formatter,
        SharedStringTable? sharedStrings,
        ICollection<WorkbookReadWarning> warnings)
    {
        if (cell == null)
        {
            return new CellModel(address, string.Empty, null, CellStyleModel.Default, false);
        }

        var style = styleReader.GetStyle(cell.StyleIndex?.Value);
        var formulaElement = cell.CellFormula;
        var formula = formulaElement == null ? null : formulaElement.Text ?? string.Empty;
        var hasCachedFormulaValue = formulaElement != null && cell.CellValue != null;
        if (formulaElement != null && !hasCachedFormulaValue)
        {
            warnings.Add(new WorkbookReadWarning("FORMULA_CACHE_MISSING", "Formula cell " + address.ToA1() + " has no cached value."));
            return new CellModel(address, string.Empty, formula, style, false);
        }

        return new CellModel(
            address,
            GetDisplayText(cell, style, formatter, sharedStrings),
            formula,
            style,
            hasCachedFormulaValue,
            GetRawNumericValue(cell));
    }

    private static string? GetRawNumericValue(Cell cell)
    {
        var type = cell.DataType?.Value;
        if (type != null && type != CellValues.Number) return null;
        var raw = cell.CellValue?.InnerText;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? raw : null;
    }

    private static string GetDisplayText(Cell cell, CellStyleModel style, OpenXmlValueFormatter formatter, SharedStringTable? sharedStrings)
    {
        var type = cell.DataType?.Value;
        if (type == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }

        var rawValue = cell.CellValue?.InnerText ?? string.Empty;
        if (type == CellValues.SharedString)
        {
            return int.TryParse(rawValue, out var index) && sharedStrings != null && index >= 0 && index < sharedStrings.Count()
                ? sharedStrings.ElementAt(index).InnerText
                : string.Empty;
        }

        if (type == CellValues.Boolean)
        {
            return rawValue == "1" ? "TRUE" : rawValue == "0" ? "FALSE" : rawValue;
        }

        if (type == CellValues.String || type == CellValues.Error)
        {
            return rawValue;
        }

        return formatter.FormatNumber(rawValue, style.NumberFormatCode);
    }

    private static WorkbookDrawingModel ReadDrawings(WorksheetPart worksheetPart, ICollection<WorkbookReadWarning> warnings)
    {
        var items = new List<WorkbookDrawingItem>();
        foreach (var drawingPart in worksheetPart.GetPartsOfType<DrawingsPart>())
        {
            foreach (var anchor in drawingPart.WorksheetDrawing.ChildElements.Where(IsDrawingAnchor))
            {
                var descendants = anchor.Descendants<OpenXmlElement>().Concat(new[] { anchor }).ToArray();
                var kind = GetDrawingKind(descendants);
                var relationshipId = descendants.Select(GetRelationshipId).FirstOrDefault(id => id != null);
                byte[]? imageBytes = null;
                WorkbookDrawingPreview? cachedPreview = null;
                if (kind == WorkbookDrawingKind.Image && relationshipId != null)
                {
                    imageBytes = TryReadImageBytes(drawingPart, relationshipId);
                }
                else if (kind != WorkbookDrawingKind.Image && relationshipId != null)
                {
                    var previewBytes = TryReadImageBytes(drawingPart, relationshipId);
                    if (previewBytes != null)
                    {
                        cachedPreview = new WorkbookDrawingPreview(relationshipId, previewBytes);
                    }
                }

                if (kind != WorkbookDrawingKind.Image && cachedPreview == null)
                {
                    warnings.Add(new WorkbookReadWarning("DRAWING_PREVIEW_MISSING", "Drawing " + kind + " at " + GetAnchor(anchor) + " has no cached preview."));
                }

                items.Add(new WorkbookDrawingItem(kind, GetAnchor(anchor), relationshipId, imageBytes, cachedPreview));
            }
        }

        return new WorkbookDrawingModel(items);
    }

    private static bool IsDrawingAnchor(OpenXmlElement element)
    {
        return string.Equals(element.LocalName, "oneCellAnchor", StringComparison.Ordinal) ||
            string.Equals(element.LocalName, "twoCellAnchor", StringComparison.Ordinal) ||
            string.Equals(element.LocalName, "absoluteAnchor", StringComparison.Ordinal);
    }

    private static WorkbookDrawingKind GetDrawingKind(IEnumerable<OpenXmlElement> elements)
    {
        var names = new HashSet<string>(elements.Select(element => element.LocalName), StringComparer.Ordinal);
        if (names.Contains("pic")) return WorkbookDrawingKind.Image;
        if (names.Contains("chart")) return WorkbookDrawingKind.Chart;
        if (names.Contains("sp")) return WorkbookDrawingKind.Shape;
        if (names.Contains("relIds") || names.Contains("dataModel")) return WorkbookDrawingKind.SmartArt;
        return WorkbookDrawingKind.Other;
    }

    private static string? GetRelationshipId(OpenXmlElement element)
    {
        var attributes = element.GetAttributes();
        return attributes.FirstOrDefault(attribute => string.Equals(attribute.LocalName, "embed", StringComparison.Ordinal)).Value
            ?? attributes.FirstOrDefault(attribute =>
                string.Equals(attribute.LocalName, "id", StringComparison.Ordinal) &&
                string.Equals(attribute.NamespaceUri, RelationshipNamespace, StringComparison.Ordinal)).Value;
    }

    private static byte[]? TryReadImageBytes(DrawingsPart drawingPart, string relationshipId)
    {
        try
        {
            if (!(drawingPart.GetPartById(relationshipId) is ImagePart imagePart))
            {
                return null;
            }

            using var input = imagePart.GetStream(FileMode.Open, FileAccess.Read);
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string GetAnchor(OpenXmlElement anchor)
    {
        if (string.Equals(anchor.LocalName, "twoCellAnchor", StringComparison.Ordinal))
        {
            return TryGetTwoCellAnchorRange(anchor, out var twoCellRange) ? twoCellRange.ToA1() : anchor.LocalName;
        }

        if (string.Equals(anchor.LocalName, "oneCellAnchor", StringComparison.Ordinal))
        {
            return TryGetOneCellAnchorRange(anchor, out var oneCellRange) ? oneCellRange.ToA1() : anchor.LocalName;
        }

        return anchor.LocalName;
    }

    private static bool TryGetTwoCellAnchorRange(OpenXmlElement anchor, out ExcelRangeReference range)
    {
        range = null!;
        var from = anchor.ChildElements.FirstOrDefault(element => string.Equals(element.LocalName, "from", StringComparison.Ordinal));
        var to = anchor.ChildElements.FirstOrDefault(element => string.Equals(element.LocalName, "to", StringComparison.Ordinal));
        if (from == null || to == null || !TryGetAnchorAddress(from, out var start) || !TryGetAnchorAddress(to, out var end))
        {
            return false;
        }

        range = new ExcelRangeReference(start, end);
        return true;
    }

    private static bool TryGetOneCellAnchorRange(OpenXmlElement anchor, out ExcelRangeReference range)
    {
        range = null!;
        var from = anchor.ChildElements.FirstOrDefault(element => string.Equals(element.LocalName, "from", StringComparison.Ordinal));
        if (from == null || !TryGetAnchorAddress(from, out var start))
        {
            return false;
        }

        range = new ExcelRangeReference(start, start);
        return true;
    }

    private static bool TryGetAnchorAddress(OpenXmlElement marker, out ExcelCellAddress address)
    {
        address = default;
        var column = marker.ChildElements.FirstOrDefault(element => string.Equals(element.LocalName, "col", StringComparison.Ordinal))?.InnerText;
        var row = marker.ChildElements.FirstOrDefault(element => string.Equals(element.LocalName, "row", StringComparison.Ordinal))?.InnerText;
        if (!int.TryParse(column, NumberStyles.None, CultureInfo.InvariantCulture, out var zeroBasedColumn) ||
            !int.TryParse(row, NumberStyles.None, CultureInfo.InvariantCulture, out var zeroBasedRow) ||
            zeroBasedColumn < 0 || zeroBasedRow < 0 ||
            zeroBasedColumn >= MaximumExcelColumn || zeroBasedRow >= MaximumExcelRow)
        {
            return false;
        }

        address = new ExcelCellAddress(zeroBasedColumn + 1, zeroBasedRow + 1);
        return true;
    }

    private static ExcelCellAddress? TryGetAddress(Cell cell)
    {
        var reference = cell.CellReference?.Value;
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        try
        {
            return ExcelCellAddress.Parse(reference!);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static ExcelRangeReference? TryParseRange(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        try
        {
            return ExcelRangeReference.Parse(reference!);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool Contains(ExcelRangeReference range, ExcelCellAddress address)
    {
        return address.Column >= range.Start.Column && address.Column <= range.End.Column && address.Row >= range.Start.Row && address.Row <= range.End.Row;
    }

    private static bool Intersects(ExcelRangeReference first, ExcelRangeReference second)
    {
        return first.Start.Column <= second.End.Column && first.End.Column >= second.Start.Column && first.Start.Row <= second.End.Row && first.End.Row >= second.Start.Row;
    }

    private static ExcelRangeReference ClipToRange(ExcelRangeReference requestedRange, ExcelRangeReference mergedRange)
    {
        var start = new ExcelCellAddress(
            Math.Max(requestedRange.Start.Column, mergedRange.Start.Column),
            Math.Max(requestedRange.Start.Row, mergedRange.Start.Row));
        var end = new ExcelCellAddress(
            Math.Min(requestedRange.End.Column, mergedRange.End.Column),
            Math.Min(requestedRange.End.Row, mergedRange.End.Row));
        return new ExcelRangeReference(start, end);
    }

    private static FileStream OpenReadStream(string path, FileShare share)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, share);
    }

    private static void ValidateWorkbookExtension(string path)
    {
        var extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only .xlsx and .xlsm workbook files are supported.");
        }
    }

    private static ExcelRangeReference? GetUsedRange(WorksheetPart worksheetPart)
    {
        var dimension = worksheetPart.Worksheet.SheetDimension?.Reference?.Value;
        if (!string.IsNullOrEmpty(dimension))
        {
            try
            {
                return ExcelRangeReference.Parse(dimension!);
            }
            catch (ArgumentException)
            {
            }
        }

        ExcelCellAddress? start = null;
        ExcelCellAddress? end = null;
        foreach (var cell in worksheetPart.Worksheet.Descendants<Cell>())
        {
            var cellReference = cell.CellReference?.Value;
            if (string.IsNullOrEmpty(cellReference))
            {
                continue;
            }

            try
            {
                var address = ExcelCellAddress.Parse(cellReference!);
                start = start.HasValue
                    ? new ExcelCellAddress(Math.Min(start.Value.Column, address.Column), Math.Min(start.Value.Row, address.Row))
                    : address;
                end = end.HasValue
                    ? new ExcelCellAddress(Math.Max(end.Value.Column, address.Column), Math.Max(end.Value.Row, address.Row))
                    : address;
            }
            catch (ArgumentException)
            {
            }
        }

        return start.HasValue && end.HasValue ? new ExcelRangeReference(start.Value, end.Value) : null;
    }

    private static IEnumerable<string> SplitRangeList(string text)
    {
        var rangeStart = 0;
        var inQuotedSheetName = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\'')
            {
                if (inQuotedSheetName && index + 1 < text.Length && text[index + 1] == '\'')
                {
                    index++;
                    continue;
                }

                inQuotedSheetName = !inQuotedSheetName;
            }
            else if (text[index] == ',' && !inQuotedSheetName)
            {
                yield return text.Substring(rangeStart, index - rangeStart);
                rangeStart = index + 1;
            }
        }

        yield return text.Substring(rangeStart);
    }

    private static WorksheetSource GetWorksheetSource(WorkbookPart workbookPart, Sheet sheet)
    {
        var relationshipId = sheet.Id == null ? null : sheet.Id.Value;
        if (string.IsNullOrEmpty(relationshipId))
        {
            throw new InvalidDataException("A worksheet is missing its relationship identifier.");
        }

        if (!(workbookPart.GetPartById(relationshipId!) is WorksheetPart worksheetPart))
        {
            throw new InvalidDataException("A worksheet relationship does not point to a worksheet part.");
        }

        var name = sheet.Name == null ? null : sheet.Name.Value;
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidDataException("A worksheet is missing its name.");
        }

        return new WorksheetSource(name!, worksheetPart);
    }

    private static bool TryParseQualifiedRange(string text, out string worksheetName, out ExcelRangeReference reference)
    {
        worksheetName = string.Empty;
        reference = null!;

        var separator = text.LastIndexOf('!');
        if (separator < 1 || separator == text.Length - 1)
        {
            return false;
        }

        var sheetText = text.Substring(0, separator);
        if (sheetText.Length >= 2 && sheetText[0] == '\'' && sheetText[sheetText.Length - 1] == '\'')
        {
            sheetText = sheetText.Substring(1, sheetText.Length - 2).Replace("''", "'");
        }

        try
        {
            reference = ExcelRangeReference.Parse(text.Substring(separator + 1));
        }
        catch (ArgumentException)
        {
            return false;
        }

        worksheetName = sheetText;
        return !string.IsNullOrEmpty(worksheetName);
    }

    private sealed record WorksheetSource(string Name, WorksheetPart Part);
}
