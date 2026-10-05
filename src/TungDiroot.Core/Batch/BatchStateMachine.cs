using System;

namespace TungDiroot.Core.Batch;

public sealed record BatchStateMachine
{
    private BatchStateMachine(BatchProgress progress) => Progress = progress;

    public BatchProgress Progress { get; }
    public BatchRunState RunState => Progress.RunState;
    public int Completed => Progress.Completed;
    public bool ShouldRaiseNext => RunState == BatchRunState.Running && Completed < Progress.Total && !Progress.IsItemInProgress;

    public static BatchStateMachine Start(int total)
    {
        if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
        return new BatchStateMachine(new BatchProgress(total == 0 ? BatchRunState.Completed : BatchRunState.Running, total, 0, 0, 0, 0, false));
    }

    public BatchStateMachine BeginNext()
    {
        if (RunState != BatchRunState.Running || Progress.IsItemInProgress || Completed >= Progress.Total)
            throw new InvalidOperationException("BATCH_NOT_READY");
        return With(runState: BatchRunState.Running, isItemInProgress: true);
    }

    public BatchStateMachine Pause()
    {
        if (RunState == BatchRunState.Running && Progress.IsItemInProgress)
            return With(runState: BatchRunState.RunningCurrentThenPause);
        if (RunState == BatchRunState.Running)
            return With(runState: BatchRunState.Paused);
        return this;
    }

    public BatchStateMachine Resume() => RunState == BatchRunState.Paused ? With(runState: BatchRunState.Running) : this;

    public BatchStateMachine RequestStop()
    {
        if (RunState is BatchRunState.Completed or BatchRunState.Stopped or BatchRunState.Idle) return this;
        return With(runState: Progress.IsItemInProgress ? BatchRunState.StopRequested : BatchRunState.Stopped);
    }

    public BatchStateMachine CompleteCurrent(BatchItemOutcome outcome)
    {
        if (!Progress.IsItemInProgress) throw new InvalidOperationException("NO_CURRENT_BATCH_ITEM");
        var completed = Progress.Completed + 1;
        var succeeded = Progress.Succeeded + (outcome == BatchItemOutcome.Succeeded ? 1 : 0);
        var failed = Progress.Failed + (outcome == BatchItemOutcome.Failed ? 1 : 0);
        var skipped = Progress.Skipped + (outcome == BatchItemOutcome.Skipped ? 1 : 0);
        var state = RunState switch
        {
            BatchRunState.StopRequested => BatchRunState.Stopped,
            BatchRunState.RunningCurrentThenPause => BatchRunState.Paused,
            _ when completed >= Progress.Total => BatchRunState.Completed,
            _ => BatchRunState.Running
        };
        return new BatchStateMachine(new BatchProgress(state, Progress.Total, completed, succeeded, failed, skipped, false));
    }

    private BatchStateMachine With(BatchRunState? runState = null, bool? isItemInProgress = null) =>
        new(new BatchProgress(runState ?? Progress.RunState, Progress.Total, Progress.Completed, Progress.Succeeded,
            Progress.Failed, Progress.Skipped, isItemInProgress ?? Progress.IsItemInProgress));
}
