using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.UI;

namespace TungDiroot.Addin;

[Transaction(TransactionMode.Manual)]
public sealed class Command_ReOrdering : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var document = commandData.Application.ActiveUIDocument?.Document;
        if (document == null || !document.IsFamilyDocument)
        {
            TaskDialog.Show("TUNG_DIROOT — ReOrdering", "Hãy mở một family trong Family Editor trước khi dùng ReOrdering.");
            return Result.Cancelled;
        }
        ReOrderingWindow.ShowOrActivate(commandData.Application);
        return Result.Succeeded;
    }
}
