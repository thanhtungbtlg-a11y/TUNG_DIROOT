using System;
using TungDiroot.Core.Documents;
using Xunit;

namespace TungDiroot.Core.Tests.Documents;

public sealed class DocumentPageSelectionTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("1,3,5-7", "1,3,5-7")]
    [InlineData("7-5, 3, 3", "3,5-7")]
    public void Parse_normalizes_unique_pages(string expression, string expected)
    {
        var result = DocumentPageSelection.Parse(expression);

        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1,a")]
    [InlineData("1-")]
    public void Parse_rejects_invalid_expressions(string expression) =>
        Assert.Throws<ArgumentException>(() => DocumentPageSelection.Parse(expression));

    [Fact]
    public void Parse_rejects_unreasonably_large_selection() =>
        Assert.Throws<ArgumentException>(() => DocumentPageSelection.Parse("1-501"));
}
