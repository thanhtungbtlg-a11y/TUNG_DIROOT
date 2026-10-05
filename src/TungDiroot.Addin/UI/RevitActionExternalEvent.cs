using System;
using Autodesk.Revit.UI;

namespace TungDiroot.Addin.UI;

internal sealed class RevitActionExternalEvent : IExternalEventHandler, IDisposable
{
    private readonly object _gate = new();
    private readonly ExternalEvent _externalEvent;
    private Action<UIApplication>? _pending;

    public RevitActionExternalEvent() => _externalEvent = ExternalEvent.Create(this);

    public void Raise(Action<UIApplication> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        lock (_gate)
        {
            if (_pending != null) throw new InvalidOperationException("REVIT_ACTION_ALREADY_PENDING");
            _pending = action;
        }
        var request = _externalEvent.Raise();
        if (request == ExternalEventRequest.Accepted) return;
        lock (_gate) _pending = null;
        throw new InvalidOperationException("EXTERNAL_EVENT_" + request.ToString().ToUpperInvariant());
    }

    public void Execute(UIApplication app)
    {
        Action<UIApplication>? action;
        lock (_gate) { action = _pending; _pending = null; }
        action?.Invoke(app);
    }

    public string GetName() => "TUNG_DIROOT UI Action";
    public void Dispose() => _externalEvent.Dispose();
}
