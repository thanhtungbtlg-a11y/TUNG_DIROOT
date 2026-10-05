using TungDiroot.Core.Import;

namespace TungDiroot.Core.RevitPlanning;

public sealed record ViewCreationRequest(
    ImportMode Mode,
    TargetViewKind Kind,
    bool HasLegendSeed,
    string RequestedName);

public sealed record ViewCreationPlan(
    TargetViewKind Kind,
    string RequestedName,
    bool DuplicateLegendSeed,
    string? ErrorCode)
{
    public bool CanCreate => ErrorCode == null;
}
