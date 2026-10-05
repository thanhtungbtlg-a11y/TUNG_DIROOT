using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Layout;

public static class BorderSegmentCoalescer
{
    public static IReadOnlyList<LinePlacement> Coalesce(IEnumerable<LinePlacement> segments)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));
        var horizontal = segments.Where(IsHorizontal)
            .GroupBy(segment => (segment.Y1, segment.Style, segment.Color, segment.Role, segment.MappingKey))
            .SelectMany(group => MergeHorizontal(group.OrderBy(segment => Math.Min(segment.X1, segment.X2))))
            .ToList();
        var vertical = segments.Where(segment => !IsHorizontal(segment))
            .GroupBy(segment => (segment.X1, segment.Style, segment.Color, segment.Role, segment.MappingKey))
            .SelectMany(group => MergeVertical(group.OrderBy(segment => Math.Min(segment.Y1, segment.Y2))))
            .ToList();
        return new ReadOnlyList<LinePlacement>(horizontal.Concat(vertical));
    }

    private static bool IsHorizontal(LinePlacement segment) => Math.Abs(segment.Y1 - segment.Y2) < 0.000001D;

    private static IEnumerable<LinePlacement> MergeHorizontal(IEnumerable<LinePlacement> source)
    {
        LinePlacement? current = null;
        foreach (var segment in source)
        {
            var normalized = segment with { X1 = Math.Min(segment.X1, segment.X2), X2 = Math.Max(segment.X1, segment.X2) };
            if (current != null && normalized.X1 <= current.X2 + 0.000001D)
            {
                current = current with { X2 = Math.Max(current.X2, normalized.X2) };
            }
            else
            {
                if (current != null) yield return current;
                current = normalized;
            }
        }
        if (current != null) yield return current;
    }

    private static IEnumerable<LinePlacement> MergeVertical(IEnumerable<LinePlacement> source)
    {
        LinePlacement? current = null;
        foreach (var segment in source)
        {
            var normalized = segment with { Y1 = Math.Min(segment.Y1, segment.Y2), Y2 = Math.Max(segment.Y1, segment.Y2) };
            if (current != null && normalized.Y1 <= current.Y2 + 0.000001D)
            {
                current = current with { Y2 = Math.Max(current.Y2, normalized.Y2) };
            }
            else
            {
                if (current != null) yield return current;
                current = normalized;
            }
        }
        if (current != null) yield return current;
    }
}
