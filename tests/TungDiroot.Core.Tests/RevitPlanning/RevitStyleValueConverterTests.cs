using TungDiroot.Core.RevitPlanning;
using Xunit;

namespace TungDiroot.Core.Tests.RevitPlanning;

public sealed class RevitStyleValueConverterTests
{
    [Theory]
    [InlineData(10, 10)]
    [InlineData(11, 11)]
    [InlineData(0, 1)]
    public void Schedule_font_size_is_in_points_not_internal_feet(double points, double expected) =>
        Assert.Equal(expected, RevitStyleValueConverter.ScheduleTextSize(points));

    [Theory]
    [InlineData(90, 900)]
    [InlineData(-45, 3150)]
    [InlineData(12.5, 125)]
    [InlineData(360, 0)]
    public void Schedule_orientation_is_tenths_of_a_degree(double degrees, int expected) =>
        Assert.Equal(expected, RevitStyleValueConverter.ScheduleTextOrientation(degrees));

    [Theory]
    [InlineData(255, 0, 0, 255, "FFFF0000")]
    [InlineData(0, 0, 255, 16711680, "FF0000FF")]
    [InlineData(17, 34, 51, 3351057, "FF112233")]
    public void Text_type_color_uses_red_in_low_byte(byte red, byte green, byte blue, int expected, string argb)
    {
        Assert.Equal(expected, RevitStyleValueConverter.TextColorParameter(red, green, blue));
        Assert.Equal(argb, RevitStyleValueConverter.TextColorArgb(expected));
    }
}
