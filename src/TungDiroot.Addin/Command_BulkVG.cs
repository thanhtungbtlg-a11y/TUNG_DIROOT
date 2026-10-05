using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.UI;

namespace TungDiroot.Addin;

[Transaction(TransactionMode.Manual)]
public sealed class Command_BulkVG : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        BulkVgWindow.ShowOrActivate(commandData.Application);
        return Result.Succeeded;
    }
}
