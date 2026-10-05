using System;
using System.IO;
using System.Linq;
using TungDiroot.Core.DataLink;
using Xunit;

namespace TungDiroot.Core.Tests.DataLink;

public sealed class DataLinkProfileRepositoryTests
{
    [Fact]
    public void Profile_round_trips_scope_filters_and_parameters()
    {
        using var fixture = new ProfileDirectory();
        var repository = new DataLinkProfileRepository(fixture.Path);
        var profile = new DataLinkProfile(
            DataLinkProfile.CurrentSchemaVersion,
            "Mechanical QA",
            DataLinkScope.CurrentSelection,
            new long[] { -2000011, -2008044 },
            new[] { "Duct", "Pipe" },
            new[] { "Standard" },
            new[] { new DataLinkParameterSelection("builtin:-1001203", false) });

        repository.Save(profile);
        var loaded = Assert.Single(repository.LoadAll()).Profile!;

        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.Scope, loaded.Scope);
        Assert.Equal(profile.CategoryIds, loaded.CategoryIds);
        Assert.Equal(profile.FamilyNames, loaded.FamilyNames);
        Assert.Equal(profile.TypeNames, loaded.TypeNames);
        Assert.Equal(profile.Parameters, loaded.Parameters);
    }

    [Fact]
    public void Invalid_profile_is_reported_without_hiding_valid_profiles()
    {
        using var fixture = new ProfileDirectory();
        var repository = new DataLinkProfileRepository(fixture.Path);
        repository.Save(new DataLinkProfile(
            DataLinkProfile.CurrentSchemaVersion,
            "valid",
            DataLinkScope.WholeModel,
            Array.Empty<long>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { new DataLinkParameterSelection("builtin:-1", false) }));
        File.WriteAllText(System.IO.Path.Combine(fixture.Path, "future.json"),
            "{\"schemaVersion\":999,\"name\":\"future\",\"scope\":0,\"categoryIds\":[],\"familyNames\":[],\"typeNames\":[],\"parameters\":[]}");

        var results = repository.LoadAll();

        Assert.Single(results, result => result.IsSuccess);
        Assert.Contains(results, result => result.ErrorCode == "DATALINK_PROFILE_SCHEMA_UNSUPPORTED");
    }

    private sealed class ProfileDirectory : IDisposable
    {
        public ProfileDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TungDirootDataLinkProfiles", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
