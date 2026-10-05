namespace TungDiroot.Core.Excel;

public enum HorizontalAlignment
{
    General,
    Left,
    Center,
    Right,
    Fill,
    Justify,
    CenterContinuous,
    Distributed
}

public enum VerticalAlignment
{
    Bottom,
    Top,
    Center,
    Justify,
    Distributed
}

public sealed record FontModel(string Name, double SizePoints, bool Bold, bool Italic, bool Underline, string? Color);

public sealed record FillModel(string Pattern, string? ForegroundColor, string? BackgroundColor);

public sealed record BorderSideModel(string Style, string? Color);

public sealed record BorderModel(BorderSideModel Left, BorderSideModel Right, BorderSideModel Top, BorderSideModel Bottom);

public sealed record CellStyleModel(
    FontModel Font,
    FillModel Fill,
    BorderModel Border,
    HorizontalAlignment Horizontal,
    VerticalAlignment Vertical,
    double RotationDegrees,
    string NumberFormatCode)
{
    public static CellStyleModel Default { get; } = new CellStyleModel(
        new FontModel("Calibri", 11D, false, false, false, null),
        new FillModel("none", null, null),
        new BorderModel(
            new BorderSideModel("none", null),
            new BorderSideModel("none", null),
            new BorderSideModel("none", null),
            new BorderSideModel("none", null)),
        HorizontalAlignment.General,
        VerticalAlignment.Bottom,
        0D,
        "General");
}
