namespace TungDiroot.Core.Batch;

public enum BatchRunState
{
    Idle,
    Running,
    RunningCurrentThenPause,
    Paused,
    StopRequested,
    Stopped,
    Completed
}

public enum BatchItemOutcome
{
    Succeeded,
    Failed,
    Skipped
}

public sealed record BatchProgress(
    BatchRunState RunState,
    int Total,
    int Completed,
    int Succeeded,
    int Failed,
    int Skipped,
    bool IsItemInProgress);
