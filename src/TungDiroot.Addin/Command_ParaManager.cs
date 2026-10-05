using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.UI;

namespace TungDiroot.Addin;

[Transaction(TransactionMode.Manual)]
public sealed class Command_ParaManager : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (commandData.Application.ActiveUIDocument?.Document == null)
        {
            TaskDialog.Show("TUNG_DIROOT — ParaManager", "Hãy mở một Revit project hoặc family trước khi dùng ParaManager.");
            return Result.Cancelled;
        }
        ParaManagerWindow.ShowOrActivate(commandData.Application);
        return Result.Succeeded;
    }
}
