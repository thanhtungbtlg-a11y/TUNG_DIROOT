using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;

namespace TungDiroot.Rendering;

public sealed class BuiltInWorkbookImageRenderer : IWorkbookImageRenderer
{
    private const long MaximumPixels = 100_000_000L;

    public ImageRenderResult Render(WorkbookRangeModel model, ImageRenderOptions options, Stream destination)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (!destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));

        var layout = TableLayoutPlanner.Create(model, new LayoutOptions(options.BlackAndWhite, options.PreserveGrayscale));
        var scale = (int)options.Dpi / 72D;
        var width = Math.Max(1, (int)Math.Ceiling(layout.WidthPoints * scale));
        var height = Math.Max(1, (int)Math.Ceiling(layout.HeightPoints * scale));
        if ((long)width * height > MaximumPixels) throw new InvalidOperationException("IMAGE_PIXEL_LIMIT_EXCEEDED");
        var warnings = new List<WorkbookReadWarning>(layout.Warnings);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            foreach (var fill in layout.Fills)
                drawing.DrawRectangle(new SolidColorBrush(ParseColor(fill.Color, Colors.Transparent)), null, Scale(fill.Bounds, scale));
            foreach (var border in layout.Borders)
                drawing.DrawLine(new Pen(new SolidColorBrush(ParseColor(border.Color, Colors.Black)), BorderThickness(border.Style, scale)),
                    new Point(border.X1 * scale, border.Y1 * scale), new Point(border.X2 * scale, border.Y2 * scale));
            foreach (var text in layout.Texts)
            {
                var font = FontFallbackResolver.Resolve(text.Style.Font.Name);
                if (font.UsedFallback)
                    warnings.Add(new WorkbookReadWarning("FONT_FALLBACK", $"Font '{font.Requested}' was replaced with '{font.Resolved}'."));
                var typeface = new Typeface(
                    new FontFamily(font.Resolved),
                    text.Style.Font.Italic ? FontStyles.Italic : FontStyles.Normal,
                    text.Style.Font.Bold ? FontWeights.Bold : FontWeights.Normal,
                    FontStretches.Normal);
                var formatted = new FormattedText(
                    text.Text,
                    CultureInfo.GetCultureInfo("vi-VN"),
                    FlowDirection.LeftToRight,
                    typeface,
                    Math.Max(1D, text.Style.Font.SizePoints * scale),
                    new SolidColorBrush(ParseColor(text.Style.Font.Color, Colors.Black)),
                    1D)
                {
                    MaxTextWidth = Math.Max(1D, text.Bounds.Width * scale),
                    MaxTextHeight = Math.Max(1D, text.Bounds.Height * scale),
                    TextAlignment = ToTextAlignment(text.Style.Horizontal)
                };
                drawing.DrawText(formatted, new Point(text.Bounds.X * scale, text.Bounds.Y * scale));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, (int)options.Dpi, (int)options.Dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        var bytes = buffer.ToArray();
        destination.Write(bytes, 0, bytes.Length);
        return new ImageRenderResult(width, height, bytes, warnings);
    }

    private static Rect Scale(LayoutRectangle rectangle, double scale) =>
        new Rect(rectangle.X * scale, rectangle.Y * scale, rectangle.Width * scale, rectangle.Height * scale);

    private static double BorderThickness(string style, double scale) =>
        (string.Equals(style, "medium", StringComparison.OrdinalIgnoreCase) ? 1.5D :
        string.Equals(style, "thick", StringComparison.OrdinalIgnoreCase) ? 2D : 0.75D) * Math.Max(1D, scale / 2D);

    private static TextAlignment ToTextAlignment(TungDiroot.Core.Excel.HorizontalAlignment alignment) => alignment switch
    {
        TungDiroot.Core.Excel.HorizontalAlignment.Center or TungDiroot.Core.Excel.HorizontalAlignment.CenterContinuous => TextAlignment.Center,
        TungDiroot.Core.Excel.HorizontalAlignment.Right => TextAlignment.Right,
        TungDiroot.Core.Excel.HorizontalAlignment.Justify => TextAlignment.Justify,
        _ => TextAlignment.Left
    };

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrEmpty(value)) return fallback;
        var hex = value!.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb)) return fallback;
        return Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }
}
