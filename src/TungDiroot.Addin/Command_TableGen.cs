using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.UI;

namespace TungDiroot.Addin;

[Transaction(TransactionMode.Manual)]
public sealed class Command_TableGen : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        TableGenWindow.ShowOrActivate(commandData.Application);
        return Result.Succeeded;
    }
}
