using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Parameters;
using Xunit;

namespace TungDiroot.Core.Tests.Parameters;

public sealed class ParameterAssignmentPlannerTests
{
    private static readonly Guid KnownGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly ParameterAssignmentPlanner Planner = new();

    [Fact]
    public void New_valid_definition_is_ready()
    {
        var plan = Assert.Single(Build(
            Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors"),
            Definition("Identity", "Asset_Code", KnownGuid)));

        Assert.Equal(ParameterPlanStatus.Ready, plan.Status);
        Assert.Equal(KnownGuid, plan.Definition!.Guid);
    }

    [Fact]
    public void Existing_compatible_category_superset_is_already_bound()
    {
        var plan = Assert.Single(Build(
            Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors"),
            Definition("Identity", "Asset_Code", KnownGuid),
            Existing("Asset_Code", KnownGuid, ParameterBindingKind.Instance, "OST_Doors", "OST_Windows")));

        Assert.Equal(ParameterPlanStatus.AlreadyBound, plan.Status);
    }

    [Fact]
    public void Same_name_with_different_guid_requires_review()
    {
        var requested = KnownGuid;
        var existing = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var plans = Build(
            Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors"),
            Definition("Identity", "Asset_Code", requested),
            Existing("Asset_Code", existing, ParameterBindingKind.Instance, "OST_Doors"));

        Assert.Equal(ParameterPlanStatus.ReviewRequired, Assert.Single(plans).Status);
        Assert.Contains("GUID", plans[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Existing_binding_kind_mismatch_requires_review()
    {
        var plan = Assert.Single(Build(
            Rule("Identity", "Asset_Code", ParameterBindingKind.Type, "PG_DATA", "OST_Doors"),
            Definition("Identity", "Asset_Code", KnownGuid),
            Existing("Asset_Code", KnownGuid, ParameterBindingKind.Instance, "OST_Doors")));

        Assert.Equal(ParameterPlanStatus.ReviewRequired, plan.Status);
        Assert.Contains("Instance", plan.Reason);
        Assert.Contains("Type", plan.Reason);
    }

    [Fact]
    public void Existing_binding_missing_a_requested_category_requires_review()
    {
        var plans = Build(
            Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors", "OST_Windows"),
            Definition("Identity", "Asset_Code", KnownGuid),
            Existing("Asset_Code", KnownGuid, ParameterBindingKind.Instance, "OST_Doors"));

        Assert.Equal(ParameterPlanStatus.ReviewRequired, Assert.Single(plans).Status);
        Assert.Contains("OST_Windows", plans[0].Reason);
    }

    [Fact]
    public void Missing_definition_is_reported()
    {
        var plan = Assert.Single(Planner.Build(
            new[] { Rule("Identity", "Missing", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors") },
            Array.Empty<SharedParameterDefinitionModel>(),
            Array.Empty<ExistingParameterBindingModel>(),
            Categories("OST_Doors"),
            Groups("PG_DATA")));

        Assert.Equal(ParameterPlanStatus.MissingDefinition, plan.Status);
    }

    [Fact]
    public void Unavailable_category_is_reported_with_reason()
    {
        var categories = new Dictionary<string, CategoryAvailability>(StringComparer.OrdinalIgnoreCase)
        {
            ["OST_Doors"] = new("OST_Doors", true, null),
            ["OST_Windows"] = new("OST_Windows", false, "Category does not allow bound parameters")
        };

        var plan = Assert.Single(Planner.Build(
            new[] { Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors", "OST_Windows") },
            new[] { Definition("Identity", "Asset_Code", KnownGuid) },
            Array.Empty<ExistingParameterBindingModel>(),
            categories,
            Groups("PG_DATA")));

        Assert.Equal(ParameterPlanStatus.InvalidCategory, plan.Status);
        Assert.Contains("OST_Windows", plan.Reason);
    }

    [Fact]
    public void Unsupported_revit_group_is_configuration_error()
    {
        var plan = Assert.Single(Planner.Build(
            new[] { Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_NOT_REAL", "OST_Doors") },
            new[] { Definition("Identity", "Asset_Code", KnownGuid) },
            Array.Empty<ExistingParameterBindingModel>(),
            Categories("OST_Doors"),
            Groups("PG_DATA")));

        Assert.Equal(ParameterPlanStatus.ConfigurationError, plan.Status);
        Assert.Contains("PG_NOT_REAL", plan.Reason);
    }

    [Fact]
    public void Row_issue_is_configuration_error_without_definition_lookup()
    {
        var invalid = new ParameterRuleDraft(
            4, true, "Identity", "Asset_Code", null, "PG_DATA", Array.Empty<string>(),
            new[] { new ParameterConfigurationIssue("BINDING_INVALID", "BINDING must be INSTANCE or TYPE") });

        var plan = Assert.Single(Planner.Build(
            new[] { invalid }, Array.Empty<SharedParameterDefinitionModel>(), Array.Empty<ExistingParameterBindingModel>(),
            new Dictionary<string, CategoryAvailability>(), Groups("PG_DATA")));

        Assert.Equal(ParameterPlanStatus.ConfigurationError, plan.Status);
        Assert.Contains("BINDING", plan.Reason);
    }

    [Fact]
    public void Duplicate_enabled_rules_are_configuration_errors()
    {
        var rules = new[]
        {
            Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", new[] { "OST_Doors" }, row: 2),
            Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", new[] { "OST_Doors" }, row: 8)
        };

        var plans = Planner.Build(rules, new[] { Definition("Identity", "Asset_Code", KnownGuid) },
            Array.Empty<ExistingParameterBindingModel>(), Categories("OST_Doors"), Groups("PG_DATA"));

        Assert.Equal(new[] { ParameterPlanStatus.ConfigurationError, ParameterPlanStatus.ConfigurationError }, plans.Select(x => x.Status));
        Assert.All(plans, plan => Assert.Contains("trùng", plan.Reason, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Same_parameter_name_from_different_groups_is_never_ready()
    {
        var otherGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var rules = new[]
        {
            Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", new[] { "OST_Doors" }, row: 2),
            Rule("Product", "Asset_Code", ParameterBindingKind.Type, "PG_DATA", new[] { "OST_Windows" }, row: 3)
        };

        var plans = Planner.Build(rules,
            new[] { Definition("Identity", "Asset_Code", KnownGuid), Definition("Product", "Asset_Code", otherGuid) },
            Array.Empty<ExistingParameterBindingModel>(), Categories("OST_Doors", "OST_Windows"), Groups("PG_DATA"));

        Assert.All(plans, plan => Assert.Equal(ParameterPlanStatus.ConfigurationError, plan.Status));
        Assert.All(plans, plan => Assert.Contains("tên", plan.Reason, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Disabled_rules_are_omitted()
    {
        var disabled = new ParameterRuleDraft(
            2, false, "Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", new[] { "OST_Doors" },
            Array.Empty<ParameterConfigurationIssue>());

        var plans = Planner.Build(new[] { disabled }, new[] { Definition("Identity", "Asset_Code", KnownGuid) },
            Array.Empty<ExistingParameterBindingModel>(), Categories("OST_Doors"), Groups("PG_DATA"));

        Assert.Empty(plans);
    }

    private static IReadOnlyList<ParameterAssignmentPlan> Build(
        ParameterRuleDraft rule,
        SharedParameterDefinitionModel definition,
        ExistingParameterBindingModel? existing = null) =>
        Planner.Build(new[] { rule }, new[] { definition },
            existing == null ? Array.Empty<ExistingParameterBindingModel>() : new[] { existing },
            Categories(rule.CategoryKeys.ToArray()), Groups(rule.RevitGroupKey));

    private static ParameterRuleDraft Rule(
        string group,
        string name,
        ParameterBindingKind kind,
        string revitGroup,
        params string[] categories) => Rule(group, name, kind, revitGroup, categories, 2);

    private static ParameterRuleDraft Rule(
        string group,
        string name,
        ParameterBindingKind kind,
        string revitGroup,
        IReadOnlyList<string> categories,
        int row) =>
        new(row, true, group, name, kind, revitGroup, categories, Array.Empty<ParameterConfigurationIssue>());

    private static SharedParameterDefinitionModel Definition(string group, string name, Guid guid) => new(group, name, guid);

    private static ExistingParameterBindingModel Existing(
        string name,
        Guid guid,
        ParameterBindingKind kind,
        params string[] categories) => new(guid, name, kind, categories);

    private static IReadOnlyDictionary<string, CategoryAvailability> Categories(params string[] keys) =>
        keys.Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(key => key, key => new CategoryAvailability(key, true, null), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyCollection<string> Groups(params string[] keys) => keys;
}
