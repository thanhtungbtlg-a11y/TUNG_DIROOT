using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TungDiroot.Core.Profiles;

namespace TungDiroot.Core.Filter;

public sealed class FilterProfileRepository
{
    private static readonly Regex PaletteColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);
    private readonly JsonProfileStore<FilterProfile> _store;

    public FilterProfileRepository(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TUNG_DIROOT",
            "profiles",
            "filter");
        _store = new JsonProfileStore<FilterProfile>(root, profile => profile.Name, Validate);
    }

    public IReadOnlyList<ProfileLoadResult<FilterProfile>> LoadAll() => _store.LoadAll();
    public void Save(FilterProfile profile) => _store.Save(profile);
    public void Delete(string name) => _store.Delete(name);

    private static string? Validate(FilterProfile profile)
    {
        if (profile.SchemaVersion != FilterProfile.CurrentSchemaVersion)
            return "FILTER_PROFILE_SCHEMA_UNSUPPORTED";
        if (string.IsNullOrWhiteSpace(profile.Name)) return "FILTER_PROFILE_NAME_REQUIRED";
        if (profile.Query == null) return "FILTER_PROFILE_QUERY_REQUIRED";
        if (profile.GroupParameterStableId == null) return "FILTER_PROFILE_GROUP_PARAMETER_REQUIRED";
        if (profile.PaletteHex == null || profile.PaletteHex.Count == 0) return "FILTER_PROFILE_PALETTE_REQUIRED";
        foreach (var color in profile.PaletteHex)
            if (color == null || !PaletteColor.IsMatch(color)) return "FILTER_PROFILE_PALETTE_INVALID";
        var errors = FilterRuleEvaluator.Validate(profile.Query);
        return errors.Count == 0 ? null : "FILTER_PROFILE_QUERY_INVALID:" + string.Join(";", errors);
    }
}
