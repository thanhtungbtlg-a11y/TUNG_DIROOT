using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using TungDiroot.Core.Parameters;
using RevitApplication = Autodesk.Revit.ApplicationServices.Application;

namespace TungDiroot.Addin.Parameters;

public sealed class RevitParameterAssignmentExecutor
{
    private readonly SharedDefinitionResolver _resolver;
    private readonly RevitCategoryCatalog _categories;
    private readonly RevitParameterGroupCatalog _groups;

    public RevitParameterAssignmentExecutor(
        SharedDefinitionResolver? resolver = null,
        RevitCategoryCatalog? categories = null,
        RevitParameterGroupCatalog? groups = null)
    {
        _resolver = resolver ?? new SharedDefinitionResolver();
        _categories = categories ?? new RevitCategoryCatalog();
        _groups = groups ?? new RevitParameterGroupCatalog();
    }

    public IReadOnlyList<ParameterAssignmentResult> Execute(
        Document document,
        RevitApplication application,
        string sharedParameterPath,
        IReadOnlyList<ParameterAssignmentPlan> plans)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (application == null) throw new ArgumentNullException(nameof(application));
        if (plans == null) throw new ArgumentNullException(nameof(plans));
        return _resolver.Use(application, sharedParameterPath, file => ExecuteResolved(document, application, file, plans));
    }

    private IReadOnlyList<ParameterAssignmentResult> ExecuteResolved(
        Document document,
        RevitApplication application,
        DefinitionFile file,
        IReadOnlyList<ParameterAssignmentPlan> plans)
    {
        var results = new List<ParameterAssignmentResult>();
        using var group = new TransactionGroup(document, "TUNG_DIROOT - Gán Shared Parameters");
        if (group.Start() != TransactionStatus.Started)
            throw new InvalidOperationException("REVIT_TRANSACTION_GROUP_NOT_STARTED");
        try
        {
            foreach (var plan in plans)
            {
                if (plan.Status != ParameterPlanStatus.Ready || plan.Definition == null || plan.Rule.BindingKind == null)
                {
                    results.Add(ParameterAssignmentResult.Failed(plan, "PLAN_NOT_READY"));
                    continue;
                }

                try
                {
                    var definition = Resolve(file, plan);
                    var categories = _categories.CreateCategorySet(application, document, plan.Rule.CategoryKeys);
                    Binding binding = plan.Rule.BindingKind == ParameterBindingKind.Instance
                        ? application.Create.NewInstanceBinding(categories)
                        : application.Create.NewTypeBinding(categories);
                    var parameterGroup = _groups.Parse(plan.Rule.RevitGroupKey);

                    using var transaction = new Transaction(document, "Gán parameter - " + definition.Name);
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("REVIT_TRANSACTION_NOT_STARTED");
                    try
                    {
                        if (!document.ParameterBindings.Insert(definition, binding, parameterGroup))
                            throw new InvalidOperationException("REVIT_BINDING_INSERT_RETURNED_FALSE");
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("REVIT_TRANSACTION_NOT_COMMITTED");
                        results.Add(ParameterAssignmentResult.Assigned(plan));
                    }
                    catch
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                        throw;
                    }
                }
                catch (Exception exception)
                {
                    results.Add(ParameterAssignmentResult.Failed(plan, Sanitize(exception.Message)));
                }
            }

            if (group.Assimilate() != TransactionStatus.Committed)
                throw new InvalidOperationException("REVIT_TRANSACTION_GROUP_NOT_COMMITTED");
            return results;
        }
        catch
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            throw;
        }
    }

    private static ExternalDefinition Resolve(DefinitionFile file, ParameterAssignmentPlan plan)
    {
        var group = file.Groups.get_Item(plan.Rule.DefinitionGroup)
            ?? throw new InvalidOperationException("SHARED_PARAMETER_GROUP_MISSING: " + plan.Rule.DefinitionGroup);
        var definition = group.Definitions.get_Item(plan.Rule.ParameterName) as ExternalDefinition
            ?? throw new InvalidOperationException("SHARED_PARAMETER_DEFINITION_MISSING: " + plan.Rule.ParameterName);
        if (definition.GUID != plan.Definition!.Guid)
            throw new InvalidOperationException("SHARED_PARAMETER_GUID_CHANGED: " + plan.Rule.ParameterName);
        return definition;
    }

    private static string Sanitize(string value)
    {
        var cleaned = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length <= 600 ? cleaned : cleaned.Substring(0, 600);
    }
}
