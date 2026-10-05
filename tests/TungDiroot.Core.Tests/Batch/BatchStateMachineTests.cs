using TungDiroot.Core.Batch;
using Xunit;

namespace TungDiroot.Core.Tests.Batch;

public sealed class BatchStateMachineTests
{
    [Fact]
    public void Stop_requested_during_item_stops_before_next_item()
    {
        var state = BatchStateMachine.Start(3);
        state = state.BeginNext().RequestStop().CompleteCurrent(BatchItemOutcome.Succeeded);
        Assert.Equal(BatchRunState.Stopped, state.RunState);
        Assert.Equal(1, state.Completed);
    }

    [Fact]
    public void Pause_does_not_interrupt_current_item()
    {
        var state = BatchStateMachine.Start(2).BeginNext().Pause();
        Assert.Equal(BatchRunState.RunningCurrentThenPause, state.RunState);
        state = state.CompleteCurrent(BatchItemOutcome.Succeeded);
        Assert.Equal(BatchRunState.Paused, state.RunState);
    }

    [Fact]
    public void Resume_continues_and_counts_outcomes()
    {
        var state = BatchStateMachine.Start(2).Pause().Resume().BeginNext().CompleteCurrent(BatchItemOutcome.Failed);
        Assert.Equal(BatchRunState.Running, state.RunState);
        state = state.BeginNext().CompleteCurrent(BatchItemOutcome.Skipped);
        Assert.Equal(BatchRunState.Completed, state.RunState);
        Assert.Equal(1, state.Progress.Failed);
        Assert.Equal(1, state.Progress.Skipped);
    }
}
