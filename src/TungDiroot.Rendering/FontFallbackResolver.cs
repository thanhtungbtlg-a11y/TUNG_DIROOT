using System;
using System.Linq;
using System.Windows.Media;

namespace TungDiroot.Rendering;

public sealed record FontFallbackResult(string Requested, string Resolved, bool UsedFallback);

public static class FontFallbackResolver
{
    private static readonly string[] Fallbacks = { "Arial", "Segoe UI" };

    public static FontFallbackResult Resolve(string? requested)
    {
        var normalized = string.IsNullOrWhiteSpace(requested) ? "Calibri" : requested!.Trim();
        var installed = Fonts.SystemFontFamilies.Select(font => font.Source).ToArray();
        if (installed.Any(font => string.Equals(font, normalized, StringComparison.OrdinalIgnoreCase)))
            return new FontFallbackResult(normalized, normalized, false);
        var fallback = Fallbacks.FirstOrDefault(candidate => installed.Any(font => string.Equals(font, candidate, StringComparison.OrdinalIgnoreCase)))
            ?? "Segoe UI";
        return new FontFallbackResult(normalized, fallback, true);
    }
}
