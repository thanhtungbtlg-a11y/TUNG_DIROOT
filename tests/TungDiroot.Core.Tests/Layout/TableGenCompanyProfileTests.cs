using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;
using Xunit;

namespace TungDiroot.Core.Tests.Layout;

public sealed class TableGenCompanyProfileTests
{
    [Fact]
    public void Repository_round_trips_unicode_formatting_and_style_mappings()
    {
        using var fixture = new ProfileDirectory();
        var repository = new TableGenProfileRepository(fixture.Path);
        var style = CellStyleModel.Default with
        {
            Font = CellStyleModel.Default.Font with { Name = "Arial", Bold = true }
        };
        var key = CellStyleKey.Create(style);
        var profile = new TableGenCompanyProfile(
            TableGenCompanyProfile.CurrentSchemaVersion,
            "Hồ sơ MEP",
            new NumericDisplayOptions(false, ",", true, ".", 2),
            new[] { new TableGenStyleMapping(key, "text-uid", "line-uid") });

        repository.Save(profile);
        var loaded = Assert.Single(repository.LoadAll()).Profile!;

        Assert.Equal("Hồ sơ MEP", loaded.Name);
        Assert.Equal(",", loaded.Numbers.DecimalSymbol);
        Assert.Equal(".", loaded.Numbers.ThousandsSeparator);
        var mapping = loaded.ToStyleMappingProfile().Resolve(style);
        Assert.NotNull(mapping);
        Assert.Equal("text-uid", mapping!.TextNoteTypeUniqueId);
        Assert.Equal("line-uid", mapping.GraphicsStyleUniqueId);
    }

    [Fact]
    public void Corrupt_profile_is_isolated_while_valid_profile_loads()
    {
        using var fixture = new ProfileDirectory();
        var repository = new TableGenProfileRepository(fixture.Path);
        repository.Save(TableGenCompanyProfile.Default("Company"));
        File.WriteAllText(Path.Combine(fixture.Path, "broken.json"), "{broken");

        var results = repository.LoadAll();

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal("PROFILE_JSON_INVALID", Assert.Single(results, result => !result.IsSuccess).ErrorCode);
    }

    [Fact]
    public void Future_profile_schema_is_rejected_without_hiding_other_profiles()
    {
        using var fixture = new ProfileDirectory();
        var repository = new TableGenProfileRepository(fixture.Path);
        repository.Save(TableGenCompanyProfile.Default("Current"));
        var future = TableGenCompanyProfile.Default("Future") with { SchemaVersion = 99 };
        File.WriteAllText(Path.Combine(fixture.Path, "future.json"), Newtonsoft.Json.JsonConvert.SerializeObject(future));

        var results = repository.LoadAll();

        Assert.Single(results, result => result.IsSuccess && result.Profile!.Name == "Current");
        Assert.Equal("TABLEGEN_PROFILE_SCHEMA_UNSUPPORTED",
            Assert.Single(results, result => !result.IsSuccess).ErrorCode);
    }

    private sealed class ProfileDirectory : IDisposable
    {
        public ProfileDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TungDirootTableGenProfiles", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}

