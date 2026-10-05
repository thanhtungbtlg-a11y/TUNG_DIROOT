using TungDiroot.Core.Layout;
using Xunit;

namespace TungDiroot.Core.Tests.Layout;

public sealed class CoalescerTests
{
    [Fact]
    public void Adjacent_equal_horizontal_borders_are_coalesced()
    {
        var result = BorderSegmentCoalescer.Coalesce(new[]
        {
            new LinePlacement(0, 0, 1, 0, "thin", "black", BorderRole.Inner),
            new LinePlacement(1, 0, 2, 0, "thin", "black", BorderRole.Inner),
            new LinePlacement(2, 0, 3, 0, "thin", "black", BorderRole.Inner)
        });

        var line = Assert.Single(result);
        Assert.Equal(3D, line.X2);
    }

    [Fact]
    public void Adjacent_equal_fills_are_coalesced()
    {
        var result = FillRectangleCoalescer.Coalesce(new[]
        {
            new FillPlacement(new LayoutRectangle(0, 0, 1, 1), "red"),
            new FillPlacement(new LayoutRectangle(1, 0, 2, 1), "red")
        });

        Assert.Equal(3D, Assert.Single(result).Bounds.Width);
    }
}
