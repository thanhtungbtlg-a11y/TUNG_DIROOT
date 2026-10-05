using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TungDiroot.Core.Filter;
using Xunit;

namespace TungDiroot.Core.Tests.Filter;

public sealed class FilterRuleEvaluatorTests
{
    [Fact]
    public void Missing_value_is_not_the_same_as_empty_value()
    {
        var rule = new FilterRule("Comments", FilterOperator.IsEmpty, null, FilterValueKind.String);

        Assert.False(FilterRuleEvaluator.Matches(Element(), rule));
        Assert.True(FilterRuleEvaluator.Matches(Element(Value("Comments", "", "")), rule));
    }

    [Fact]
    public void Missing_value_never_satisfies_negative_string_operators()
    {
        var snapshot = Element();

        Assert.False(FilterRuleEvaluator.Matches(snapshot,
            new FilterRule("Comments", FilterOperator.NotEquals, "x", FilterValueKind.String)));
        Assert.False(FilterRuleEvaluator.Matches(snapshot,
            new FilterRule("Comments", FilterOperator.NotContains, "x", FilterValueKind.String)));
        Assert.False(FilterRuleEvaluator.Matches(snapshot,
            new FilterRule("Comments", FilterOperator.IsNotEmpty, null, FilterValueKind.String)));
    }

    [Fact]
    public void Numeric_comparison_uses_invariant_internal_value_not_localized_display()
    {
        var rule = new FilterRule("Length", FilterOperator.GreaterThan, "3.5", FilterValueKind.Number);
        var snapshot = Element(new KeyValuePair<string, FilterValue>("Length",
            new FilterValue(true, FilterValueKind.Number, "4,00 m", "4")));

        Assert.True(FilterRuleEvaluator.Matches(snapshot, rule));
    }

    [Theory]
    [InlineData(FilterOperator.Equals, "Pump", true)]
    [InlineData(FilterOperator.Equals, "pump", true)]
    [InlineData(FilterOperator.NotEquals, "Fan", true)]
    [InlineData(FilterOperator.Contains, "um", true)]
    [InlineData(FilterOperator.NotContains, "duct", true)]
    public void String_operators_are_ordinal_case_insensitive(FilterOperator op, string operand, bool expected)
    {
        var snapshot = Element(Value("Name", "Pump", "Pump"));
        Assert.Equal(expected, FilterRuleEvaluator.Matches(snapshot,
            new FilterRule("Name", op, operand, FilterValueKind.String)));
    }

    [Fact]
    public void And_and_or_are_deterministic_with_mixed_kinds_and_missing_values()
    {
        var elements = new[]
        {
            Element(Value("Name", "Pump A", "Pump A"), Number("Flow", 4), elementId: 1),
            Element(Value("Name", "Fan A", "Fan A"), Number("Flow", 8), elementId: 2),
            Element(Value("Name", "Pump B", "Pump B"), elementId: 3)
        };
        var rules = new[]
        {
            new FilterRule("Name", FilterOperator.Contains, "pump", FilterValueKind.String),
            new FilterRule("Flow", FilterOperator.GreaterThan, "5", FilterValueKind.Number)
        };

        var andMatches = FilterRuleEvaluator.Evaluate(elements, Query(rules, FilterJoin.And));
        var orMatches = FilterRuleEvaluator.Evaluate(elements, Query(rules, FilterJoin.Or));

        Assert.Empty(andMatches);
        Assert.Equal(new long[] { 1, 2, 3 }, orMatches.Select(match => match.Element.ElementId));
    }

    [Fact]
    public void Zero_rules_match_elements_that_pass_category_family_and_type_filters()
    {
        var elements = new[]
        {
            Element(categoryId: 10, family: "Pipe", type: "A"),
            Element(categoryId: 20, family: "Duct", type: "B")
        };
        var query = new FilterQuery(FilterScope.WholeModel, new long[] { 10 }, new[] { "Pipe" }, new[] { "A" },
            Array.Empty<FilterRule>(), FilterJoin.And);

        Assert.Equal(1, Assert.Single(FilterRuleEvaluator.Evaluate(elements, query)).Element.ElementId);
    }

    [Theory]
    [InlineData(FilterValueKind.String, FilterOperator.GreaterThan, "1")]
    [InlineData(FilterValueKind.Number, FilterOperator.Contains, "1")]
    [InlineData(FilterValueKind.Number, FilterOperator.GreaterThan, "not-a-number")]
    [InlineData(FilterValueKind.Boolean, FilterOperator.Equals, "maybe")]
    public void Invalid_rule_combinations_are_reported_before_evaluation(
        FilterValueKind kind,
        FilterOperator op,
        string operand)
    {
        var query = Query(new[] { new FilterRule("P", op, operand, kind) }, FilterJoin.And);

        var errors = FilterRuleEvaluator.Validate(query);

        Assert.NotEmpty(errors);
        Assert.Throws<ArgumentException>(() => FilterRuleEvaluator.Evaluate(new[] { Element() }, query));
    }

    [Fact]
    public void Null_display_text_does_not_throw()
    {
        var value = new FilterValue(true, FilterValueKind.String, null, "abc");
        var snapshot = Element(new KeyValuePair<string, FilterValue>("P", value));

        Assert.True(FilterRuleEvaluator.Matches(snapshot,
            new FilterRule("P", FilterOperator.Contains, "b", FilterValueKind.String)));
    }

    [Fact]
    public void Ten_thousand_snapshots_produce_stable_order_and_result()
    {
        var elements = Enumerable.Range(0, 10_000)
            .Select(index => Element(Number("N", index), elementId: index + 1))
            .ToArray();
        var query = Query(new[] { new FilterRule("N", FilterOperator.GreaterThan, "9990", FilterValueKind.Number) }, FilterJoin.And);

        var matches = FilterRuleEvaluator.Evaluate(elements, query);

        Assert.Equal(9, matches.Count);
        Assert.Equal(9_992, matches[0].Element.ElementId);
        Assert.Equal(10_000, matches[^1].Element.ElementId);
    }

    private static FilterQuery Query(IReadOnlyList<FilterRule> rules, FilterJoin join) =>
        new(FilterScope.WholeModel, Array.Empty<long>(), Array.Empty<string>(), Array.Empty<string>(), rules, join);

    private static KeyValuePair<string, FilterValue> Value(string key, string? display, string internalValue) =>
        new(key, new FilterValue(true, FilterValueKind.String, display, internalValue));

    private static KeyValuePair<string, FilterValue> Number(string key, double value) =>
        new(key, new FilterValue(true, FilterValueKind.Number,
            value.ToString(CultureInfo.CurrentCulture), value.ToString("R", CultureInfo.InvariantCulture)));

    private static FilterElementSnapshot Element(
        KeyValuePair<string, FilterValue>? first = null,
        KeyValuePair<string, FilterValue>? second = null,
        long elementId = 1,
        long categoryId = 10,
        string family = "Pipe",
        string type = "A")
    {
        var values = new Dictionary<string, FilterValue>(StringComparer.Ordinal);
        if (first.HasValue) values[first.Value.Key] = first.Value.Value;
        if (second.HasValue) values[second.Value.Key] = second.Value.Value;
        return new FilterElementSnapshot("element-" + elementId, elementId, categoryId, "Pipes", family, type, "L1", values);
    }
}
