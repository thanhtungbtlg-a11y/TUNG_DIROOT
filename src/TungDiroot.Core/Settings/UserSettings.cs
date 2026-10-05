using TungDiroot.Core.Import;

namespace TungDiroot.Core.Settings;

public sealed record UserSettings(
    ImageDpi DefaultDpi = ImageDpi.Dpi300,
    string RendererPreference = "Automatic",
    bool PreferRelativePaths = true,
    bool ConfirmLargeImports = true,
    string? LastSharedParameterPath = null,
    string? LastParameterWorkbookPath = null);
