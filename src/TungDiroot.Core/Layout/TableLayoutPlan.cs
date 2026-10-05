using System;
using System.Collections.Generic;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Layout;

public enum BorderRole
{
    Inner,
    Outer
}

public sealed record LayoutRectangle(double X, double Y, double Width, double Height);

public sealed record TextPlacement(
    string CellKey,
    string Text,
    LayoutRectangle Bounds,
    CellStyleModel Style,
    string? MappingKey = null);

public sealed record LinePlacement(
    double X1,
    double Y1,
    double X2,
    double Y2,
    string Style,
    string? Color,
    BorderRole Role,
    string? MappingKey = null);

public sealed record FillPlacement(LayoutRectangle Bounds, string Color);

public sealed class TableLayoutPlan
{
    public TableLayoutPlan(
        IEnumerable<TextPlacement> texts,
        IEnumerable<LinePlacement> borders,
        IEnumerable<FillPlacement> fills,
        double widthPoints,
        double heightPoints,
        IEnumerable<WorkbookReadWarning> warnings)
    {
        Texts = new ReadOnlyList<TextPlacement>(texts);
        Borders = new ReadOnlyList<LinePlacement>(borders);
        Fills = new ReadOnlyList<FillPlacement>(fills);
        Warnings = new ReadOnlyList<WorkbookReadWarning>(warnings);
        WidthPoints = widthPoints;
        HeightPoints = heightPoints;
    }

    public IReadOnlyList<TextPlacement> Texts { get; }
    public IReadOnlyList<LinePlacement> Borders { get; }
    public IReadOnlyList<FillPlacement> Fills { get; }
    public double WidthPoints { get; }
    public double HeightPoints { get; }
    public IReadOnlyList<WorkbookReadWarning> Warnings { get; }
}

public sealed record LayoutOptions(bool BlackAndWhite, bool PreserveGrayscale)
{
    public static LayoutOptions Default { get; } = new LayoutOptions(false, true);
}
