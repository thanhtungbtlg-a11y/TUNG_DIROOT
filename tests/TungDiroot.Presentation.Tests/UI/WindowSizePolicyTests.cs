using TungDiroot.Presentation.UI;
using Xunit;

namespace TungDiroot.Presentation.Tests.UI;

public sealed class WindowSizePolicyTests
{
    [Fact]
    public void Clamp_KeepsPreferredSize_WhenItFitsTheWorkArea()
    {
        var result = WindowSizePolicy.Clamp(1800, 980, 760, 520, 1920, 1080, 32);

        Assert.Equal(1800, result.Width);
        Assert.Equal(980, result.Height);
    }

    [Fact]
    public void Clamp_UsesAvailableWorkArea_WhenPreferredSizeDoesNotFit()
    {
        var result = WindowSizePolicy.Clamp(1800, 980, 760, 520, 1366, 768, 32);

        Assert.Equal(1334, result.Width);
        Assert.Equal(736, result.Height);
    }

    [Fact]
    public void Clamp_NeverExceedsTheWorkArea_WhenItIsSmallerThanThePreferredMinimum()
    {
        var result = WindowSizePolicy.Clamp(1800, 980, 760, 520, 700, 480, 32);

        Assert.Equal(668, result.Width);
        Assert.Equal(448, result.Height);
    }
}
