using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.UI;

namespace TungDiroot.Addin;

[Transaction(TransactionMode.Manual)]
public sealed class Command_ParameterLoader : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var document = commandData.Application.ActiveUIDocument?.Document;
        if (document == null)
        {
            TaskDialog.Show("TUNG_DIROOT — Parameter Loader", "Hãy mở một Revit project trước khi dùng Parameter Loader.");
            return Result.Cancelled;
        }
        if (document.IsFamilyDocument)
        {
            TaskDialog.Show("TUNG_DIROOT — Parameter Loader", "Parameter Loader chỉ hỗ trợ Revit project, không chạy trong Family Editor.");
            return Result.Cancelled;
        }
        ParameterLoaderWindow.ShowOrActivate(commandData.Application);
        return Result.Succeeded;
    }
}
