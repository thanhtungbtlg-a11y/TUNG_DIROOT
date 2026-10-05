using System;
using Newtonsoft.Json;

namespace TungDiroot.Core.Diagnostics;

public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string AddinVersion,
    string RevitVersion,
    Guid LinkId,
    string SourcePath,
    string Step,
    string ResultCode,
    string? ExceptionType,
    string? ExceptionMessage)
{
    public static DiagnosticEvent ForLinkFailure(Guid linkId, string sourcePath, string resultCode, Exception? exception = null,
        string addinVersion = "unknown", string revitVersion = "unknown", string step = "Update") =>
        new(DateTimeOffset.UtcNow, addinVersion, revitVersion, linkId, sourcePath ?? string.Empty, step, resultCode,
            exception?.GetType().FullName, exception?.Message);

    public string ToJson() => JsonConvert.SerializeObject(this);
}
