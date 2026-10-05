using System;
using System.IO;

namespace TungDiroot.Core.Documents;

public enum DocumentSourceKind
{
    Pdf,
    Word
}

public enum DocumentImportDpi
{
    Dpi150 = 150,
    Dpi300 = 300,
    Dpi600 = 600
}

public sealed record DocumentImportRequest(
    string SourcePath,
    DocumentSourceKind SourceKind,
    DocumentPageSelection Pages,
    DocumentImportDpi Dpi,
    int Columns,
    string RequestedViewName,
    bool PreferRelativePath)
{
    public static DocumentImportRequest Create(
        string sourcePath,
        string pageExpression,
        DocumentImportDpi dpi,
        int columns,
        string requestedViewName,
        bool preferRelativePath = true)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("SOURCE_PATH_REQUIRED", nameof(sourcePath));
        if (columns < 1 || columns > 8) throw new ArgumentOutOfRangeException(nameof(columns));
        if (!Enum.IsDefined(typeof(DocumentImportDpi), dpi)) throw new ArgumentOutOfRangeException(nameof(dpi));
        if (string.IsNullOrWhiteSpace(requestedViewName)) throw new ArgumentException("VIEW_NAME_REQUIRED", nameof(requestedViewName));

        var extension = Path.GetExtension(sourcePath);
        var kind = extension.ToLowerInvariant() switch
        {
            ".pdf" => DocumentSourceKind.Pdf,
            ".doc" or ".docx" or ".docm" => DocumentSourceKind.Word,
            _ => throw new ArgumentException("SOURCE_TYPE_UNSUPPORTED", nameof(sourcePath))
        };
        return new DocumentImportRequest(
            sourcePath.Trim(), kind, DocumentPageSelection.Parse(pageExpression), dpi, columns, requestedViewName.Trim(), preferRelativePath);
    }
}
