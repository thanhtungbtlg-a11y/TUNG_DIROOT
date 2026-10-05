using System;
using System.Collections.Generic;

namespace TungDiroot.Core.Parameters;

public enum ParameterBindingKind
{
    Instance,
    Type
}

public enum ParameterPlanStatus
{
    Ready,
    AlreadyBound,
    ReviewRequired,
    MissingDefinition,
    InvalidCategory,
    ConfigurationError,
    AssignmentFailed
}

public sealed record ParameterConfigurationIssue(string Code, string Message);

public sealed record ParameterRuleDraft(
    int ExcelRow,
    bool Enabled,
    string DefinitionGroup,
    string ParameterName,
    ParameterBindingKind? BindingKind,
    string RevitGroupKey,
    IReadOnlyList<string> CategoryKeys,
    IReadOnlyList<ParameterConfigurationIssue> Issues);

public sealed record SharedParameterDefinitionModel(string GroupName, string Name, Guid Guid);

public sealed record ExistingParameterBindingModel(
    Guid Guid,
    string Name,
    ParameterBindingKind BindingKind,
    IReadOnlyList<string> CategoryKeys);

public sealed record CategoryAvailability(string Key, bool IsAvailable, string? Reason);

public sealed record ParameterAssignmentPlan(
    ParameterRuleDraft Rule,
    SharedParameterDefinitionModel? Definition,
    ParameterPlanStatus Status,
    string Reason);

public enum ParameterAssignmentOutcome
{
    Assigned,
    Failed
}

public sealed record ParameterAssignmentResult(
    ParameterAssignmentPlan Plan,
    ParameterAssignmentOutcome Outcome,
    string? Error)
{
    public static ParameterAssignmentResult Assigned(ParameterAssignmentPlan plan) =>
        new(plan, ParameterAssignmentOutcome.Assigned, null);

    public static ParameterAssignmentResult Failed(ParameterAssignmentPlan plan, string error) =>
        new(plan, ParameterAssignmentOutcome.Failed, error);
}
