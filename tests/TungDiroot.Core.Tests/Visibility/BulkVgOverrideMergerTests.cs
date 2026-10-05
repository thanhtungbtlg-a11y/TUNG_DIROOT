using TungDiroot.Core.Visibility;
using Xunit;

namespace TungDiroot.Core.Tests.Visibility;

public sealed class BulkVgOverrideMergerTests
{
    [Fact]
    public void Merge_uses_target_baseline_and_preserves_every_unchecked_graphics_group()
    {
        var source = new Graphics { Projection = "source-p", Cut = "source-c", Fill = "source-f", Transparency = 75, Halftone = true };
        var target = new Graphics { Projection = "target-p", Cut = "target-c", Fill = "target-f", Transparency = 25, Halftone = false };

        var result = Merge(source, target, new BulkVgApplyOptions(false, true, false, false, false));

        Assert.Equal("source-p", result.Projection);
        Assert.Equal("target-c", result.Cut);
        Assert.Equal("target-f", result.Fill);
        Assert.Equal(25, result.Transparency);
        Assert.False(result.Halftone);
        Assert.Equal("target-p", target.Projection);
        Assert.NotSame(target, result);
    }

    [Fact]
    public void Merge_with_only_visibility_selected_leaves_all_graphic_overrides_unchanged()
    {
        var source = new Graphics { Projection = "source-p", Cut = "source-c", Fill = "source-f", Transparency = 75, Halftone = true };
        var target = new Graphics { Projection = "target-p", Cut = "target-c", Fill = "target-f", Transparency = 25, Halftone = false };

        var result = Merge(source, target, new BulkVgApplyOptions(true, false, false, false, false));

        Assert.Equal("target-p", result.Projection);
        Assert.Equal("target-c", result.Cut);
        Assert.Equal("target-f", result.Fill);
        Assert.Equal(25, result.Transparency);
        Assert.False(result.Halftone);
    }

    [Fact]
    public void Merge_copies_checked_cut_fill_and_transparency_groups_without_changing_projection()
    {
        var source = new Graphics { Projection = "source-p", Cut = "source-c", Fill = "source-f", Transparency = 75, Halftone = true };
        var target = new Graphics { Projection = "target-p", Cut = "target-c", Fill = "target-f", Transparency = 25, Halftone = false };

        var result = Merge(source, target, new BulkVgApplyOptions(false, false, true, true, true));

        Assert.Equal("target-p", result.Projection);
        Assert.Equal("source-c", result.Cut);
        Assert.Equal("source-f", result.Fill);
        Assert.Equal(75, result.Transparency);
        Assert.True(result.Halftone);
    }

    private static Graphics Merge(Graphics source, Graphics target, BulkVgApplyOptions options) =>
        BulkVgOverrideMerger.Merge(source, target, options,
            value => new Graphics { Projection = value.Projection, Cut = value.Cut, Fill = value.Fill, Transparency = value.Transparency, Halftone = value.Halftone },
            (result, value) => result.Projection = value.Projection,
            (result, value) => result.Cut = value.Cut,
            (result, value) => result.Fill = value.Fill,
            (result, value) => { result.Transparency = value.Transparency; result.Halftone = value.Halftone; });

    private sealed class Graphics
    {
        public string Projection { get; set; } = "";
        public string Cut { get; set; } = "";
        public string Fill { get; set; } = "";
        public int Transparency { get; set; }
        public bool Halftone { get; set; }
    }
}
