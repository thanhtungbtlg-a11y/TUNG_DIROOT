using System;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Layout;

public static class BlackAndWhiteTransform
{
    public static CellStyleModel Apply(CellStyleModel style, bool preserveGrayscale)
    {
        if (style == null) throw new ArgumentNullException(nameof(style));
        return style with
        {
            Font = style.Font with { Color = "FF000000" },
            Fill = style.Fill with
            {
                Pattern = "none",
                ForegroundColor = preserveGrayscale ? ToGrayscale(style.Fill.ForegroundColor) : null,
                BackgroundColor = null
            },
            Border = new BorderModel(
                ToBlack(style.Border.Left),
                ToBlack(style.Border.Right),
                ToBlack(style.Border.Top),
                ToBlack(style.Border.Bottom))
        };
    }

    private static BorderSideModel ToBlack(BorderSideModel side) => side with { Color = "FF000000" };

    private static string? ToGrayscale(string? color)
    {
        if (color == null || color.Length < 6) return color;
        var rgb = color.Substring(color.Length - 6);
        if (!int.TryParse(rgb.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var red) ||
            !int.TryParse(rgb.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var green) ||
            !int.TryParse(rgb.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue)) return color;
        var gray = (int)Math.Round((red * 0.299D) + (green * 0.587D) + (blue * 0.114D));
        return "FF" + gray.ToString("X2") + gray.ToString("X2") + gray.ToString("X2");
    }
}
