using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Layout;

public static class FillRectangleCoalescer
{
    public static IReadOnlyList<FillPlacement> Coalesce(IEnumerable<FillPlacement> fills)
    {
        if (fills == null) throw new ArgumentNullException(nameof(fills));
        var result = new List<FillPlacement>();
        foreach (var group in fills.GroupBy(fill => (fill.Bounds.Y, fill.Bounds.Height, fill.Color)))
        {
            FillPlacement? current = null;
            foreach (var fill in group.OrderBy(item => item.Bounds.X))
            {
                if (current != null && Math.Abs(current.Bounds.X + current.Bounds.Width - fill.Bounds.X) < 0.000001D)
                {
                    current = current with { Bounds = current.Bounds with { Width = current.Bounds.Width + fill.Bounds.Width } };
                }
                else
                {
                    if (current != null) result.Add(current);
                    current = fill;
                }
            }
            if (current != null) result.Add(current);
        }
        return new ReadOnlyList<FillPlacement>(result);
    }
}
