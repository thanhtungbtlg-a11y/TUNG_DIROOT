using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Layout;
using TungDiroot.Core.RevitPlanning;

namespace TungDiroot.Addin.Revit;

public sealed class ScheduleHeaderTableWriter
{
    private const double PointsToFeet = 1D / 864D;
    private readonly StyleMappingProfile _styleProfile;

    public ScheduleHeaderTableWriter(StyleMappingProfile? styleProfile = null)
    {
        _styleProfile = styleProfile ?? new StyleMappingProfile(new Dictionary<string, RevitStyleMapping>());
    }

    public void Write(ViewSchedule schedule, WorkbookRangeModel model, UpdateBehavior behavior, bool blackAndWhite = false)
    {
        if (schedule == null) throw new ArgumentNullException(nameof(schedule));
        if (model == null) throw new ArgumentNullException(nameof(model));
        var header = schedule.GetTableData().GetSectionData(SectionType.Header);
        if (behavior == UpdateBehavior.Recreate)
        {
            UnmergeAll(header);
            Resize(header, model.Rows.Count, model.Columns.Count);
            var firstRow = header.FirstRowNumber;
            var firstColumn = header.FirstColumnNumber;
            for (var row = 0; row < model.Rows.Count; row++) header.SetRowHeight(firstRow + row, model.Rows[row].HeightPoints * PointsToFeet);
            for (var column = 0; column < model.Columns.Count; column++)
                header.SetColumnWidth(firstColumn + column, Math.Max(1D, model.Columns[column].WidthCharacters * 7D) * PointsToFeet);
            ApplyStyles(schedule.Document, header, model, firstRow, firstColumn, blackAndWhite, _styleProfile);
            foreach (var merge in model.MergedRanges)
            {
                header.MergeCells(new TableMergedCell(
                    firstRow + merge.Start.Row - model.Rows[0].Address,
                    firstColumn + merge.Start.Column - model.Columns[0].Address,
                    firstRow + merge.End.Row - model.Rows[0].Address,
                    firstColumn + merge.End.Column - model.Columns[0].Address));
            }
        }

        var rowOffset = header.FirstRowNumber;
        var columnOffset = header.FirstColumnNumber;
        foreach (var cell in model.Cells)
        {
            var row = IndexOfRow(model.Rows, cell.Address.Row);
            var column = IndexOfColumn(model.Columns, cell.Address.Column);
            if (row >= 0 && column >= 0) header.SetCellText(rowOffset + row, columnOffset + column, cell.DisplayText);
        }
    }

    private static void ApplyStyles(Document document, TableSectionData header, WorkbookRangeModel model, int firstRow, int firstColumn, bool blackAndWhite, StyleMappingProfile styleProfile)
    {
        var resolver = new RevitStyleResolver(styleProfile);
        foreach (var cell in model.Cells)
        {
            var row = IndexOfRow(model.Rows, cell.Address.Row);
            var column = IndexOfColumn(model.Columns, cell.Address.Column);
            if (row < 0 || column < 0 || !header.AllowOverrideCellStyle(firstRow + row, firstColumn + column)) continue;
            using var style = header.GetTableCellStyle(firstRow + row, firstColumn + column);
            using var overrides = style.GetCellStyleOverrideOptions();
            var sourceStyle = blackAndWhite ? BlackAndWhiteTransform.Apply(cell.Style, preserveGrayscale: true) : cell.Style;
            var mappedTextId = styleProfile.Resolve(cell.Style)?.TextNoteTypeUniqueId;
            if (mappedTextId != null && document.GetElement(mappedTextId) is TextNoteType mappedText)
                sourceStyle = sourceStyle with { Font = MappedFont(mappedText, sourceStyle.Font) };
            style.FontName = string.IsNullOrWhiteSpace(sourceStyle.Font.Name) ? "Arial" : sourceStyle.Font.Name;
            style.TextSize = RevitStyleValueConverter.ScheduleTextSize(sourceStyle.Font.SizePoints);
            style.IsFontBold = sourceStyle.Font.Bold;
            style.IsFontItalic = sourceStyle.Font.Italic;
            style.IsFontUnderline = sourceStyle.Font.Underline;
            style.TextColor = RevitStyleResolver.ResolveColor(sourceStyle.Font.Color, new Color(0, 0, 0));
            style.BackgroundColor = string.Equals(sourceStyle.Fill.Pattern, "none", StringComparison.OrdinalIgnoreCase)
                ? new Color(255, 255, 255)
                : RevitStyleResolver.ResolveColor(sourceStyle.Fill.ForegroundColor, new Color(255, 255, 255));
            style.FontHorizontalAlignment = Horizontal(sourceStyle.Horizontal);
            style.FontVerticalAlignment = Vertical(sourceStyle.Vertical);
            style.TextOrientation = RevitStyleValueConverter.ScheduleTextOrientation(sourceStyle.RotationDegrees);
            style.BorderLeftLineStyle = BorderStyleId(document, resolver, sourceStyle.Border.Left, cell.Style.Border.Left);
            style.BorderRightLineStyle = BorderStyleId(document, resolver, sourceStyle.Border.Right, cell.Style.Border.Right);
            style.BorderTopLineStyle = BorderStyleId(document, resolver, sourceStyle.Border.Top, cell.Style.Border.Top);
            style.BorderBottomLineStyle = BorderStyleId(document, resolver, sourceStyle.Border.Bottom, cell.Style.Border.Bottom);
            overrides.Font = overrides.FontSize = overrides.FontColor = true;
            overrides.Bold = overrides.Italics = overrides.Underline = true;
            overrides.BackgroundColor = overrides.HorizontalAlignment = overrides.VerticalAlignment = true;
            overrides.TextOrientation = true;
            overrides.BorderLeftLineStyle = overrides.BorderRightLineStyle = true;
            overrides.BorderTopLineStyle = overrides.BorderBottomLineStyle = true;
            style.SetCellStyleOverrideOptions(overrides);
            header.SetCellStyle(firstRow + row, firstColumn + column, style);
        }
    }

