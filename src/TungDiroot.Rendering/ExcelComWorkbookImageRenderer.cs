using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace TungDiroot.Rendering;

public sealed class ExcelComWorkbookImageRenderer
{
    private readonly IExcelComBridgeFactory _factory;

    public ExcelComWorkbookImageRenderer()
        : this(new ExcelComBridgeFactory())
    {
    }

    internal ExcelComWorkbookImageRenderer(IExcelComBridgeFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public ImageRenderResult RenderFileRange(string path, string worksheetName, string rangeReference, Stream destination)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (worksheetName == null) throw new ArgumentNullException(nameof(worksheetName));
        if (rangeReference == null) throw new ArgumentNullException(nameof(rangeReference));
        if (destination == null || !destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        var temporaryPath = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-Excel-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var bridge = _factory.Create()) bridge.ExportRangePng(path, worksheetName, rangeReference, temporaryPath);
            var bytes = File.ReadAllBytes(temporaryPath);
            destination.Write(bytes, 0, bytes.Length);
            using var imageStream = new MemoryStream(bytes, writable: false);
            var decoder = BitmapDecoder.Create(imageStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            return new ImageRenderResult(frame.PixelWidth, frame.PixelHeight, bytes, Array.Empty<TungDiroot.Core.Excel.WorkbookReadWarning>());
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

internal interface IExcelComBridgeFactory
{
    IExcelComBridge Create();
}

internal interface IExcelComBridge : IDisposable
{
    void ExportRangePng(string path, string worksheetName, string rangeReference, string outputPath);
}

internal sealed class ExcelComBridgeFactory : IExcelComBridgeFactory
{
    public IExcelComBridge Create() => new ExcelComBridge();
}

internal sealed class ExcelComBridge : IExcelComBridge
{
    private dynamic? _application;
    private dynamic? _workbooks;
    private dynamic? _workbook;
    private dynamic? _worksheet;
    private dynamic? _range;
    private dynamic? _chartObjects;
    private dynamic? _chartObject;
    private dynamic? _chart;
    private object? _previousAutomationSecurity;

    public ExcelComBridge()
    {
        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application", throwOnError: false)
                ?? throw new InvalidOperationException("EXCEL_NOT_INSTALLED");
            _application = Activator.CreateInstance(excelType) ?? throw new InvalidOperationException("EXCEL_START_FAILED");
            _application.Visible = false;
            _application.DisplayAlerts = false;
            _previousAutomationSecurity = _application.AutomationSecurity;
            _application.AutomationSecurity = 3; // msoAutomationSecurityForceDisable
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void ExportRangePng(string path, string worksheetName, string rangeReference, string outputPath)
    {
        _workbooks = _application!.Workbooks;
        _workbook = _workbooks.Open(path, ReadOnly: true, UpdateLinks: 0, IgnoreReadOnlyRecommended: true);
        _worksheet = _workbook.Worksheets[worksheetName];
        _range = _worksheet.Range[rangeReference];
        _range.CopyPicture(1, 2); // xlScreen, xlBitmap
        _chartObjects = _worksheet.ChartObjects();
        _chartObject = _chartObjects.Add(0, 0, Math.Max(1D, (double)_range.Width), Math.Max(1D, (double)_range.Height));
        _chart = _chartObject.Chart;
        _chart.Paste();
        if (!(bool)_chart.Export(outputPath, "PNG")) throw new InvalidOperationException("EXCEL_IMAGE_EXPORT_FAILED");
    }

    public void Dispose()
    {
        try { if (_chartObject != null) _chartObject.Delete(); } catch { }
        try { if (_workbook != null) _workbook.Close(false); } catch { }
        try { if (_application != null && _previousAutomationSecurity != null) _application!.AutomationSecurity = _previousAutomationSecurity; } catch { }
        try { if (_application != null) _application.Quit(); } catch { }
        Release(ref _chart);
        Release(ref _chartObject);
        Release(ref _chartObjects);
        Release(ref _range);
        Release(ref _worksheet);
        Release(ref _workbook);
        Release(ref _workbooks);
        Release(ref _application);
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static void Release(ref dynamic? value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        value = null;
    }
}
