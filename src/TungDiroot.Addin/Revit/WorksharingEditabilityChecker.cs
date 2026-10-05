using System;
using Autodesk.Revit.DB;

namespace TungDiroot.Addin.Revit;

public sealed record EditabilityResult(bool CanEdit, string? ReasonCode);

public static class WorksharingEditabilityChecker
{
    public static EditabilityResult Check(Document document, ElementId elementId)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (elementId == null || elementId == ElementId.InvalidElementId) return new EditabilityResult(false, "VIEW_DELETED");
        if (document.GetElement(elementId) == null) return new EditabilityResult(false, "VIEW_DELETED");
        if (!document.IsWorkshared) return new EditabilityResult(true, null);
        var status = WorksharingUtils.GetCheckoutStatus(document, elementId);
        return status == CheckoutStatus.OwnedByOtherUser
            ? new EditabilityResult(false, "VIEW_NOT_EDITABLE")
            : new EditabilityResult(true, null);
    }
}
