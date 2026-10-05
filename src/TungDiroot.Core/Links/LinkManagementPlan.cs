namespace TungDiroot.Core.Links;

public enum LinkManagementAction
{
    OpenView,
    OpenSource,
    OpenSourceFolder,
    UnlinkKeepView,
    DeleteLinkAndView
}

public sealed record LinkManagementPlan(
    bool CanExecute,
    string? ErrorCode,
    bool RequiresTransaction,
    bool RemoveRegistryRecord,
    bool ClearViewMarker,
    bool DeleteTargetView,
    bool DeleteLinkedElements);

public static class LinkManagementPlanner
{
    public static LinkManagementPlan Create(LinkManagementAction action, bool viewExists, bool sourceExists)
    {
        return action switch
        {
            LinkManagementAction.OpenView => viewExists
                ? Navigate()
                : Reject("VIEW_DELETED"),
            LinkManagementAction.OpenSource or LinkManagementAction.OpenSourceFolder => sourceExists
                ? Navigate()
                : Reject("FILE_MISSING"),
            LinkManagementAction.UnlinkKeepView => new LinkManagementPlan(
                true, null, true, true, viewExists, false, false),
            LinkManagementAction.DeleteLinkAndView => new LinkManagementPlan(
                true, null, true, true, false, viewExists, false),
            _ => Reject("ACTION_NOT_SUPPORTED")
        };
    }

    private static LinkManagementPlan Navigate() => new(true, null, false, false, false, false, false);

    private static LinkManagementPlan Reject(string errorCode) => new(false, errorCode, false, false, false, false, false);
}
