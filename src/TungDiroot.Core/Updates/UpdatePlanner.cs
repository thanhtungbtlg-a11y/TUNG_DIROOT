using System;
using TungDiroot.Core.Import;

namespace TungDiroot.Core.Updates;

public static class UpdatePlanner
{
    public static UpdatePlan Create(UpdatePlanInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (!input.SourceChanged) return UpdatePlan.NoChange(input.After);
        if (input.ImportMode != ImportMode.Image && input.RequestedBehavior == UpdateBehavior.DataOnly && input.StructureCompatible)
            return new UpdatePlan(UpdateAction.DataOnly, input.After, CellValueDiff.Create(input.Before, input.After), null);
        var reason = input.ImportMode == ImportMode.Image ? "IMAGE_REPLACE_REQUIRED" : input.StructureReasonCode;
        return new UpdatePlan(UpdateAction.Recreate, input.After, Array.Empty<CellValueChange>(), reason);
    }
}
