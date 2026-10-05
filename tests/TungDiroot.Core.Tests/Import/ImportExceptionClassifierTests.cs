using System;
using System.IO;
using TungDiroot.Core.Import;
using Xunit;

namespace TungDiroot.Core.Tests.Import;

public sealed class ImportExceptionClassifierTests
{
    [Theory]
    [InlineData(typeof(FileNotFoundException), "FILE_MISSING")]
    [InlineData(typeof(DirectoryNotFoundException), "FILE_MISSING")]
    [InlineData(typeof(UnauthorizedAccessException), "FILE_LOCKED")]
    [InlineData(typeof(IOException), "FILE_LOCKED")]
    public void Classifies_expected_exception(Type exceptionType, string code)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "test")!;
        Assert.Equal(code, ImportExceptionClassifier.Classify(exception).Code);
    }

    [Theory]
    [InlineData("FORMULA_CACHE_MISSING")]
    [InlineData("FONT_FALLBACK")]
    [InlineData("VIEW_NOT_EDITABLE")]
    [InlineData("VIEW_DELETED")]
    [InlineData("EXCEL_NOT_INSTALLED")]
    [InlineData("DATA_ONLY_TEXT_SCOPE_CHANGED")]
    [InlineData("DATA_ONLY_STRUCTURE_CHANGED")]
    [InlineData("REVIT_TRANSACTION_NOT_STARTED")]
    [InlineData("REVIT_TRANSACTION_NOT_COMMITTED")]
    public void Preserves_stable_domain_result_codes(string code) =>
        Assert.Equal(code, ImportExceptionClassifier.Classify(new InvalidOperationException(code)).Code);

    [Fact]
    public void Preserves_text_scope_error_code_with_actionable_details()
    {
        var exception = new InvalidOperationException("DATA_ONLY_TEXT_SCOPE_CHANGED: Choose Recreate explicitly.");

        Assert.Equal("DATA_ONLY_TEXT_SCOPE_CHANGED", ImportExceptionClassifier.Classify(exception).Code);
    }

    [Fact]
    public void Preserves_structure_error_code_with_explicit_rebuild_instruction()
    {
        var exception = new InvalidOperationException("DATA_ONLY_STRUCTURE_CHANGED: Choose Dựng lại bảng (Recreate) explicitly.");

        Assert.Equal("DATA_ONLY_STRUCTURE_CHANGED", ImportExceptionClassifier.Classify(exception).Code);
    }

    [Theory]
    [InlineData("LINKED_IMAGE_TYPE_SHARED")]
    [InlineData("LINKED_IMAGE_TYPE_SHARED: Image is used outside the managed link.")]
    public void Reports_shared_image_safety_failure_instead_of_unknown_error(string message) =>
        Assert.Equal("LINKED_IMAGE_TYPE_SHARED", ImportExceptionClassifier.Classify(new InvalidOperationException(message)).Code);
}
