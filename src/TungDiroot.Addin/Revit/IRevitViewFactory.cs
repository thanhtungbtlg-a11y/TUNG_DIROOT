using Autodesk.Revit.DB;
using TungDiroot.Core.RevitPlanning;

namespace TungDiroot.Addin.Revit;

public interface IRevitViewFactory
{
    View Create(Document document, ViewCreationPlan plan);
}

public interface IRevitViewCreationContext
{
    ElementId DraftingViewFamilyTypeId(Document document);

    View? LegendSeed(Document document);

    ElementId ScheduleCategoryId(Document document);
}
