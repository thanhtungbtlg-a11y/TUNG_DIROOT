using System;
using System.Collections.Generic;
using System.IO;
using TungDiroot.Core.Profiles;

namespace TungDiroot.Core.DataLink;

public sealed class DataLinkProfileRepository
{
    private readonly JsonProfileStore<DataLinkProfile> _store;

    public DataLinkProfileRepository(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TUNG_DIROOT",
            "profiles",
            "datalink");
        _store = new JsonProfileStore<DataLinkProfile>(root, profile => profile.Name, Validate);
    }

    public IReadOnlyList<ProfileLoadResult<DataLinkProfile>> LoadAll() => _store.LoadAll();
    public void Save(DataLinkProfile profile) => _store.Save(profile);
    public bool Delete(string profileName) => _store.Delete(profileName);

    private static string? Validate(DataLinkProfile profile)
    {
        if (profile.SchemaVersion != DataLinkProfile.CurrentSchemaVersion)
            return "DATALINK_PROFILE_SCHEMA_UNSUPPORTED";
        if (string.IsNullOrWhiteSpace(profile.Name)) return "DATALINK_PROFILE_NAME_REQUIRED";
        if (profile.CategoryIds == null || profile.FamilyNames == null || profile.TypeNames == null || profile.Parameters == null)
            return "DATALINK_PROFILE_COLLECTION_REQUIRED";
        if (profile.Parameters.Count == 0) return "DATALINK_PROFILE_PARAMETER_REQUIRED";
        return null;
    }
}
