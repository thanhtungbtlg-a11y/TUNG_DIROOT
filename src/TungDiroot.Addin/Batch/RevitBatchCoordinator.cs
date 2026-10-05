using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using System.Windows.Threading;
using TungDiroot.Core.Batch;

namespace TungDiroot.Addin.Batch;

public interface ITableGenBatchItemProcessor
{
    BatchItemResult ProcessOne(UIApplication application, Guid linkId);
}

public sealed class RevitBatchCoordinator : IDisposable
{
    private readonly object _gate = new();
    private readonly ITableGenBatchItemProcessor _processor;
    private readonly ExternalEvent _externalEvent;
    private IReadOnlyList<Guid> _queue = Array.Empty<Guid>();
    private BatchStateMachine _state = BatchStateMachine.Start(0);
    private int _nextIndex;

    public RevitBatchCoordinator(ITableGenBatchItemProcessor processor)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        var handler = new TableGenExternalEventHandler();
        handler.Attach(this);
        _externalEvent = ExternalEvent.Create(handler);
    }

    public event Action<BatchProgress>? ProgressChanged;
    public event Action<BatchItemResult>? ItemCompleted;
    public bool IsRunning
    {
        get
        {
            lock (_gate) return _state.RunState is not (BatchRunState.Completed or BatchRunState.Stopped or BatchRunState.Idle);
        }
    }

    public void Enqueue(IEnumerable<Guid> linkIds)
    {
        if (linkIds == null) throw new ArgumentNullException(nameof(linkIds));
        lock (_gate)
        {
            if (_state.RunState is not (BatchRunState.Completed or BatchRunState.Stopped or BatchRunState.Idle))
                throw new InvalidOperationException("BATCH_ALREADY_RUNNING");
            _queue = new List<Guid>(linkIds).AsReadOnly();
            _nextIndex = 0;
            _state = BatchStateMachine.Start(_queue.Count);
        }
        PublishAndRaiseIfNeeded();
    }

    public void Pause() { lock (_gate) _state = _state.Pause(); Publish(); }
    public void Resume() { lock (_gate) _state = _state.Resume(); PublishAndRaiseIfNeeded(); }
    public void RequestStop() { lock (_gate) _state = _state.RequestStop(); Publish(); }

    internal void Execute(UIApplication app)
    {
        Guid linkId;
        lock (_gate)
        {
            if (!_state.ShouldRaiseNext || _nextIndex >= _queue.Count) return;
            _state = _state.BeginNext();
            linkId = _queue[_nextIndex++];
        }
        BatchItemResult result;
        try { result = _processor.ProcessOne(app, linkId); }
        catch (Exception exception) { result = new BatchItemResult(linkId, false, false, "UNKNOWN_ERROR", exception.Message); }
        lock (_gate)
        {
            _state = _state.CompleteCurrent(result.Skipped ? BatchItemOutcome.Skipped : result.Succeeded ? BatchItemOutcome.Succeeded : BatchItemOutcome.Failed);
        }
        ItemCompleted?.Invoke(result);
        Publish();
        bool raise;
        lock (_gate) raise = _state.ShouldRaiseNext;
        if (raise) Dispatcher.CurrentDispatcher.BeginInvoke(new Action(RaiseIfNeeded), DispatcherPriority.Background);
    }

    private void Publish() { BatchProgress value; lock (_gate) value = _state.Progress; ProgressChanged?.Invoke(value); }
    private void PublishAndRaiseIfNeeded()
    {
        bool raise;
        BatchProgress value;
        lock (_gate) { value = _state.Progress; raise = _state.ShouldRaiseNext; }
        ProgressChanged?.Invoke(value);
        if (raise) RaiseIfNeeded();
    }

    private void RaiseIfNeeded()
    {
        lock (_gate)
        {
            if (!_state.ShouldRaiseNext) return;
        }
        var request = _externalEvent.Raise();
        if (request == ExternalEventRequest.Accepted) return;
        lock (_gate) _state = _state.RequestStop();
        Publish();
    }

    public void Dispose() => _externalEvent.Dispose();
}
