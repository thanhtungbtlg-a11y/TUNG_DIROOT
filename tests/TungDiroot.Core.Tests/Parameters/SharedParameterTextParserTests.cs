using System;
using System.IO;
using System.Text;
using TungDiroot.Core.Parameters;
using Xunit;

namespace TungDiroot.Core.Tests.Parameters;

public sealed class SharedParameterTextParserTests
{
    [Fact]
    public void Parser_resolves_parameter_group_name_and_guid_from_utf16()
    {
        using var file = TempTextFile.Create(Standard("GROUP\t1\tIdentity\r\n" + Parameter("1", "Asset_Code")), Encoding.Unicode);

        var definition = Assert.Single(new SharedParameterTextParser().Parse(file.Path));

        Assert.Equal("Identity", definition.GroupName);
        Assert.Equal("Asset_Code", definition.Name);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), definition.Guid);
    }

    [Fact]
    public void Parser_supports_group_declared_after_parameter_in_utf8_bom_file()
    {
        using var file = TempTextFile.Create(Standard(Parameter("7", "Asset_Code") + "GROUP\t7\tIdentity\r\n"), new UTF8Encoding(true));

        var definition = Assert.Single(new SharedParameterTextParser().Parse(file.Path));

        Assert.Equal("Identity", definition.GroupName);
    }

    [Fact]
    public void Parser_ignores_headers_meta_comments_and_blank_lines()
    {
        using var file = TempTextFile.Create(
            "# comment\r\n\r\nMETA\t2\t1\r\n*GROUP\tID\tNAME\r\nGROUP\t1\tIdentity\r\n" +
            "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\r\n" + Parameter("1", "Asset_Code"),
            new UTF8Encoding(false));

        Assert.Single(new SharedParameterTextParser().Parse(file.Path));
    }

    [Fact]
    public void Parser_reports_unknown_group_with_line_number()
    {
        using var file = TempTextFile.Create(Standard(Parameter("9", "Asset_Code")), Encoding.Unicode);

        var error = Assert.Throws<InvalidDataException>(() => new SharedParameterTextParser().Parse(file.Path));

        Assert.Contains("9", error.Message);
        Assert.Contains("line", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_reports_malformed_guid_with_line_number()
    {
        using var file = TempTextFile.Create(Standard("GROUP\t1\tIdentity\r\nPARAM\tnot-a-guid\tAsset_Code\tTEXT\t\t1\r\n"), Encoding.Unicode);

        var error = Assert.Throws<InvalidDataException>(() => new SharedParameterTextParser().Parse(file.Path));

        Assert.Contains("GUID", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_rejects_duplicate_group_and_name()
    {
        using var file = TempTextFile.Create(Standard(
            "GROUP\t1\tIdentity\r\n" + Parameter("1", "Asset_Code") +
            "PARAM\t22222222-2222-2222-2222-222222222222\tAsset_Code\tTEXT\t\t1\r\n"), Encoding.Unicode);

        var error = Assert.Throws<InvalidDataException>(() => new SharedParameterTextParser().Parse(file.Path));

        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_rejects_duplicate_guid_with_different_names()
    {
        using var file = TempTextFile.Create(Standard(
            "GROUP\t1\tIdentity\r\n" + Parameter("1", "Asset_Code") + Parameter("1", "Asset_Tag")), Encoding.Unicode);

        var error = Assert.Throws<InvalidDataException>(() => new SharedParameterTextParser().Parse(file.Path));

        Assert.Contains("GUID", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_propagates_missing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        Assert.Throws<FileNotFoundException>(() => new SharedParameterTextParser().Parse(path));
    }

    private static string Standard(string body) =>
        "# This is a Revit shared parameter file.\r\n*GROUP\tID\tNAME\r\n" +
        "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\r\n" + body;

    private static string Parameter(string group, string name) =>
        $"PARAM\t11111111-1111-1111-1111-111111111111\t{name}\tTEXT\t\t{group}\t1\tAsset code\t1\r\n";

    private sealed class TempTextFile : IDisposable
    {
        private readonly string _directory;
        private TempTextFile(string path, string directory) { Path = path; _directory = directory; }
        public string Path { get; }

        public static TempTextFile Create(string content, Encoding encoding)
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TungDirootTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "shared-parameters.txt");
            File.WriteAllText(path, content, encoding);
            return new TempTextFile(path, directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
