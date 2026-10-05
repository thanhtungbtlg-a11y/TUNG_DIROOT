using System;
using TungDiroot.Core.Diagnostics;
using Xunit;

namespace TungDiroot.Core.Tests.Diagnostics;

public sealed class DiagnosticEventTests
{
    [Fact]
    public void Log_event_never_contains_cell_values()
    {
        var line = DiagnosticEvent.ForLinkFailure(Guid.Parse("11111111-1111-1111-1111-111111111111"), @"X:\A.xlsx", "FILE_LOCKED").ToJson();
        Assert.DoesNotContain("CellValues", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DisplayText", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FILE_LOCKED", line);
    }
}
