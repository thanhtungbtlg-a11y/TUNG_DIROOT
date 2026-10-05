using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Layout;

public enum TableGenStyleCandidateKind
{
    Text,
    Line
}

public sealed record TableGenStyleCandidate(
    TableGenStyleCandidateKind Kind,
    string Key,
    string Description);

public static class StyleMappingCandidateExtractor
{
    public static IReadOnlyList<TableGenStyleCandidate> Extract(WorkbookRangeModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        var candidates = new Dictionary<string, TableGenStyleCandidate>(StringComparer.Ordinal);
        foreach (var cell in model.Cells)
        {
            var style = cell.Style;
            var cellKey = CellStyleKey.Create(style);
            candidates[cellKey] = new TableGenStyleCandidate(
                TableGenStyleCandidateKind.Text,
                cellKey,
                DescribeText(style));
            foreach (var side in Sides(style.Border))
            {
                if (string.IsNullOrWhiteSpace(side.Style) || string.Equals(side.Style, "none", StringComparison.OrdinalIgnoreCase))
                    continue;
                var lineKey = LineStyleKey.Create(side.Style, side.Color);
                candidates[lineKey] = new TableGenStyleCandidate(
                    TableGenStyleCandidateKind.Line,
                    lineKey,
                    $"{side.Style} · {NormalizeColor(side.Color)}");
            }
        }
        return new ReadOnlyCollection<TableGenStyleCandidate>(candidates.Values
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Description, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray());
    }

    private static IEnumerable<BorderSideModel> Sides(BorderModel border)
    {
        yield return border.Left;
        yield return border.Right;
        yield return border.Top;
        yield return border.Bottom;
    }

    private static string DescribeText(CellStyleModel style)
    {
        var flags = new List<string>();
        if (style.Font.Bold) flags.Add("Bold");
        if (style.Font.Italic) flags.Add("Italic");
        if (style.Font.Underline) flags.Add("Underline");
        var suffix = flags.Count == 0 ? string.Empty : " · " + string.Join(", ", flags);
        return $"{style.Font.Name} {style.Font.SizePoints.ToString("0.##", CultureInfo.InvariantCulture)} pt{suffix}";
    }

    private static string NormalizeColor(string? color) => string.IsNullOrWhiteSpace(color)
        ? "mặc định"
        : "#" + color!.Trim().TrimStart('#').ToUpperInvariant();
}
