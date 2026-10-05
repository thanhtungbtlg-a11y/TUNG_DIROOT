using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TungDiroot.Core.Filter;

public static class FilterRuleEvaluator
{
    public static IReadOnlyList<string> Validate(FilterQuery query)
    {
        if (query == null) return new[] { "FILTER_QUERY_REQUIRED" };
        var errors = new List<string>();
        if (query.CategoryIds == null) errors.Add("FILTER_CATEGORIES_REQUIRED");
        if (query.FamilyNames == null) errors.Add("FILTER_FAMILIES_REQUIRED");
        if (query.TypeNames == null) errors.Add("FILTER_TYPES_REQUIRED");
        if (query.Rules == null)
        {
            errors.Add("FILTER_RULES_REQUIRED");
            return errors;
        }

        for (var index = 0; index < query.Rules.Count; index++)
        {
            var error = ValidateRule(query.Rules[index]);
            if (error != null) errors.Add($"RULE_{index + 1}_{error}");
        }
        return errors;
    }

    public static IReadOnlyList<FilterMatch> Evaluate(
        IEnumerable<FilterElementSnapshot> snapshots,
        FilterQuery query)
    {
        if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
        var errors = Validate(query);
        if (errors.Count > 0) throw new ArgumentException(string.Join(";", errors), nameof(query));

        var categories = new HashSet<long>(query.CategoryIds);
        var families = new HashSet<string>(query.FamilyNames.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
        var types = new HashSet<string>(query.TypeNames.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
        var results = new List<FilterMatch>();
        foreach (var snapshot in snapshots)
        {
            if (snapshot == null || !PassesFixedFilters(snapshot, categories, families, types)) continue;
            var matched = query.Rules.Where(rule => Matches(snapshot, rule)).ToArray();
            var isMatch = query.Rules.Count == 0 ||
                (query.Join == FilterJoin.And ? matched.Length == query.Rules.Count : matched.Length > 0);
            if (isMatch) results.Add(new FilterMatch(snapshot, matched));
        }
        return results;
    }

    public static bool Matches(FilterElementSnapshot snapshot, FilterRule rule)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        var validationError = ValidateRule(rule);
        if (validationError != null) throw new ArgumentException(validationError, nameof(rule));

        var value = Resolve(snapshot.Values, rule.ParameterStableId);
        if (value == null || !value.Exists || value.Kind != rule.Kind) return false;
        var internalValue = value.Internal ?? string.Empty;
        if (rule.Operator == FilterOperator.IsEmpty) return internalValue.Length == 0;
        if (rule.Operator == FilterOperator.IsNotEmpty) return internalValue.Length > 0;

        return rule.Kind switch
        {
            FilterValueKind.String => MatchString(internalValue, rule.Operator, rule.Operand!),
            FilterValueKind.Number => MatchNumber(internalValue, rule.Operator, rule.Operand!),
            FilterValueKind.Integer => MatchInteger(internalValue, rule.Operator, rule.Operand!),
            FilterValueKind.Boolean => MatchBoolean(internalValue, rule.Operator, rule.Operand!),
            _ => false
        };
    }

    private static bool PassesFixedFilters(
        FilterElementSnapshot snapshot,
        ISet<long> categories,
        ISet<string> families,
        ISet<string> types) =>
        (categories.Count == 0 || categories.Contains(snapshot.CategoryId)) &&
        (families.Count == 0 || families.Contains(snapshot.Family ?? string.Empty)) &&
        (types.Count == 0 || types.Contains(snapshot.TypeName ?? string.Empty));

    private static FilterValue? Resolve(IReadOnlyDictionary<string, FilterValue> values, string stableId)
    {
        if (values == null) return null;
        if (values.TryGetValue(stableId, out var direct)) return direct;
        foreach (var pair in values)
            if (string.Equals(pair.Key, stableId, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return null;
    }

    private static string? ValidateRule(FilterRule? rule)
    {
        if (rule == null) return "REQUIRED";
        if (string.IsNullOrWhiteSpace(rule.ParameterStableId)) return "PARAMETER_REQUIRED";
        if (rule.Operator is FilterOperator.IsEmpty or FilterOperator.IsNotEmpty) return null;
        if (string.IsNullOrWhiteSpace(rule.Operand)) return "OPERAND_REQUIRED";

        if (rule.Kind == FilterValueKind.String)
            return rule.Operator is FilterOperator.Equals or FilterOperator.NotEquals or FilterOperator.Contains or FilterOperator.NotContains
                ? null : "OPERATOR_NOT_SUPPORTED";
        if (rule.Kind is FilterValueKind.Number or FilterValueKind.Integer)
        {
            if (rule.Operator is not (FilterOperator.Equals or FilterOperator.NotEquals or FilterOperator.GreaterThan or FilterOperator.LessThan))
                return "OPERATOR_NOT_SUPPORTED";
            return rule.Kind == FilterValueKind.Number
                ? TryDouble(rule.Operand!, out _) ? null : "OPERAND_INVALID"
                : long.TryParse(rule.Operand, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : "OPERAND_INVALID";
        }
        if (rule.Kind == FilterValueKind.Boolean)
        {
            if (rule.Operator is not (FilterOperator.Equals or FilterOperator.NotEquals)) return "OPERATOR_NOT_SUPPORTED";
            return TryBoolean(rule.Operand!, out _) ? null : "OPERAND_INVALID";
        }
        return "KIND_NOT_SUPPORTED";
    }

    private static bool MatchString(string value, FilterOperator op, string operand) => op switch
    {
        FilterOperator.Equals => string.Equals(value, operand, StringComparison.OrdinalIgnoreCase),
        FilterOperator.NotEquals => !string.Equals(value, operand, StringComparison.OrdinalIgnoreCase),
        FilterOperator.Contains => value.IndexOf(operand, StringComparison.OrdinalIgnoreCase) >= 0,
        FilterOperator.NotContains => value.IndexOf(operand, StringComparison.OrdinalIgnoreCase) < 0,
        _ => false
    };

    private static bool MatchNumber(string value, FilterOperator op, string operand)
    {
        if (!TryDouble(value, out var left) || !TryDouble(operand, out var right)) return false;
        return Compare(left, right, op);
    }

    private static bool MatchInteger(string value, FilterOperator op, string operand)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var left) ||
            !long.TryParse(operand, NumberStyles.Integer, CultureInfo.InvariantCulture, out var right)) return false;
        return Compare(left, right, op);
    }

    private static bool MatchBoolean(string value, FilterOperator op, string operand)
    {
        if (!TryBoolean(value, out var left) || !TryBoolean(operand, out var right)) return false;
        return op == FilterOperator.Equals ? left == right : left != right;
    }

    private static bool Compare<T>(T left, T right, FilterOperator op) where T : IComparable<T>
    {
        var comparison = left.CompareTo(right);
        return op switch
        {
            FilterOperator.Equals => comparison == 0,
            FilterOperator.NotEquals => comparison != 0,
            FilterOperator.GreaterThan => comparison > 0,
            FilterOperator.LessThan => comparison < 0,
            _ => false
        };
    }

    private static bool TryDouble(string value, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
        !double.IsNaN(parsed) && !double.IsInfinity(parsed);

    private static bool TryBoolean(string value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed)) return true;
        if (value == "1") { parsed = true; return true; }
        if (value == "0") { parsed = false; return true; }
        parsed = false;
        return false;
    }
}
