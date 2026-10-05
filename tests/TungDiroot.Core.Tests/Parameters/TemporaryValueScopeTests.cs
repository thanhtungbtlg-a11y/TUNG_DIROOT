using System;
using TungDiroot.Core.Parameters;
using Xunit;

namespace TungDiroot.Core.Tests.Parameters;

public sealed class TemporaryValueScopeTests
{
    [Fact]
    public void Temporary_value_is_visible_during_action_and_restored_after_success()
    {
        string? current = "old.txt";

        var observed = TemporaryValueScope.Use(
            () => current,
            value => current = value,
            "selected.txt",
            () => current);

        Assert.Equal("selected.txt", observed);
        Assert.Equal("old.txt", current);
    }

    [Fact]
    public void Temporary_value_is_restored_after_action_throws()
    {
        string? current = "old.txt";

        Assert.Throws<InvalidOperationException>(() => TemporaryValueScope.Use<object?>(
            () => current,
            value => current = value,
            "selected.txt",
            () => throw new InvalidOperationException("read failed")));

        Assert.Equal("old.txt", current);
    }
}
