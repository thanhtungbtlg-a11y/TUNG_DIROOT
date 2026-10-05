using System;
using System.IO;
using System.Linq;
using TungDiroot.Core.Profiles;
using Xunit;

namespace TungDiroot.Core.Tests.Profiles;

public sealed class JsonProfileStoreTests
{
    [Fact]
    public void Corrupt_json_is_isolated_while_other_profiles_still_load()
    {
        using var fixture = new ProfileDirectory();
        var store = new JsonProfileStore<TestProfile>(fixture.Path, profile => profile.Name);
        store.Save(new TestProfile("valid", 3));
        File.WriteAllText(System.IO.Path.Combine(fixture.Path, "broken.json"), "{not-json");

        var results = store.LoadAll();

        var loaded = Assert.Single(results, result => result.IsSuccess);
        Assert.Equal("valid", loaded.Profile!.Name);
        var failed = Assert.Single(results, result => !result.IsSuccess);
        Assert.Equal("PROFILE_JSON_INVALID", failed.ErrorCode);
        Assert.EndsWith("broken.json", failed.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_replaces_an_existing_profile_without_leaving_temp_files()
    {
        using var fixture = new ProfileDirectory();
        var store = new JsonProfileStore<TestProfile>(fixture.Path, profile => profile.Name);

        store.Save(new TestProfile("office", 1));
        store.Save(new TestProfile("office", 2));

        Assert.Equal(2, Assert.Single(store.LoadAll()).Profile!.Value);
        Assert.DoesNotContain(Directory.GetFiles(fixture.Path), path => path.Contains(".tmp-", StringComparison.Ordinal));
    }

    public sealed record TestProfile(string Name, int Value);

    private sealed class ProfileDirectory : IDisposable
    {
        public ProfileDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TungDirootProfiles", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
