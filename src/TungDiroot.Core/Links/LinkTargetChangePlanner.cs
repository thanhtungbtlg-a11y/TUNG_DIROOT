using TungDiroot.Core.Import;

namespace TungDiroot.Core.Links;

public sealed record LinkTargetViewOption(string UniqueId, string Name, TargetViewKind Kind);

public sealed record LinkTargetChangePlan(
    bool CanExecute,
    string? ErrorCode,
    bool UseTransactionGroup,
    bool RecreateAtTarget,
    bool RemoveOldLinkedElements);

public static class LinkTargetChangePlanner
{
    public static LinkTargetChangePlan Create(TargetViewKind sourceKind, TargetViewKind targetKind, bool sameTarget)
    {
        if (sameTarget) return Reject("TARGET_VIEW_UNCHANGED");
        if (sourceKind == TargetViewKind.Schedule || targetKind == TargetViewKind.Schedule)
            return Reject("TARGET_VIEW_CHANGE_SCHEDULE_UNSUPPORTED");
        if (sourceKind != targetKind) return Reject("TARGET_VIEW_KIND_MISMATCH");
        return new LinkTargetChangePlan(true, null, true, true, true);
    }

    private static LinkTargetChangePlan Reject(string errorCode) => new(false, errorCode, false, false, false);
}
