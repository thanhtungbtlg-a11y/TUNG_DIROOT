using System;
using System.IO;
using TungDiroot.Core.Documents;
using TungDiroot.Rendering;

namespace TungDiroot.Addin.Documents;

public sealed record PreparedDocumentSource(
    Guid LinkId,
    DocumentImportRequest Request,
    string PdfPath,
    DocumentFileFingerprint Fingerprint);

public sealed class DocumentSourcePreparer
{
    private readonly WordPdfConverter _wordConverter;

    public DocumentSourcePreparer() : this(new WordPdfConverter()) { }
    internal DocumentSourcePreparer(WordPdfConverter wordConverter) =>
        _wordConverter = wordConverter ?? throw new ArgumentNullException(nameof(wordConverter));

    public PreparedDocumentSource Prepare(DocumentImportRequest request, Guid linkId)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!File.Exists(request.SourcePath)) throw new FileNotFoundException("SOURCE_NOT_FOUND", request.SourcePath);
        var fingerprint = DocumentFileFingerprint.Create(request.SourcePath);
        if (request.SourceKind == DocumentSourceKind.Pdf)
            return new PreparedDocumentSource(linkId, request, request.SourcePath, fingerprint);

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TUNG_DIROOT", "DocumentCache");
        Directory.CreateDirectory(root);
        // A content-versioned name avoids overwriting a PDF that Revit may still hold open.
        var cachedPdf = Path.Combine(root, linkId.ToString("N") + "-" + fingerprint.Sha256Hex.Substring(0, 12) + ".pdf");
        _wordConverter.Convert(request.SourcePath, cachedPdf);
        return new PreparedDocumentSource(linkId, request, cachedPdf, fingerprint);
    }
}
