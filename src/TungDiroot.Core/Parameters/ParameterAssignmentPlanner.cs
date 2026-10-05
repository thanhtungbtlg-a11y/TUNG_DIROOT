using System;
using System.Collections.Generic;
using System.Linq;

namespace TungDiroot.Core.Parameters;

public sealed class ParameterAssignmentPlanner
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    public IReadOnlyList<ParameterAssignmentPlan> Build(
        IReadOnlyList<ParameterRuleDraft> rules,
        IReadOnlyList<SharedParameterDefinitionModel> definitions,
        IReadOnlyList<ExistingParameterBindingModel> existingBindings,
        IReadOnlyDictionary<string, CategoryAvailability> categories,
        IReadOnlyCollection<string> supportedRevitGroups)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        if (existingBindings == null) throw new ArgumentNullException(nameof(existingBindings));
        if (categories == null) throw new ArgumentNullException(nameof(categories));
        if (supportedRevitGroups == null) throw new ArgumentNullException(nameof(supportedRevitGroups));

        var enabled = rules.Where(rule => rule.Enabled).ToArray();
        var duplicateKeys = new HashSet<string>(enabled
            .GroupBy(rule => DefinitionKey(rule.DefinitionGroup, rule.ParameterName), KeyComparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key), KeyComparer);
        var duplicateNames = new HashSet<string>(enabled
            .GroupBy(rule => rule.ParameterName.Trim(), KeyComparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key), KeyComparer);
        var supportedGroups = new HashSet<string>(supportedRevitGroups, KeyComparer);

        return enabled.Select(rule => BuildOne(
            rule,
            duplicateKeys.Contains(DefinitionKey(rule.DefinitionGroup, rule.ParameterName)),
            duplicateNames.Contains(rule.ParameterName.Trim()),
            definitions,
            existingBindings,
            categories,
            supportedGroups)).ToArray();
    }

    private static ParameterAssignmentPlan BuildOne(
        ParameterRuleDraft rule,
        bool isDuplicate,
        bool hasDuplicateName,
        IReadOnlyList<SharedParameterDefinitionModel> definitions,
        IReadOnlyList<ExistingParameterBindingModel> existingBindings,
        IReadOnlyDictionary<string, CategoryAvailability> categories,
        ISet<string> supportedGroups)
    {
        if (rule.Issues.Count > 0)
        {
            return Plan(rule, null, ParameterPlanStatus.ConfigurationError,
                string.Join("; ", rule.Issues.Select(issue => issue.Message)));
        }

        if (isDuplicate)
        {
            return Plan(rule, null, ParameterPlanStatus.ConfigurationError,
                $"Cấu hình trùng definition {rule.DefinitionGroup}/{rule.ParameterName}.");
        }

        if (hasDuplicateName)
        {
            return Plan(rule, null, ParameterPlanStatus.ConfigurationError,
                $"Tên parameter trùng trong nhiều dòng cấu hình: {rule.ParameterName}.");
        }

        var matches = definitions.Where(definition =>
            KeyComparer.Equals(definition.GroupName, rule.DefinitionGroup) &&
            KeyComparer.Equals(definition.Name, rule.ParameterName)).ToArray();
        if (matches.Length == 0)
        {
            return Plan(rule, null, ParameterPlanStatus.MissingDefinition,
                $"Không tìm thấy definition {rule.DefinitionGroup}/{rule.ParameterName}.");
        }

        if (matches.Length > 1)
        {
            return Plan(rule, null, ParameterPlanStatus.ConfigurationError,
                $"Shared Parameter có nhiều definition trùng {rule.DefinitionGroup}/{rule.ParameterName}.");
        }

        var definition = matches[0];
        if (!supportedGroups.Contains(rule.RevitGroupKey))
        {
            return Plan(rule, definition, ParameterPlanStatus.ConfigurationError,
                $"Revit group không được hỗ trợ: {rule.RevitGroupKey}.");
        }

        var invalidCategories = rule.CategoryKeys
            .Select(key => FindCategory(categories, key) ?? new CategoryAvailability(key, false, "Không tồn tại trong document"))
            .Where(category => !category.IsAvailable)
            .ToArray();
        if (invalidCategories.Length > 0)
        {
            var details = string.Join("; ", invalidCategories.Select(category =>
                category.Key + (string.IsNullOrWhiteSpace(category.Reason) ? string.Empty : $" ({category.Reason})")));
            return Plan(rule, definition, ParameterPlanStatus.InvalidCategory, "Category không hợp lệ: " + details);
        }

        var byGuid = existingBindings.FirstOrDefault(binding => binding.Guid == definition.Guid);
        if (byGuid != null)
        {
            if (!KeyComparer.Equals(byGuid.Name, definition.Name))
            {
                return Plan(rule, definition, ParameterPlanStatus.ReviewRequired,
                    $"GUID {definition.Guid} đã tồn tại với tên {byGuid.Name}.");
            }

            if (byGuid.BindingKind != rule.BindingKind)
            {
                return Plan(rule, definition, ParameterPlanStatus.ReviewRequired,
                    $"Binding hiện tại là {byGuid.BindingKind}, cấu hình yêu cầu {rule.BindingKind}.");
            }

            var existingCategories = new HashSet<string>(byGuid.CategoryKeys, KeyComparer);
            var missing = rule.CategoryKeys.Where(key => !existingCategories.Contains(key)).ToArray();
            if (missing.Length > 0)
            {
                return Plan(rule, definition, ParameterPlanStatus.ReviewRequired,
                    "Binding hiện tại thiếu category: " + string.Join(", ", missing) + ".");
            }

            return Plan(rule, definition, ParameterPlanStatus.AlreadyBound, "Parameter đã được gán đúng cấu hình.");
        }

        var byName = existingBindings.FirstOrDefault(binding => KeyComparer.Equals(binding.Name, definition.Name));
        if (byName != null)
        {
            return Plan(rule, definition, ParameterPlanStatus.ReviewRequired,
                $"Tên {definition.Name} đã tồn tại với GUID khác: {byName.Guid}.");
        }

        return Plan(rule, definition, ParameterPlanStatus.Ready, "Sẵn sàng gán mới.");
    }

    private static CategoryAvailability? FindCategory(
        IReadOnlyDictionary<string, CategoryAvailability> categories,
        string key)
    {
        if (categories.TryGetValue(key, out var direct)) return direct;
        foreach (var pair in categories)
            if (KeyComparer.Equals(pair.Key, key)) return pair.Value;
        return null;
    }

    private static string DefinitionKey(string group, string name) => group.Trim() + "\u001f" + name.Trim();

    private static ParameterAssignmentPlan Plan(
        ParameterRuleDraft rule,
        SharedParameterDefinitionModel? definition,
        ParameterPlanStatus status,
        string reason) => new(rule, definition, status, reason);
}
