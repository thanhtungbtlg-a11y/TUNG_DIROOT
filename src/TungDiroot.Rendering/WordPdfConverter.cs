using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TungDiroot.Rendering;

public sealed class WordPdfConverter
{
    private readonly IWordPdfBridgeFactory _factory;

    public WordPdfConverter() : this(new WordPdfBridgeFactory()) { }

    internal WordPdfConverter(IWordPdfBridgeFactory factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public void Convert(string sourcePath, string destinationPdfPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("SOURCE_PATH_REQUIRED", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(destinationPdfPath)) throw new ArgumentException("DESTINATION_PATH_REQUIRED", nameof(destinationPdfPath));
        var directory = Path.GetDirectoryName(destinationPdfPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = destinationPdfPath + "." + Guid.NewGuid().ToString("N") + ".tmp.pdf";
        try
        {
            using (var bridge = _factory.Create()) bridge.Convert(sourcePath, temporaryPath);
            var output = new FileInfo(temporaryPath);
            if (!output.Exists || output.Length == 0) throw new InvalidOperationException("WORD_PDF_EMPTY");
            File.Copy(temporaryPath, destinationPdfPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }
}

internal interface IWordPdfBridgeFactory
{
    IWordPdfBridge Create();
}

internal interface IWordPdfBridge : IDisposable
{
    void Convert(string sourcePath, string destinationPdfPath);
}

internal sealed class WordPdfBridgeFactory : IWordPdfBridgeFactory
{
    public IWordPdfBridge Create() => new WordPdfBridge();
}

internal sealed class WordPdfBridge : IWordPdfBridge
{
    private dynamic? _application;
    private dynamic? _documents;
    private dynamic? _document;
    private object? _previousAutomationSecurity;

    public WordPdfBridge()
    {
        try
        {
            var wordType = Type.GetTypeFromProgID("Word.Application", throwOnError: false)
                ?? throw new InvalidOperationException("WORD_NOT_INSTALLED");
            _application = Activator.CreateInstance(wordType) ?? throw new InvalidOperationException("WORD_START_FAILED");
            _application.Visible = false;
            _application.DisplayAlerts = 0; // wdAlertsNone
            _previousAutomationSecurity = _application.AutomationSecurity;
            _application.AutomationSecurity = 3; // msoAutomationSecurityForceDisable
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Convert(string sourcePath, string destinationPdfPath)
    {
        _documents = _application!.Documents;
        _document = _documents.Open(
            sourcePath,
            ConfirmConversions: false,
            ReadOnly: true,
            AddToRecentFiles: false,
            Visible: false,
            OpenAndRepair: false,
            NoEncodingDialog: true);
        _document.ExportAsFixedFormat(destinationPdfPath, 17); // wdExportFormatPDF
    }

    public void Dispose()
    {
        try { if (_document != null) _document.Close(false); } catch { }
        try { if (_application != null && _previousAutomationSecurity != null) _application!.AutomationSecurity = _previousAutomationSecurity; } catch { }
        try { if (_application != null) _application.Quit(false); } catch { }
        Release(ref _document);
        Release(ref _documents);
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
