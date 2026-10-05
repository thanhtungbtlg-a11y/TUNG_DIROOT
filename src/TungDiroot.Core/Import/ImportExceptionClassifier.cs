using System;
using System.IO;

namespace TungDiroot.Core.Import;

public sealed record ImportFailure(string Code);

public static class ImportExceptionClassifier
{
    public static ImportFailure Classify(Exception exception)
    {
        if (exception == null) throw new ArgumentNullException(nameof(exception));
        return exception switch
        {
            FileNotFoundException => new ImportFailure("FILE_MISSING"),
            DirectoryNotFoundException => new ImportFailure("FILE_MISSING"),
            UnauthorizedAccessException => new ImportFailure("FILE_LOCKED"),
            IOException => new ImportFailure("FILE_LOCKED"),
            InvalidOperationException invalid when invalid.Message == "FORMULA_CACHE_MISSING" => new ImportFailure("FORMULA_CACHE_MISSING"),
            InvalidOperationException invalid when invalid.Message == "FONT_FALLBACK" => new ImportFailure("FONT_FALLBACK"),
            InvalidOperationException invalid when invalid.Message == "VIEW_NOT_EDITABLE" => new ImportFailure("VIEW_NOT_EDITABLE"),
            InvalidOperationException invalid when invalid.Message == "VIEW_DELETED" => new ImportFailure("VIEW_DELETED"),
            InvalidOperationException invalid when invalid.Message == "EXCEL_NOT_INSTALLED" => new ImportFailure("EXCEL_NOT_INSTALLED"),
            InvalidOperationException invalid when invalid.Message == "REVIT_TRANSACTION_NOT_STARTED" => new ImportFailure("REVIT_TRANSACTION_NOT_STARTED"),
            InvalidOperationException invalid when invalid.Message == "REVIT_TRANSACTION_NOT_COMMITTED" => new ImportFailure("REVIT_TRANSACTION_NOT_COMMITTED"),
            InvalidOperationException invalid when invalid.Message == "DATA_ONLY_TEXT_SCOPE_CHANGED" ||
                invalid.Message.StartsWith("DATA_ONLY_TEXT_SCOPE_CHANGED:", StringComparison.Ordinal) => new ImportFailure("DATA_ONLY_TEXT_SCOPE_CHANGED"),
            InvalidOperationException invalid when invalid.Message == "DATA_ONLY_STRUCTURE_CHANGED" ||
                invalid.Message.StartsWith("DATA_ONLY_STRUCTURE_CHANGED:", StringComparison.Ordinal) => new ImportFailure("DATA_ONLY_STRUCTURE_CHANGED"),
            InvalidOperationException invalid when invalid.Message == "LINKED_IMAGE_TYPE_SHARED" ||
                invalid.Message.StartsWith("LINKED_IMAGE_TYPE_SHARED:", StringComparison.Ordinal) => new ImportFailure("LINKED_IMAGE_TYPE_SHARED"),
            _ => new ImportFailure("UNKNOWN_ERROR")
        };
    }
}
