using System;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Core.Import;
using TungDiroot.Core.RevitPlanning;

namespace TungDiroot.Addin.Revit;

public sealed class RevitViewFactory : IRevitViewFactory
{
    private readonly IRevitViewCreationContext _context;

    public RevitViewFactory(IRevitViewCreationContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public View Create(Document document, ViewCreationPlan plan)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (!plan.CanCreate) throw new InvalidOperationException($"Cannot create view: {plan.ErrorCode}");
        if (new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>()
            .Any(view => !view.IsTemplate && string.Equals(view.Name, plan.RequestedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("VIEW_NAME_ALREADY_EXISTS");
        }

        View view;
        switch (plan.Kind)
        {
            case TargetViewKind.Drafting:
                view = ViewDrafting.Create(document, _context.DraftingViewFamilyTypeId(document));
                break;
            case TargetViewKind.Legend:
                var seed = _context.LegendSeed(document) ?? throw new InvalidOperationException("LEGEND_SEED_REQUIRED");
                if (!seed.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                {
                    throw new InvalidOperationException("LEGEND_SEED_NOT_DUPLICABLE");
                }
                view = (View)document.GetElement(seed.Duplicate(ViewDuplicateOption.Duplicate));
                break;
            case TargetViewKind.Schedule:
                view = ViewSchedule.CreateSchedule(document, _context.ScheduleCategoryId(document));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.Kind, "Unsupported target view kind.");
        }

        view.Name = plan.RequestedName;
        return view;
    }
}
