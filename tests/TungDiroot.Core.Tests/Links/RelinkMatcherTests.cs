using System;
using TungDiroot.Core.Import;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class RelinkMatcherTests
{
    [Fact]
    public void Folder_relink_matches_filename_then_validates_sheet()
    {
        var result = RelinkMatcher.Match(Link(), new[]
        {
            new CandidateWorkbook(@"D:\Moved\A.xlsx", new[] { "PCCC!A1:D4" }),
            new CandidateWorkbook(@"D:\Wrong\A.xlsx", new[] { "Other!A1:D4" })
        });
        Assert.Equal(RelinkMatchKind.ExactFileAndWorksheet, result.Kind);
        Assert.Equal(@"D:\Moved\A.xlsx", result.Match!.FullPath);
    }

    [Fact]
    public void Multiple_valid_candidates_are_ambiguous_and_never_auto_selected()
    {
        var candidates = new[] { @"D:\One\A.xlsx", @"E:\Two\A.xlsx" };
        var result = RelinkMatcher.Match(Link(), Array.ConvertAll(candidates, path => new CandidateWorkbook(path, new[] { "PCCC!A1:D4" })));
        Assert.Equal(RelinkMatchKind.Ambiguous, result.Kind);
        Assert.Null(result.Match);
    }

    private static LinkRecord Link() => new(1, Guid.NewGuid(), "view", new StoredPath(@"C:\Old\A.xlsx", PathMode.Absolute),
        "PCCC", "A1:D4", ImportMode.RevitTable, TargetViewKind.Drafting, UpdateBehavior.Recreate, false,
        SourceFingerprint.Parse("1:1:" + new string('A', 64)), null, null);
}
