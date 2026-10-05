using TungDiroot.Core.Visibility;
using Xunit;

namespace TungDiroot.Core.Tests.Visibility;

public sealed class BulkVgDifferenceDetectorTests
{
    [Fact]
    public void Compare_reports_only_selected_property_groups()
    {
        var source = new BulkVgComparableState(false, "projection-a", "cut-a", "fill-a", 30, true);
        var target = new BulkVgComparableState(true, "projection-b", "cut-b", "fill-b", 0, false);
        var options = new BulkVgApplyOptions(CopyVisibility: true, CopyProjection: false, CopyCut: false,
            CopyFill: false, CopyTransparencyAndHalftone: true);

        var differences = BulkVgDifferenceDetector.Compare(source, target, options);

        Assert.Equal(new[] { "Visibility", "Transparency/Halftone" }, differences);
    }

    [Fact]
    public void Compare_reports_enabled_flag_changes_even_when_both_filters_are_effectively_invisible()
    {
        var source = new BulkVgComparableState(false, "p", "c", "f", 0, false, Enabled: true);
        var target = new BulkVgComparableState(false, "p", "c", "f", 0, false, Enabled: false);

        var differences = BulkVgDifferenceDetector.Compare(source, target,
            new BulkVgApplyOptions(true, false, false, false, false));

        Assert.Equal(new[] { "Visibility" }, differences);
    }

    [Fact]
    public void Compare_ignores_enabled_flag_when_visibility_is_unchecked()
    {
        var source = new BulkVgComparableState(false, "p", "c", "f", 0, false, Enabled: true);
        var target = new BulkVgComparableState(false, "p", "c", "f", 0, false, Enabled: false);

        var differences = BulkVgDifferenceDetector.Compare(source, target,
            new BulkVgApplyOptions(false, true, false, false, false));

        Assert.Empty(differences);
    }
}
