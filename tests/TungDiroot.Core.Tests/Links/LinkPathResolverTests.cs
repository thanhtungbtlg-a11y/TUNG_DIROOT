using System;
using System.Linq;
using Newtonsoft.Json;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class LinkPathResolverTests
{
    [Fact]
    public void Stored_path_round_trips_through_registry_json()
    {
        var original = new StoredPath(
            @"C:\BIM\Data\A.xlsx",
            PathMode.Absolute,
            new[] { new StoredPathWarning("MODEL_PATH_UNAVAILABLE", "Model path is unavailable.") });

        var json = JsonConvert.SerializeObject(original);
        var restored = JsonConvert.DeserializeObject<StoredPath>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Value, restored.Value);
        Assert.Equal(original.Mode, restored.Mode);
        var warning = Assert.Single(restored.Warnings);
        Assert.Equal("MODEL_PATH_UNAVAILABLE", warning.Code);
        Assert.Equal("Model path is unavailable.", warning.Message);
    }

    [Fact]
    public void Relative_path_round_trips_against_saved_model_folder()
    {
        var stored = LinkPathResolver.ToStoredPath(@"C:\BIM\Data\A.xlsx", @"C:\BIM\Model.rvt", preferRelative: true);

        Assert.Equal(@"Data\A.xlsx", stored.Value);
        Assert.Equal(PathMode.Relative, stored.Mode);
        Assert.Equal(@"C:\BIM\Data\A.xlsx", LinkPathResolver.ResolveStoredPath(stored, @"C:\BIM\Model.rvt"));
    }

    [Fact]
    public void Relative_path_uses_windows_case_insensitive_root_and_normalizes_dot_segments()
    {
        var stored = LinkPathResolver.ToStoredPath(@"c:/BIM/Models/../Data/A.xlsx", @"C:\bim\Model.rvt", preferRelative: true);

        Assert.Equal(PathMode.Relative, stored.Mode);
        Assert.Equal(@"Data\A.xlsx", stored.Value);
    }

    [Fact]
    public void Unsaved_model_keeps_absolute_source_and_reports_a_deterministic_warning()
    {
        var stored = LinkPathResolver.ToStoredPath(@"C:\BIM\Data\A.xlsx", null, preferRelative: true);

        Assert.Equal(PathMode.Absolute, stored.Mode);
        Assert.Equal(@"C:\BIM\Data\A.xlsx", stored.Value);
        Assert.Equal("MODEL_PATH_UNAVAILABLE", Assert.Single(stored.Warnings).Code);
    }

    [Fact]
    public void Different_drive_never_uses_relative_storage()
    {
        var stored = LinkPathResolver.ToStoredPath(@"D:\Data\A.xlsx", @"C:\BIM\Model.rvt", preferRelative: true);

        Assert.Equal(PathMode.Absolute, stored.Mode);
        Assert.Empty(stored.Warnings);
    }

    [Fact]
    public void Unc_model_path_is_not_considered_a_stable_local_model_path()
    {
        var stored = LinkPathResolver.ToStoredPath(@"\\server\share\Data\A.xlsx", @"\\server\share\Models\Model.rvt", preferRelative: true);

        Assert.Equal(PathMode.Absolute, stored.Mode);
        Assert.Equal("MODEL_PATH_UNAVAILABLE", Assert.Single(stored.Warnings).Code);
    }

    [Fact]
    public void Relative_stored_path_resolves_against_the_model_after_its_folder_moves()
    {
        var stored = new StoredPath(@"Data\A.xlsx", PathMode.Relative);

        Assert.Equal(@"D:\Moved\Data\A.xlsx", LinkPathResolver.ResolveStoredPath(stored, @"D:\Moved\Model.rvt"));
    }

    [Fact]
    public void Relative_stored_path_requires_a_stable_local_model_path()
    {
        var stored = new StoredPath(@"Data\A.xlsx", PathMode.Relative);

        Assert.Throws<InvalidOperationException>(() => LinkPathResolver.ResolveStoredPath(stored, string.Empty));
    }

    [Fact]
    public void Relative_stored_path_cannot_be_rooted_or_escape_its_declared_mode()
    {
        var stored = new StoredPath(@"C:\Data\A.xlsx", PathMode.Relative);

        Assert.Throws<ArgumentException>(() => LinkPathResolver.ResolveStoredPath(stored, @"C:\BIM\Model.rvt"));
    }

    [Theory]
    [InlineData(@"\Data\A.xlsx")]
    [InlineData("/Data/A.xlsx")]
    [InlineData(@"\\server\share\Data\A.xlsx")]
    [InlineData("//server/share/Data/A.xlsx")]
    [InlineData(@"C:\Data\A.xlsx")]
    [InlineData(@"C:Data\A.xlsx")]
    public void Relative_stored_path_rejects_all_rooted_windows_forms(string value)
    {
        var stored = new StoredPath(value, PathMode.Relative);

        Assert.Throws<ArgumentException>(() => LinkPathResolver.ResolveStoredPath(stored, @"C:\BIM\Model.rvt"));
    }

    [Fact]
    public void Relative_stored_path_normalizes_dot_segments_but_rejects_volume_root_escape()
    {
        var normalized = new StoredPath(@"Data\.\Archive\..\A.xlsx", PathMode.Relative);
        var escaping = new StoredPath(@"..\..\A.xlsx", PathMode.Relative);

        Assert.Equal(@"C:\BIM\Data\A.xlsx", LinkPathResolver.ResolveStoredPath(normalized, @"C:\BIM\Model.rvt"));
        Assert.Throws<ArgumentException>(() => LinkPathResolver.ResolveStoredPath(escaping, @"C:\BIM\Model.rvt"));
    }
}
