using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;

namespace TungDiroot.Rendering;

public sealed record ImageRenderOptions(ImageDpi Dpi, bool BlackAndWhite, bool PreserveGrayscale)
{
    public static ImageRenderOptions Default { get; } = new ImageRenderOptions(ImageDpi.Dpi300, false, true);
}

public sealed class ImageRenderResult
{
    private readonly byte[] _bytes;

    public ImageRenderResult(int pixelWidth, int pixelHeight, byte[] bytes, IEnumerable<WorkbookReadWarning> warnings)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        _bytes = (byte[])(bytes ?? throw new ArgumentNullException(nameof(bytes))).Clone();
        Warnings = new ReadOnlyCollection<WorkbookReadWarning>((warnings ?? throw new ArgumentNullException(nameof(warnings))).ToArray());
    }

    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public byte[] Bytes => (byte[])_bytes.Clone();
    public IReadOnlyList<WorkbookReadWarning> Warnings { get; }
}

public interface IWorkbookImageRenderer
{
    ImageRenderResult Render(WorkbookRangeModel model, ImageRenderOptions options, Stream destination);
}
