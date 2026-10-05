using System;
using System.IO;
using System.Linq;
using TungDiroot.Core.Parameters;
using Xunit;

namespace TungDiroot.Core.Tests.Parameters;

public sealed class ParameterRuleSampleContractTests
{
    [Fact]
    public void Shipped_sample_is_safe_and_covers_instance_and_type_rules()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "samples", "TUNG_DIROOT-ParameterLoader.xlsx");

        Assert.True(File.Exists(path), $"Không tìm thấy workbook mẫu: {path}");
        var rules = new ParameterRuleWorkbookReader().Read(path);

        Assert.True(rules.Count >= 2);
        Assert.All(rules, rule =>
        {
            Assert.False(rule.Enabled);
            Assert.Empty(rule.Issues);
        });
        Assert.Contains(rules, rule => rule.BindingKind == ParameterBindingKind.Instance);
        Assert.Contains(rules, rule => rule.BindingKind == ParameterBindingKind.Type);
        Assert.Contains(rules, rule => rule.ParameterName == "Manufacturer" && rule.RevitGroupKey == "PG_IDENTITY_DATA");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TUNG_DIROOT.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc TUNG_DIROOT.");
    }
}
