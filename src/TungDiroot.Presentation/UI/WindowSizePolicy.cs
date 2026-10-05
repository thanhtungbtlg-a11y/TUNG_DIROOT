using System;

namespace TungDiroot.Presentation.UI;

public readonly struct WindowSize
{
    public WindowSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}

public static class WindowSizePolicy
{
    public static WindowSize Clamp(
        double preferredWidth,
        double preferredHeight,
        double minimumWidth,
        double minimumHeight,
        double workAreaWidth,
        double workAreaHeight,
        double margin)
    {
        var availableWidth = Math.Max(1, workAreaWidth - margin);
        var availableHeight = Math.Max(1, workAreaHeight - margin);
        var width = Math.Min(availableWidth, Math.Max(minimumWidth, preferredWidth));
        var height = Math.Min(availableHeight, Math.Max(minimumHeight, preferredHeight));
        return new WindowSize(width, height);
    }
}
