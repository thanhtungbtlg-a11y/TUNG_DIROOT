using System;
using TungDiroot.Core.Documents;
using Xunit;

namespace TungDiroot.Core.Tests.Documents;

public sealed class DocumentImportRequestTests
{
    [Theory]
    [InlineData(@"C:\Data\A.pdf", DocumentSourceKind.Pdf)]
    [InlineData(@"C:\Data\A.DOCX", DocumentSourceKind.Word)]
    [InlineData(@"C:\Data\A.docm", DocumentSourceKind.Word)]
    public void Create_detects_supported_source_kind(string path, DocumentSourceKind expected)
    {
        var request = DocumentImportRequest.Create(path, "1,3-4", DocumentImportDpi.Dpi300, 2, "Tài liệu");

        Assert.Equal(expected, request.SourceKind);
        Assert.Equal("1,3-4", request.Pages.ToString());
    }

    [Fact]
    public void Create_rejects_unsupported_file_type() =>
        Assert.Throws<ArgumentException>(() =>
            DocumentImportRequest.Create(@"C:\Data\A.xlsx", "1", DocumentImportDpi.Dpi300, 1, "Tài liệu"));

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Create_rejects_invalid_column_count(int columns) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DocumentImportRequest.Create(@"C:\Data\A.pdf", "1", DocumentImportDpi.Dpi300, columns, "Tài liệu"));
}
