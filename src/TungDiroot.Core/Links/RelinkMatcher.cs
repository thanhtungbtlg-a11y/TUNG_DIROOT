using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TungDiroot.Core.Links;

public sealed record CandidateWorkbook(string FullPath, IReadOnlyCollection<string> Regions)
{
    public string FileName => Path.GetFileName(FullPath);
    public bool Contains(string worksheet, string region) => Regions.Contains(worksheet + "!" + region, StringComparer.OrdinalIgnoreCase);
}

public enum RelinkMatchKind
{
    None,
    ExactFileAndWorksheet,
    Ambiguous
}

public sealed record RelinkResult(RelinkMatchKind Kind, CandidateWorkbook? Match, IReadOnlyList<CandidateWorkbook> Candidates)
{
    public static RelinkResult None { get; } = new(RelinkMatchKind.None, null, Array.Empty<CandidateWorkbook>());
}

public static class RelinkMatcher
{
    public static RelinkResult Match(LinkRecord link, IReadOnlyList<CandidateWorkbook> candidates)
    {
        if (link == null) throw new ArgumentNullException(nameof(link));
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        var fileName = Path.GetFileName(link.SourcePath.Value);
        var valid = candidates
            .Where(candidate => StringComparer.OrdinalIgnoreCase.Equals(candidate.FileName, fileName))
            .Where(candidate => candidate.Contains(link.WorksheetName, link.RegionReference))
            .ToArray();
        return valid.Length switch
        {
            0 => RelinkResult.None,
            1 => new RelinkResult(RelinkMatchKind.ExactFileAndWorksheet, valid[0], valid),
            _ => new RelinkResult(RelinkMatchKind.Ambiguous, null, valid)
        };
    }
}
