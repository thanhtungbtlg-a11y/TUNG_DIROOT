using System;
using System.Collections.Generic;
using TungDiroot.Core.Runtime;
using Xunit;

namespace TungDiroot.Core.Tests.Runtime;

public sealed class BestEffortBatchRunnerTests
{
    [Fact]
    public void Run_continues_after_a_recoverable_native_item_failure()
    {
        var completed = new List<int>();
        var skipped = new List<int>();

        var skippedCount = BestEffortBatchRunner.Run(
            new[] { 10, 20, 30 },
            item =>
            {
                if (item == 20) throw new AccessViolationException("invalid native handle");
                completed.Add(item);
            },
            exception => exception is AccessViolationException,
            (item, _) => skipped.Add(item));

        Assert.Equal(new[] { 10, 30 }, completed);
        Assert.Equal(new[] { 20 }, skipped);
        Assert.Equal(1, skippedCount);
    }

    [Fact]
    public void Run_rethrows_a_non_recoverable_failure()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BestEffortBatchRunner.Run(
            new[] { 1 },
            _ => throw new InvalidOperationException("programming error"),
            candidate => candidate is AccessViolationException));

        Assert.Equal("programming error", exception.Message);
    }
}
