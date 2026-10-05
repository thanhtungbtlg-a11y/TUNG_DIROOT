using Autodesk.Revit.UI;

namespace TungDiroot.Addin.Batch;

public sealed class TableGenExternalEventHandler : IExternalEventHandler
{
    private RevitBatchCoordinator? _coordinator;
    internal void Attach(RevitBatchCoordinator coordinator) => _coordinator = coordinator;
    public void Execute(UIApplication app) => _coordinator?.Execute(app);
    public string GetName() => "TUNG_DIROOT TableGen Batch";
}
