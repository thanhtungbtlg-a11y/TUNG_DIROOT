using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Layout;

public sealed record RevitStyleMapping(string? TextNoteTypeUniqueId, string? GraphicsStyleUniqueId);

public sealed record TableGenStyleMapping(
    string CellStyleKey,
    string? TextNoteTypeUniqueId,
    string? GraphicsStyleUniqueId);

public sealed class StyleMappingProfile
{
    private readonly IReadOnlyDictionary<string, RevitStyleMapping> _mappings;

    public StyleMappingProfile(IDictionary<string, RevitStyleMapping> mappings)
    {
        if (mappings == null) throw new ArgumentNullException(nameof(mappings));
        _mappings = new ReadOnlyDictionary<string, RevitStyleMapping>(
            new Dictionary<string, RevitStyleMapping>(mappings, StringComparer.Ordinal));
    }

    public RevitStyleMapping? Resolve(CellStyleModel style)
    {
        _mappings.TryGetValue(CellStyleKey.Create(style), out var mapping);
        return mapping;
    }

    public RevitStyleMapping? ResolveLine(string lineStyle, string? color)
    {
        _mappings.TryGetValue(LineStyleKey.Create(lineStyle, color), out var mapping);
        return mapping;
    }

    public RevitStyleMapping? ResolveText(TextPlacement text) =>
        ResolveKey(text.MappingKey ?? CellStyleKey.Create(text.Style));

    public RevitStyleMapping? ResolveLine(LinePlacement line) =>
        ResolveKey(line.MappingKey ?? LineStyleKey.Create(line.Style, line.Color));

    public RevitStyleMapping? ResolveKey(string key)
    {
        _mappings.TryGetValue(key, out var mapping);
        return mapping;
    }

    public static StyleMappingProfile FromEntries(IEnumerable<TableGenStyleMapping> entries)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        var mappings = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.CellStyleKey))
            .GroupBy(entry => entry.CellStyleKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new RevitStyleMapping(
                    group.Last().TextNoteTypeUniqueId,
                    group.Last().GraphicsStyleUniqueId),
                StringComparer.Ordinal);
        return new StyleMappingProfile(mappings);
    }
}

public static class LineStyleKey
{
    public static string Create(string? lineStyle, string? color) => string.Join("|", new[]
    {
        "LINE",
        (lineStyle ?? "none").Trim().ToLowerInvariant(),
        (color ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant()
    });
}

public static class CellStyleKey
{
    public static string Create(CellStyleModel style)
    {
        if (style == null) throw new ArgumentNullException(nameof(style));
        return string.Join("|", new[]
        {
            style.Font.Name,
            style.Font.SizePoints.ToString("R", CultureInfo.InvariantCulture),
            style.Font.Bold ? "1" : "0",
            style.Font.Italic ? "1" : "0",
            style.Font.Underline ? "1" : "0",
            style.Font.Color ?? string.Empty,
            style.Fill.Pattern,
            style.Fill.ForegroundColor ?? string.Empty,
            style.Border.Left.Style,
            style.Border.Right.Style,
            style.Border.Top.Style,
            style.Border.Bottom.Style,
            style.Horizontal.ToString(),
            style.Vertical.ToString(),
            style.RotationDegrees.ToString("R", CultureInfo.InvariantCulture),
            style.NumberFormatCode
        });
    }
}
