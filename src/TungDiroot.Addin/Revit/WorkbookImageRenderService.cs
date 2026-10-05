using System;
using System.IO;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Rendering;

namespace TungDiroot.Addin.Revit;

public sealed class PreparedWorkbookImage : IDisposable
{
    internal PreparedWorkbookImage(string path, RendererKind rendererKind)
    {
        Path = path;
        RendererKind = rendererKind;
    }

    public string Path { get; }
    public RendererKind RendererKind { get; }

    public void Dispose()
    {
        try { if (File.Exists(Path)) File.Delete(Path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public static class WorkbookImageRenderService
{
    public static PreparedWorkbookImage Prepare(
        WorkbookRangeModel model,
        string sourcePath,
        string worksheetName,
        ExcelRangeReference range,
        ImageRenderOptions options,
        ImageRendererEngine engine)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        var preference = engine switch
        {
            ImageRendererEngine.BuiltIn => RendererPreference.BuiltInOnly,
            ImageRendererEngine.ExcelHighFidelity => RendererPreference.ExcelHighFidelityOnly,
            _ => RendererPreference.Automatic
        };
        var selected = RendererSelector.Select(preference, new ExcelAvailabilityProbe());
        try { return Render(selected, model, sourcePath, worksheetName, range, options); }
        catch when (engine == ImageRendererEngine.Automatic && selected == RendererKind.ExcelHighFidelity)
        {
            return Render(RendererKind.BuiltIn, model, sourcePath, worksheetName, range, options);
        }
    }

    private static PreparedWorkbookImage Render(
        RendererKind selected,
        WorkbookRangeModel model,
        string sourcePath,
        string worksheetName,
        ExcelRangeReference range,
        ImageRenderOptions options)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TUNG_DIROOT-Prepared-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (selected == RendererKind.ExcelHighFidelity)
                    new ExcelComWorkbookImageRenderer().RenderFileRange(sourcePath, worksheetName, range.ToA1(), stream);
                else
                    new BuiltInWorkbookImageRenderer().Render(model, options, stream);
            }
            return new PreparedWorkbookImage(path, selected);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }
}
