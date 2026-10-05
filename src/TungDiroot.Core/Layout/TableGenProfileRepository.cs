using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TungDiroot.Core.Profiles;

namespace TungDiroot.Core.Layout;

public sealed class TableGenProfileRepository
{
    private readonly JsonProfileStore<TableGenCompanyProfile> _store;

    public TableGenProfileRepository(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TUNG_DIROOT",
            "profiles",
            "tablegen");
        _store = new JsonProfileStore<TableGenCompanyProfile>(root, profile => profile.Name, Validate);
    }

    public IReadOnlyList<ProfileLoadResult<TableGenCompanyProfile>> LoadAll() => _store.LoadAll();
    public void Save(TableGenCompanyProfile profile) => _store.Save(profile);
    public bool Delete(string profileName) => _store.Delete(profileName);

    private static string? Validate(TableGenCompanyProfile profile)
    {
        if (profile.SchemaVersion != TableGenCompanyProfile.CurrentSchemaVersion)
            return "TABLEGEN_PROFILE_SCHEMA_UNSUPPORTED";
        if (string.IsNullOrWhiteSpace(profile.Name)) return "TABLEGEN_PROFILE_NAME_REQUIRED";
        if (profile.Numbers == null || profile.StyleMappings == null)
            return "TABLEGEN_PROFILE_DATA_REQUIRED";
        if (profile.Numbers.DecimalPlaces < 0 || profile.Numbers.DecimalPlaces > 12)
            return "TABLEGEN_PROFILE_DECIMAL_PLACES_INVALID";
        if (string.IsNullOrEmpty(profile.Numbers.DecimalSymbol))
            return "TABLEGEN_PROFILE_DECIMAL_SYMBOL_REQUIRED";
        if (profile.Numbers.UseThousandsSeparator && string.IsNullOrEmpty(profile.Numbers.ThousandsSeparator))
            return "TABLEGEN_PROFILE_GROUP_SYMBOL_REQUIRED";
        if (string.Equals(profile.Numbers.DecimalSymbol, profile.Numbers.ThousandsSeparator, StringComparison.Ordinal))
            return "TABLEGEN_PROFILE_NUMBER_SYMBOLS_CONFLICT";
        if (profile.StyleMappings.Any(mapping => string.IsNullOrWhiteSpace(mapping.CellStyleKey)))
            return "TABLEGEN_PROFILE_STYLE_KEY_REQUIRED";
        return null;
    }
}

