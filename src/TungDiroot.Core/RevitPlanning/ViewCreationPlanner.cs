using System;
using TungDiroot.Core.Import;

namespace TungDiroot.Core.RevitPlanning;

public static class ViewCreationPlanner
{
    public static ViewCreationPlan Plan(ViewCreationRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var normalizedName = request.RequestedName?.Trim() ?? string.Empty;
        string? errorCode = null;
        if (normalizedName.Length == 0)
        {
            errorCode = "VIEW_NAME_REQUIRED";
        }
        else if (request.Mode == ImportMode.Image && request.Kind == TargetViewKind.Schedule)
        {
            errorCode = "IMAGE_SCHEDULE_UNSUPPORTED";
        }
        else if (request.Kind == TargetViewKind.Legend && !request.HasLegendSeed)
        {
            errorCode = "LEGEND_SEED_REQUIRED";
        }

        return new ViewCreationPlan(request.Kind, normalizedName, request.Kind == TargetViewKind.Legend, errorCode);
    }
}
