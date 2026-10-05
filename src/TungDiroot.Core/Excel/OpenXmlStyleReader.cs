using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TungDiroot.Core.Excel;

internal sealed class OpenXmlStyleReader
{
    private static readonly IReadOnlyDictionary<uint, string> BuiltInNumberFormats = new Dictionary<uint, string>
    {
        { 0U, "General" },
        { 1U, "0" },
        { 2U, "0.00" },
        { 9U, "0%" },
        { 10U, "0.00%" },
        { 11U, "0.00E+00" },
        { 12U, "# ?/?" },
        { 13U, "# ??/??" },
        { 14U, "m/d/yyyy" },
        { 15U, "d-mmm-yy" },
        { 16U, "d-mmm" },
        { 17U, "mmm-yy" },
        { 18U, "h:mm AM/PM" },
        { 19U, "h:mm:ss AM/PM" },
        { 20U, "h:mm" },
        { 21U, "h:mm:ss" },
        { 22U, "m/d/yyyy h:mm" }
    };

    private readonly IReadOnlyList<CellStyleModel> _styles;

    public OpenXmlStyleReader(WorkbookStylesPart? stylesPart)
    {
        _styles = ReadStyles(stylesPart);
    }

    public CellStyleModel GetStyle(uint? styleIndex)
    {
        if (!styleIndex.HasValue || styleIndex.Value >= _styles.Count)
        {
            return CellStyleModel.Default;
        }

        return _styles[(int)styleIndex.Value];
    }

    private static IReadOnlyList<CellStyleModel> ReadStyles(WorkbookStylesPart? stylesPart)
    {
        var stylesheet = stylesPart?.Stylesheet;
        var formats = stylesheet?.CellFormats?.Elements<CellFormat>().ToArray();
        if (formats == null || formats.Length == 0)
        {
            return new[] { CellStyleModel.Default };
        }

        var fonts = stylesheet!.Fonts?.Elements<Font>().ToArray() ?? Array.Empty<Font>();
        var fills = stylesheet.Fills?.Elements<Fill>().ToArray() ?? Array.Empty<Fill>();
        var borders = stylesheet.Borders?.Elements<Border>().ToArray() ?? Array.Empty<Border>();
        var numberFormats = (stylesheet.NumberingFormats?.Elements<NumberingFormat>() ?? Enumerable.Empty<NumberingFormat>())
            .Where(format => format.NumberFormatId is not null && format.FormatCode is not null)
            .ToDictionary(format => GetValue(format.NumberFormatId)!.Value, format => format.FormatCode!.Value!, EqualityComparer<uint>.Default);

        return formats.Select(format => new CellStyleModel(
                GetFont(fonts, GetValue(format.FontId)),
                GetFill(fills, GetValue(format.FillId)),
                GetBorder(borders, GetValue(format.BorderId)),
                GetHorizontal(format.Alignment),
                GetVertical(format.Alignment),
                GetRotation(format.Alignment),
                GetNumberFormat(GetValue(format.NumberFormatId), numberFormats)))
            .ToArray();
    }

    private static uint? GetValue(UInt32Value? value)
    {
        return value is null ? null : value.Value;
    }

    private static FontModel GetFont(IReadOnlyList<Font> fonts, uint? id)
    {
        if (!id.HasValue || id.Value >= fonts.Count)
        {
            return CellStyleModel.Default.Font;
        }

        var font = fonts[(int)id.Value];
        return new FontModel(
            font.FontName?.Val?.Value ?? "Calibri",
            font.FontSize?.Val?.Value ?? 11D,
            GetBooleanPropertyValue(font.Bold),
            GetBooleanPropertyValue(font.Italic),
            GetUnderlineValue(font.Underline),
            GetColor(font.Color));
    }

    private static bool GetBooleanPropertyValue(BooleanPropertyType? property)
    {
        return property is not null && (property.Val is null || property.Val.Value);
    }

    private static bool GetUnderlineValue(Underline? underline)
    {
        return underline is not null && (underline.Val is null || underline.Val.Value != UnderlineValues.None);
    }

    private static FillModel GetFill(IReadOnlyList<Fill> fills, uint? id)
    {
        if (!id.HasValue || id.Value >= fills.Count)
        {
            return CellStyleModel.Default.Fill;
        }

        var fill = fills[(int)id.Value].PatternFill;
        if (fill == null)
        {
            return CellStyleModel.Default.Fill;
        }

        return new FillModel(
            fill.PatternType?.Value.ToString() ?? "none",
            GetColor(fill.ForegroundColor),
            GetColor(fill.BackgroundColor));
    }

    private static BorderModel GetBorder(IReadOnlyList<Border> borders, uint? id)
    {
        if (!id.HasValue || id.Value >= borders.Count)
        {
            return CellStyleModel.Default.Border;
        }

        var border = borders[(int)id.Value];
        return new BorderModel(GetBorderSide(border.LeftBorder), GetBorderSide(border.RightBorder), GetBorderSide(border.TopBorder), GetBorderSide(border.BottomBorder));
    }

    private static BorderSideModel GetBorderSide(BorderPropertiesType? side)
    {
        return side == null
            ? new BorderSideModel("none", null)
            : new BorderSideModel(side.Style?.Value.ToString() ?? "none", GetColor(side.Color));
    }

    private static HorizontalAlignment GetHorizontal(Alignment? alignment)
    {
        switch (alignment?.Horizontal?.Value.ToString())
        {
            case "left": return HorizontalAlignment.Left;
            case "center": return HorizontalAlignment.Center;
            case "right": return HorizontalAlignment.Right;
            case "fill": return HorizontalAlignment.Fill;
            case "justify": return HorizontalAlignment.Justify;
            case "centerContinuous": return HorizontalAlignment.CenterContinuous;
            case "distributed": return HorizontalAlignment.Distributed;
            default: return HorizontalAlignment.General;
        }
    }

    private static VerticalAlignment GetVertical(Alignment? alignment)
    {
        switch (alignment?.Vertical?.Value.ToString())
        {
            case "top": return VerticalAlignment.Top;
            case "center": return VerticalAlignment.Center;
            case "justify": return VerticalAlignment.Justify;
            case "distributed": return VerticalAlignment.Distributed;
            default: return VerticalAlignment.Bottom;
        }
    }

    private static double GetRotation(Alignment? alignment)
    {
        return alignment?.TextRotation?.Value ?? 0D;
    }

    private static string GetNumberFormat(uint? id, IReadOnlyDictionary<uint, string> customFormats)
    {
        if (!id.HasValue)
        {
            return "General";
        }

        if (customFormats.TryGetValue(id.Value, out var customFormat))
        {
            return customFormat;
        }

        return BuiltInNumberFormats.TryGetValue(id.Value, out var builtInFormat) ? builtInFormat : "General";
    }

    private static string? GetColor(OpenXmlElement? color)
    {
        if (color == null)
        {
            return null;
        }

        var attributes = color.GetAttributes();
        var rgb = attributes.FirstOrDefault(attribute => attribute.LocalName == "rgb").Value;
        if (rgb != null)
        {
            return rgb;
        }

        var indexed = attributes.FirstOrDefault(attribute => attribute.LocalName == "indexed").Value;
        if (indexed != null)
        {
            return "indexed:" + indexed;
        }

        var theme = attributes.FirstOrDefault(attribute => attribute.LocalName == "theme").Value;
        if (theme != null)
        {
            return "theme:" + theme;
        }

        return null;
    }
}
