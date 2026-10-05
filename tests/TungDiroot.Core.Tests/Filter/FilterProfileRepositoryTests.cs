using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TungDiroot.Core.Filter;
using Xunit;

namespace TungDiroot.Core.Tests.Filter;

public sealed class FilterProfileRepositoryTests
{
    [Fact]
    public void Unicode_name_and_complete_query_round_trip()
    {
        using var fixture = new ProfileDirectory();
        var repository = new FilterProfileRepository(fixture.Path);
        var profile = Profile("Bơm cấp nước tầng 01");

        repository.Save(profile);
        var loaded = Assert.Single(repository.LoadAll()).Profile!;

        Assert.Equal(profile.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.Query.Scope, loaded.Query.Scope);
        Assert.Equal(profile.Query.CategoryIds, loaded.Query.CategoryIds);
        Assert.Equal(profile.Query.FamilyNames, loaded.Query.FamilyNames);
        Assert.Equal(profile.Query.TypeNames, loaded.Query.TypeNames);
        Assert.Equal(profile.Query.Rules, loaded.Query.Rules);
        Assert.Equal(profile.Query.Join, loaded.Query.Join);
        Assert.Equal(profile.GroupParameterStableId, loaded.GroupParameterStableId);
        Assert.Equal(profile.PaletteHex, loaded.PaletteHex);
    }

    [Fact]
    public void Invalid_filename_characters_are_sanitized_and_duplicate_name_replaces_atomically()
    {
        using var fixture = new ProfileDirectory();
        var repository = new FilterProfileRepository(fixture.Path);

        repository.Save(Profile("MEP: cấp/thoát", "#112233"));
        repository.Save(Profile("MEP: cấp/thoát", "#AABBCC"));

        var result = Assert.Single(repository.LoadAll());
        Assert.True(result.IsSuccess);
        Assert.Equal("#AABBCC", Assert.Single(result.Profile!.PaletteHex));
        Assert.Single(Directory.GetFiles(fixture.Path, "*.json"));
        Assert.DoesNotContain(Directory.GetFiles(fixture.Path), path => path.Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public void Corrupt_json_and_future_schema_are_isolated_from_valid_profiles()
    {
        using var fixture = new ProfileDirectory();
        var repository = new FilterProfileRepository(fixture.Path);
        repository.Save(Profile("valid"));
        File.WriteAllText(System.IO.Path.Combine(fixture.Path, "broken.json"), "{oops");
        File.WriteAllText(System.IO.Path.Combine(fixture.Path, "future.json"),
            JsonConvert.SerializeObject(Profile("future") with { SchemaVersion = 999 }));

        var results = repository.LoadAll();

        Assert.Single(results, result => result.IsSuccess);
        Assert.Contains(results, result => result.ErrorCode == "PROFILE_JSON_INVALID");
        Assert.Contains(results, result => result.ErrorCode == "FILTER_PROFILE_SCHEMA_UNSUPPORTED");
    }

    [Fact]
    public void Serialized_profile_contains_query_but_never_cached_result_element_ids()
    {
        using var fixture = new ProfileDirectory();
        var repository = new FilterProfileRepository(fixture.Path);
        repository.Save(Profile("No cache"));

        var json = File.ReadAllText(Assert.Single(Directory.GetFiles(fixture.Path, "*.json")));

        Assert.Contains("parameter:Comments", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementIds", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UniqueIds", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Results", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("112233")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    public void Invalid_palette_is_rejected_before_save(string color)
    {
        using var fixture = new ProfileDirectory();
        var repository = new FilterProfileRepository(fixture.Path);

        var exception = Assert.Throws<InvalidOperationException>(() => repository.Save(Profile("bad", color)));

        Assert.Equal("FILTER_PROFILE_PALETTE_INVALID", exception.Message);
        Assert.Empty(Directory.GetFiles(fixture.Path));
    }

    [Fact]
    public void Invalid_query_is_rejected_before_save()
    {
        using var fixture = new ProfileDirectory();
        var repository = new FilterProfileRepository(fixture.Path);
        var profile = Profile("bad query") with
        {
            Query = Query() with
            {
                Rules = new[] { new FilterRule("parameter:X", FilterOperator.GreaterThan, "abc", FilterValueKind.Number) }
            }
        };

        Assert.Throws<InvalidOperationException>(() => repository.Save(profile));
    }

    private static FilterProfile Profile(string name, string color = "#112233") =>
        new(FilterProfile.CurrentSchemaVersion, name, Query(), "parameter:Group", new[] { color });

    private static FilterQuery Query() => new(
        FilterScope.ActiveView,
        new long[] { 10 },
        new[] { "Pipe" },
        new[] { "Standard" },
        new[] { new FilterRule("parameter:Comments", FilterOperator.Contains, "QA", FilterValueKind.String) },
        FilterJoin.And);

    private sealed class ProfileDirectory : IDisposable
    {
        public ProfileDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TungDirootFilterProfiles", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
