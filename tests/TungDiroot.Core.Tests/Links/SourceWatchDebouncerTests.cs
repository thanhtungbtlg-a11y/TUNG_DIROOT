using System;
using System.Linq;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class SourceWatchDebouncerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(SourceWatchChangeKind.Created)]
    [InlineData(SourceWatchChangeKind.Changed)]
    public void Create_and_change_are_released_after_quiet_period(SourceWatchChangeKind kind)
    {
        using var debouncer = new SourceWatchDebouncer(TimeSpan.FromMilliseconds(500));
        Assert.True(debouncer.Record(kind, @"C:\Data\A.xlsx", null, Start));
        Assert.Empty(debouncer.DrainReady(Start.AddMilliseconds(499)));
        Assert.Equal(@"C:\Data\A.xlsx", Assert.Single(debouncer.DrainReady(Start.AddMilliseconds(500))));
    }

    [Fact]
    public void Rename_reports_both_old_and_new_paths()
    {
        using var debouncer = new SourceWatchDebouncer(TimeSpan.FromMilliseconds(250));
        debouncer.Record(SourceWatchChangeKind.Renamed, @"C:\Data\B.xlsx", @"C:\Data\A.xlsx", Start);

        var ready = debouncer.DrainReady(Start.AddMilliseconds(250));

        Assert.Equal(new[] { @"C:\Data\A.xlsx", @"C:\Data\B.xlsx" }, ready.OrderBy(path => path));
    }

    [Fact]
    public void Burst_is_deduplicated_and_latest_event_extends_deadline()
    {
        using var debouncer = new SourceWatchDebouncer(TimeSpan.FromMilliseconds(500));
        debouncer.Record(SourceWatchChangeKind.Changed, @"C:\Data\A.xlsx", null, Start);
        debouncer.Record(SourceWatchChangeKind.Changed, @"c:\data\a.xlsx", null, Start.AddMilliseconds(400));

        Assert.Empty(debouncer.DrainReady(Start.AddMilliseconds(500)));
        Assert.Single(debouncer.DrainReady(Start.AddMilliseconds(900)));
        Assert.Empty(debouncer.DrainReady(Start.AddSeconds(2)));
    }

    [Fact]
    public void Dispose_clears_pending_changes_and_rejects_future_events()
    {
        var debouncer = new SourceWatchDebouncer(TimeSpan.FromMilliseconds(100));
        debouncer.Record(SourceWatchChangeKind.Deleted, @"C:\Data\A.xlsx", null, Start);

        debouncer.Dispose();

        Assert.False(debouncer.Record(SourceWatchChangeKind.Changed, @"C:\Data\A.xlsx", null, Start.AddSeconds(1)));
        Assert.Empty(debouncer.DrainReady(Start.AddSeconds(1)));
    }
}
