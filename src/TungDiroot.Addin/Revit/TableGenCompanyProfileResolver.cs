using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Layout;

namespace TungDiroot.Addin.Revit;

internal sealed record ResolvedTableGenCompanyProfile(
    NumericDisplayOptions Numbers,
    StyleMappingProfile Styles,
    string? Name);

internal static class TableGenCompanyProfileResolver
{
    public static ResolvedTableGenCompanyProfile Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Default();
        var profile = new TableGenProfileRepository().LoadAll()
            .Where(result => result.IsSuccess)
            .Select(result => result.Profile!)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return profile == null
            ? Default()
            : new ResolvedTableGenCompanyProfile(profile.Numbers, profile.ToStyleMappingProfile(), profile.Name);
    }

    private static ResolvedTableGenCompanyProfile Default() => new(
        NumericDisplayOptions.ExcelDefault,
        new StyleMappingProfile(new Dictionary<string, RevitStyleMapping>()),
        null);
}