    private static ElementId BorderStyleId(Document document, RevitStyleResolver resolver, BorderSideModel border, BorderSideModel original)
    {
        if (string.IsNullOrWhiteSpace(border.Style) || string.Equals(border.Style, "none", StringComparison.OrdinalIgnoreCase))
            return ElementId.InvalidElementId;
        return resolver.ResolveLineStyle(document, border.Style, border.Color, LineStyleKey.Create(original.Style, original.Color))?.Id ?? ElementId.InvalidElementId;
    }

    private static FontModel MappedFont(TextNoteType type, FontModel fallback)
    {
        var size = type.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble();
        var color = type.get_Parameter(BuiltInParameter.LINE_COLOR)?.AsInteger();
        return new FontModel(
            type.get_Parameter(BuiltInParameter.TEXT_FONT)?.AsString() ?? fallback.Name,
            size.HasValue && size.Value > 0 ? size.Value / PointsToFeet : fallback.SizePoints,
            Flag(type, BuiltInParameter.TEXT_STYLE_BOLD, fallback.Bold),
            Flag(type, BuiltInParameter.TEXT_STYLE_ITALIC, fallback.Italic),
            Flag(type, BuiltInParameter.TEXT_STYLE_UNDERLINE, fallback.Underline),
            color.HasValue ? RevitStyleValueConverter.TextColorArgb(color.Value) : fallback.Color);
    }

    private static bool Flag(TextNoteType type, BuiltInParameter parameter, bool fallback) =>
        type.get_Parameter(parameter) is Parameter value ? value.AsInteger() != 0 : fallback;

    private static HorizontalAlignmentStyle Horizontal(HorizontalAlignment value) => value switch
    {
        HorizontalAlignment.Center or HorizontalAlignment.CenterContinuous => HorizontalAlignmentStyle.Center,
        HorizontalAlignment.Right => HorizontalAlignmentStyle.Right,
        _ => HorizontalAlignmentStyle.Left
    };

    private static VerticalAlignmentStyle Vertical(VerticalAlignment value) => value switch
    {
        VerticalAlignment.Center => VerticalAlignmentStyle.Middle,
        VerticalAlignment.Top => VerticalAlignmentStyle.Top,
        _ => VerticalAlignmentStyle.Bottom
    };

    private static void Resize(TableSectionData header, int rows, int columns)
    {
        if (rows < 1 || columns < 1) throw new ArgumentOutOfRangeException(nameof(rows), "Schedule headers require at least one row and column.");
        while (header.NumberOfRows < rows) header.InsertRow(header.LastRowNumber + 1);
        while (header.NumberOfRows > rows) header.RemoveRow(header.LastRowNumber);
        while (header.NumberOfColumns < columns) header.InsertColumn(header.LastColumnNumber + 1);
        while (header.NumberOfColumns > columns) header.RemoveColumn(header.LastColumnNumber);
    }

    private static void UnmergeAll(TableSectionData header)
    {
        for (var row = header.FirstRowNumber; row <= header.LastRowNumber; row++)
        for (var column = header.FirstColumnNumber; column <= header.LastColumnNumber; column++)
        {
            var merged = header.GetMergedCell(row, column);
            if (merged.Top != merged.Bottom || merged.Left != merged.Right)
                header.SetMergedCell(row, column, new TableMergedCell(row, column, row, column));
        }
    }

    private static int IndexOfRow(System.Collections.Generic.IReadOnlyList<WorkbookRowModel> values, int address)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index].Address == address) return index;
        }
        return -1;
    }

    private static int IndexOfColumn(System.Collections.Generic.IReadOnlyList<WorkbookColumnModel> values, int address)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index].Address == address) return index;
        }
        return -1;
    }
}
