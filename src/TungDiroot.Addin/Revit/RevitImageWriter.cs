using System;
using System.IO;
using Autodesk.Revit.DB;
using TungDiroot.Core.Excel;
using TungDiroot.Rendering;

namespace TungDiroot.Addin.Revit;

public sealed class RevitImageWriter
{
    private readonly IWorkbookImageRenderer _renderer;

    public RevitImageWriter(IWorkbookImageRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public ImageInstance Write(
        Document document,
        View view,
        WorkbookRangeModel model,
        ImageRenderOptions options,
        Guid linkId)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (view == null) throw new ArgumentNullException(nameof(view));
        var temporaryPath = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                _renderer.Render(model, options, stream);
            return PlacePng(document, view, temporaryPath, linkId);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static ImageInstance PlacePng(Document document, View view, string pngPath, Guid linkId)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (!File.Exists(pngPath)) throw new FileNotFoundException("Prepared image does not exist.", pngPath);
        using var imageOptions = new ImageTypeOptions(pngPath, false, ImageTypeSource.Import);
        var imageType = ImageType.Create(document, imageOptions);
        using var placement = new ImagePlacementOptions(XYZ.Zero, BoxPlacement.TopLeft);
        var instance = ImageInstance.Create(document, view, imageType.Id, placement);
        ElementLinkTagger.Tag(imageType, linkId, string.Empty, "ImageType");
        ElementLinkTagger.Tag(instance, linkId, string.Empty, "ImageInstance");
        return instance;
    }
}
