using System;
using TungDiroot.Core.Parameters;

namespace TungDiroot.Presentation.UI;

public sealed class ParameterPlanRowViewModel : BindableBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public ParameterPlanRowViewModel(ParameterAssignmentPlan plan, Action selectionChanged)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
    }

    public ParameterAssignmentPlan Plan { get; }
    public bool CanSelect => Plan.Status == ParameterPlanStatus.Ready;
    public ParameterPlanStatus Status => Plan.Status;
    public string StatusDisplay => ParameterPlanStatusDisplay.For(Plan.Status);
    public string DefinitionGroup => Plan.Rule.DefinitionGroup;
    public string ParameterName => Plan.Rule.ParameterName;
    public string GuidDisplay => Plan.Definition?.Guid.ToString() ?? "—";
    public string BindingDisplay => Plan.Rule.BindingKind?.ToString() ?? "—";
    public string RevitGroupKey => Plan.Rule.RevitGroupKey;
    public string CategoriesDisplay => string.Join("; ", Plan.Rule.CategoryKeys);
    public string Reason => Plan.Reason;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var allowed = CanSelect && value;
            if (!SetProperty(ref _isSelected, allowed)) return;
            _selectionChanged();
        }
    }
}
