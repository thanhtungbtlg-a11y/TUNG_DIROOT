using System.Collections.Generic;

namespace TungDiroot.Core.Filter;

public enum FilterScope
{
    WholeModel,
    ActiveView,
    CurrentSelection
}

public enum FilterJoin
{
    And,
    Or
}

public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    GreaterThan,
    LessThan,
    IsEmpty,
    IsNotEmpty
}

public enum FilterValueKind
{
    String,
    Number,
    Integer,
    Boolean
}

public sealed record FilterValue(
    bool Exists,
    FilterValueKind Kind,
    string? Display,
    string? Internal)
{
    public static FilterValue Missing(FilterValueKind kind) => new(false, kind, null, null);
}

public sealed record FilterRule(
    string ParameterStableId,
    FilterOperator Operator,
    string? Operand,
    FilterValueKind Kind);

public sealed record FilterElementSnapshot(
    string UniqueId,
    long ElementId,
    long CategoryId,
    string Category,
    string Family,
    string TypeName,
    string? Level,
    IReadOnlyDictionary<string, FilterValue> Values);

public sealed record FilterQuery(
    FilterScope Scope,
    IReadOnlyList<long> CategoryIds,
    IReadOnlyList<string> FamilyNames,
    IReadOnlyList<string> TypeNames,
    IReadOnlyList<FilterRule> Rules,
    FilterJoin Join);

public sealed record FilterMatch(
    FilterElementSnapshot Element,
    IReadOnlyList<FilterRule> MatchedRules);

public sealed record FilterCategoryDescriptor(long Id, string Name);

public sealed record FilterParameterDescriptor(
    string StableId,
    string Name,
    FilterValueKind Kind,
    bool IsType);

public sealed record FilterCatalog(
    IReadOnlyList<FilterCategoryDescriptor> Categories,
    IReadOnlyList<string> FamilyNames,
    IReadOnlyList<string> TypeNames,
    IReadOnlyList<FilterParameterDescriptor> Parameters);
