using System;
using System.IO;
using System.Linq;
using TungDiroot.Core.Parameters;
using TungDiroot.Core.Tests.Fixtures;
using Xunit;

namespace TungDiroot.Core.Tests.Parameters;

public sealed class ParameterRuleWorkbookReaderTests
{
    private static readonly string[] Headers =
        { "ENABLED", "PARAMETER_GROUP", "PARAMETER_NAME", "BINDING", "REVIT_GROUP", "CATEGORIES" };

    [Fact]
    public void Reader_normalizes_headers_values_and_category_duplicates()
    {
        using var fixture = TestWorkbookFactory.CreateParameterRuleWorkbook(
            new[] { " enabled ", "Parameter_Group", "PARAMETER_NAME", "binding", "REVIT_GROUP", "categories" },
            new[] { new[] { "YES", " Identity ", " Asset_Code ", " instance ", " PG_DATA ", " OST_Doors ; OST_Windows;OST_Doors " } });

        var rule = Assert.Single(new ParameterRuleWorkbookReader().Read(fixture.Path));

        Assert.True(rule.Enabled);
        Assert.Equal(2, rule.ExcelRow);
        Assert.Equal("Identity", rule.DefinitionGroup);
        Assert.Equal(ParameterBindingKind.Instance, rule.BindingKind);
        Assert.Equal(new[] { "OST_Doors", "OST_Windows" }, rule.CategoryKeys);
        Assert.Empty(rule.Issues);
    }

    [Theory]
    [InlineData("missing-sheet", "PARAMETER_LOADER")]
    [InlineData("missing-header", "CATEGORIES")]
    public void Reader_reports_workbook_contract_errors(string fixtureKind, string expectedText)
    {
        using var fixture = TestWorkbookFactory.CreateInvalidParameterRuleWorkbook(fixtureKind);
        var error = Assert.Throws<InvalidDataException>(() => new ParameterRuleWorkbookReader().Read(fixture.Path));
        Assert.Contains(expectedText, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reader_rejects_legacy_xls_extension()
    {
        using var fixture = TestWorkbookFactory.CreateParameterRuleWorkbook(Headers, Array.Empty<string[]>());
        var legacy = Path.ChangeExtension(fixture.Path, ".xls");
        File.Move(fixture.Path, legacy);

        var error = Assert.Throws<NotSupportedException>(() => new ParameterRuleWorkbookReader().Read(legacy));

        Assert.Contains(".xlsx", error.Message);
        Assert.Contains(".xlsm", error.Message);
    }

    [Fact]
    public void Reader_reads_xlsm_without_changing_bytes()
    {
        using var fixture = TestWorkbookFactory.CreateParameterRuleWorkbook(
            Headers, new[] { new[] { "TRUE", "Identity", "Asset", "TYPE", "PG_DATA", "OST_Doors" } }, macroEnabled: true);
        var before = File.ReadAllBytes(fixture.Path);

        var rules = new ParameterRuleWorkbookReader().Read(fixture.Path);

        Assert.Single(rules);
        Assert.Equal(before, File.ReadAllBytes(fixture.Path));
    }

    [Fact]
    public void Reader_skips_blank_rows_and_retains_disabled_rows()
    {
        using var fixture = TestWorkbookFactory.CreateParameterRuleWorkbook(Headers, new[]
        {
            new[] { "", "", "", "", "", "" },
            new[] { "NO", "Identity", "Disabled", "INSTANCE", "PG_DATA", "OST_Doors" },
            new[] { "X", "Identity", "Enabled", "TYPE", "PG_DATA", "OST_Windows" }
        });

        var rules = new ParameterRuleWorkbookReader().Read(fixture.Path);

        Assert.Equal(2, rules.Count);
        Assert.False(rules[0].Enabled);
        Assert.Equal(3, rules[0].ExcelRow);
        Assert.True(rules[1].Enabled);
        Assert.Equal(4, rules[1].ExcelRow);
    }

    [Fact]
    public void Reader_records_bad_binding_and_empty_categories_as_row_issues()
    {
        using var fixture = TestWorkbookFactory.CreateParameterRuleWorkbook(Headers, new[]
        {
            new[] { "1", "Identity", "Asset", "PROJECT", "PG_DATA", "" }
        });

        var rule = Assert.Single(new ParameterRuleWorkbookReader().Read(fixture.Path));

        Assert.Null(rule.BindingKind);
        Assert.Contains(rule.Issues, issue => issue.Code == "BINDING_INVALID");
        Assert.Contains(rule.Issues, issue => issue.Code == "CATEGORIES_REQUIRED");
    }

    [Fact]
    public void Reader_allows_an_existing_shared_read_handle()
    {
        using var fixture = TestWorkbookFactory.CreateParameterRuleWorkbook(
            Headers, new[] { new[] { "TRUE", "Identity", "Asset", "TYPE", "PG_DATA", "OST_Doors" } });
        using var handle = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        var rules = new ParameterRuleWorkbookReader().Read(fixture.Path);

        Assert.Single(rules);
    }
}
