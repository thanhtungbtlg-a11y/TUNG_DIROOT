using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.UI;

namespace TungDiroot.Addin;

[Transaction(TransactionMode.Manual)]
public sealed class Command_SheetTools : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var document = commandData.Application.ActiveUIDocument?.Document;
        if (document == null || document.IsFamilyDocument)
        {
            TaskDialog.Show("TUNG_DIROOT — SheetLink / SheetGen", "Hãy mở một Revit project trước khi dùng công cụ này.");
            return Result.Cancelled;
        }
        SheetToolsWindow.ShowOrActivate(commandData.Application);
        return Result.Succeeded;
    }
}
