using System;

namespace TungDiroot.Addin.Batch;

public sealed record BatchItemResult(Guid LinkId, bool Succeeded, bool Skipped, string ResultCode, string? Message);
