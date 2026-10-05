using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TungDiroot.Core.Documents;

public sealed record DocumentPageSize(int PageNumber, double Width, double Height);
public sealed record DocumentPagePlacement(int PageNumber, double TopLeftX, double TopLeftY, double Width, double Height);

public static class DocumentPagePlacementPlanner
{
    public static IReadOnlyList<DocumentPagePlacement> Plan(
        IReadOnlyList<DocumentPageSize> pages,
        int columns,
        double gap)
    {
        if (pages == null) throw new ArgumentNullException(nameof(pages));
        if (columns < 1 || columns > 8) throw new ArgumentOutOfRangeException(nameof(columns));
        if (gap < 0 || double.IsNaN(gap) || double.IsInfinity(gap)) throw new ArgumentOutOfRangeException(nameof(gap));
        if (pages.Any(page => page.PageNumber <= 0 || page.Width <= 0 || page.Height <= 0 ||
                              double.IsNaN(page.Width) || double.IsNaN(page.Height) ||
                              double.IsInfinity(page.Width) || double.IsInfinity(page.Height)))
            throw new ArgumentException("PAGE_SIZE_INVALID", nameof(pages));

        var result = new List<DocumentPagePlacement>(pages.Count);
        var top = 0d;
        for (var rowStart = 0; rowStart < pages.Count; rowStart += columns)
        {
            var row = pages.Skip(rowStart).Take(columns).ToArray();
            var left = 0d;
            foreach (var page in row)
            {
                result.Add(new DocumentPagePlacement(page.PageNumber, left, top, page.Width, page.Height));
                left += page.Width + gap;
            }
            top -= row.Max(page => page.Height) + gap;
        }
        return new ReadOnlyCollection<DocumentPagePlacement>(result);
    }
}
