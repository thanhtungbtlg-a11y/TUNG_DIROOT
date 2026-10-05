using System;
using System.IO;
using TungDiroot.Core.Settings;
using Xunit;

namespace TungDiroot.Core.Tests.Settings;

public sealed class UserSettingsStoreTests
{
    [Fact]
    public void Legacy_settings_load_with_null_parameter_loader_paths()
    {
        using var fixture = new SettingsFile("{\"DefaultDpi\":2,\"RendererPreference\":\"Automatic\",\"PreferRelativePaths\":true,\"ConfirmLargeImports\":true}");

        var settings = new UserSettingsStore(fixture.Path).Load();

        Assert.Null(settings.LastSharedParameterPath);
        Assert.Null(settings.LastParameterWorkbookPath);
        Assert.Equal("Automatic", settings.RendererPreference);
    }

    [Fact]
    public void Settings_round_trip_parameter_loader_paths()
    {
        using var fixture = new SettingsFile(null);
        var store = new UserSettingsStore(fixture.Path);

        store.Save(new UserSettings(LastSharedParameterPath: "C:\\Data\\shared.txt", LastParameterWorkbookPath: "C:\\Data\\rules.xlsx"));
        var loaded = store.Load();

        Assert.Equal("C:\\Data\\shared.txt", loaded.LastSharedParameterPath);
        Assert.Equal("C:\\Data\\rules.xlsx", loaded.LastParameterWorkbookPath);
    }

    private sealed class SettingsFile : IDisposable
    {
        private readonly string _directory;
        public SettingsFile(string? content)
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TungDirootSettings", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "settings.json");
            if (content != null) File.WriteAllText(Path, content);
        }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    }
}
