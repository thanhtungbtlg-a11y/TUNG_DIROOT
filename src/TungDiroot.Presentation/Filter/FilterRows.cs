using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Filter;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Presentation.Filter;

public sealed class FilterCategoryRow : BindableBase
{
    private readonly Action _changed;
    private bool _isSelected;

    public FilterCategoryRow(FilterCategoryDescriptor descriptor, Action changed)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    public FilterCategoryDescriptor Descriptor { get; }
    public long Id => Descriptor.Id;
    public string Name => Descriptor.Name;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) _changed(); }
    }
}

public sealed class FilterNameRow : BindableBase
{
    private readonly Action _changed;
    private bool _isSelected;

    public FilterNameRow(string name, Action changed)
    {
        Name = name ?? string.Empty;
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    public string Name { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) _changed(); }
    }
}

public sealed class FilterRuleRow : BindableBase
{
    private static readonly FilterOperator[] StringOperators =
    {
        FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.NotContains,
        FilterOperator.IsEmpty, FilterOperator.IsNotEmpty
    };
    private static readonly FilterOperator[] NumberOperators =
    {
        FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.LessThan,
        FilterOperator.IsEmpty, FilterOperator.IsNotEmpty
    };
    private static readonly FilterOperator[] BooleanOperators =
    {
        FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.IsEmpty, FilterOperator.IsNotEmpty
    };

    private readonly Action _changed;
    private FilterParameterDescriptor? _selectedParameter;
    private FilterOperator _selectedOperator = FilterOperator.Equals;
    private string _operand = string.Empty;

    public FilterRuleRow(Action changed) => _changed = changed ?? throw new ArgumentNullException(nameof(changed));

    public FilterParameterDescriptor? SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (!SetProperty(ref _selectedParameter, value)) return;
            RaisePropertyChanged(nameof(AvailableOperators));
            RaisePropertyChanged(nameof(AvailableOperatorOptions));
            if (!AvailableOperators.Contains(SelectedOperator)) SelectedOperator = AvailableOperators[0];
            _changed();
        }
    }

    public IReadOnlyList<FilterOperator> AvailableOperators => SelectedParameter?.Kind switch
    {
        FilterValueKind.Number or FilterValueKind.Integer => NumberOperators,
        FilterValueKind.Boolean => BooleanOperators,
        _ => StringOperators
    };

    public IReadOnlyList<FilterOperatorOption> AvailableOperatorOptions =>
        AvailableOperators.Select(value => new FilterOperatorOption(value, OperatorLabel(value))).ToArray();

    public FilterOperator SelectedOperator
    {
        get => _selectedOperator;
        set
        {
            if (!SetProperty(ref _selectedOperator, value)) return;
            RaisePropertyChanged(nameof(RequiresOperand));
            _changed();
        }
    }

    public string Operand
    {
        get => _operand;
        set { if (SetProperty(ref _operand, value ?? string.Empty)) _changed(); }
    }

    public bool RequiresOperand => SelectedOperator is not (FilterOperator.IsEmpty or FilterOperator.IsNotEmpty);

    public FilterRule? ToRule() => SelectedParameter == null
        ? null
        : new FilterRule(
            SelectedParameter.StableId,
            SelectedOperator,
            RequiresOperand ? Operand : null,
            SelectedParameter.Kind);

    private static string OperatorLabel(FilterOperator value) => value switch
    {
        FilterOperator.Equals => "Bằng",
        FilterOperator.NotEquals => "Khác",
        FilterOperator.Contains => "Chứa",
        FilterOperator.NotContains => "Không chứa",
        FilterOperator.GreaterThan => "Lớn hơn",
        FilterOperator.LessThan => "Nhỏ hơn",
        FilterOperator.IsEmpty => "Đang trống",
        FilterOperator.IsNotEmpty => "Không trống",
        _ => value.ToString()
    };
}

public sealed record FilterOperatorOption(FilterOperator Value, string Label);

public sealed class FilterResultRow : BindableBase
{
    private readonly Action _changed;
    private bool _isSelected;

    public FilterResultRow(FilterMatch match, Action changed)
    {
        Match = match ?? throw new ArgumentNullException(nameof(match));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    public FilterMatch Match { get; }
    public long ElementId => Match.Element.ElementId;
    public string UniqueId => Match.Element.UniqueId;
    public string Category => Match.Element.Category;
    public string Family => Match.Element.Family;
    public string TypeName => Match.Element.TypeName;
    public string Level => Match.Element.Level ?? string.Empty;
    public int MatchedRuleCount => Match.MatchedRules.Count;

    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) _changed(); }
    }
}

public enum FilterResultAction
{
    SelectAndShow,
    TemporaryIsolate,
    TemporaryHide,
    ResetTemporaryIsolation,
    SaveSelectionSet,
    ExportExcel,
    Visualize
}
