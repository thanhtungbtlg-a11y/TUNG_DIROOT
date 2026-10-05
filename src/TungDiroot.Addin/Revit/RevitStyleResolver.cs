using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;
using TungDiroot.Core.RevitPlanning;

namespace TungDiroot.Addin.Revit;

public sealed class RevitStyleResolver
{
    private const double PointsToFeet = 1D / 864D;
    private readonly StyleMappingProfile _profile;

    public RevitStyleResolver(StyleMappingProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public ElementId ResolveTextNoteType(Document document, CellStyleModel style, string? mappingKey = null)
    {
        var uniqueId = _profile.ResolveKey(mappingKey ?? CellStyleKey.Create(style))?.TextNoteTypeUniqueId;
        var mapped = uniqueId == null ? null : document.GetElement(uniqueId) as TextNoteType;
        if (mapped != null) return mapped.Id;

        var name = "TUNG_DIROOT_Text_" + ShortHash("RGB-v2|" + CellStyleKey.Create(style));
        var existing = new FilteredElementCollector(document).OfClass(typeof(TextNoteType)).Cast<TextNoteType>()
            .FirstOrDefault(type => string.Equals(type.Name, name, StringComparison.Ordinal));
        if (existing != null) return existing.Id;
        var defaultType = document.GetElement(document.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType)) as TextNoteType
            ?? throw new InvalidOperationException("DEFAULT_TEXT_TYPE_MISSING");
        var created = (TextNoteType)defaultType.Duplicate(name);
        Set(created, BuiltInParameter.TEXT_FONT, string.IsNullOrWhiteSpace(style.Font.Name) ? "Arial" : style.Font.Name);
        Set(created, BuiltInParameter.TEXT_SIZE, Math.Max(1D, style.Font.SizePoints) * PointsToFeet);
        Set(created, BuiltInParameter.TEXT_STYLE_BOLD, style.Font.Bold ? 1 : 0);
        Set(created, BuiltInParameter.TEXT_STYLE_ITALIC, style.Font.Italic ? 1 : 0);
        Set(created, BuiltInParameter.TEXT_STYLE_UNDERLINE, style.Font.Underline ? 1 : 0);
        if (TryColor(style.Font.Color, out var color)) Set(created, BuiltInParameter.LINE_COLOR, ColorInteger(color));
        return created.Id;
    }

    public GraphicsStyle? ResolveLineStyle(Document document, string lineStyle, string? colorText, string? mappingKey = null)
    {
        var uniqueId = _profile.ResolveKey(mappingKey ?? LineStyleKey.Create(lineStyle, colorText))?.GraphicsStyleUniqueId;
        var mapped = uniqueId == null ? null : document.GetElement(uniqueId) as GraphicsStyle;
        if (mapped != null) return mapped;

        var parent = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        if (parent == null) return null;
        var name = "TUNG_DIROOT_Line_" + ShortHash((lineStyle ?? "none") + "|" + (colorText ?? string.Empty));
        var category = parent.SubCategories.Cast<Category>().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))
            ?? document.Settings.Categories.NewSubcategory(parent, name);
        if (TryColor(colorText, out var color)) category.LineColor = color;
        category.SetLineWeight(LineWeight(lineStyle), GraphicsStyleType.Projection);
        return category.GetGraphicsStyle(GraphicsStyleType.Projection);
    }

    public ElementId ResolveFilledRegionType(Document document, string colorText)
    {
        var name = "TUNG_DIROOT_Fill_" + ShortHash(colorText ?? string.Empty);
        var existing = new FilteredElementCollector(document).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>()
            .FirstOrDefault(type => string.Equals(type.Name, name, StringComparison.Ordinal));
        if (existing != null) return existing.Id;
        var source = new FilteredElementCollector(document).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().FirstOrDefault();
        if (source == null) return ElementId.InvalidElementId;
        var created = (FilledRegionType)source.Duplicate(name);
        var solid = new FilteredElementCollector(document).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
            .FirstOrDefault(pattern => pattern.GetFillPattern().IsSolidFill);
        if (solid != null) created.ForegroundPatternId = solid.Id;
        if (TryColor(colorText, out var color)) created.ForegroundPatternColor = color;
        return created.Id;
    }

    public static Color ResolveColor(string? value, Color fallback) => TryColor(value, out var color) ? color : fallback;

    private static void Set(Element element, BuiltInParameter parameter, string value)
    {
        var target = element.get_Parameter(parameter);
        if (target != null && !target.IsReadOnly) target.Set(value);
    }

    private static void Set(Element element, BuiltInParameter parameter, double value)
    {
        var target = element.get_Parameter(parameter);
        if (target != null && !target.IsReadOnly) target.Set(value);
    }

    private static void Set(Element element, BuiltInParameter parameter, int value)
    {
        var target = element.get_Parameter(parameter);
        if (target != null && !target.IsReadOnly) target.Set(value);
    }

    private static int LineWeight(string? style) => style switch
    {
        "medium" => 2,
        "thick" => 3,
        "double" => 3,
        _ => 1
    };

    private static bool TryColor(string? value, out Color color)
    {
        color = new Color(0, 0, 0);
        var hex = (value ?? string.Empty).Trim().TrimStart('#');
        if (hex.Length == 8) hex = hex.Substring(2);
        if (hex.Length != 6 || !byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) ||
            !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue)) return false;
        color = new Color(red, green, blue);
        return true;
    }

    private static int ColorInteger(Color color) => RevitStyleValueConverter.TextColorParameter(color.Red, color.Green, color.Blue);

    private static string ShortHash(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).Substring(0, 12);
    }
}
