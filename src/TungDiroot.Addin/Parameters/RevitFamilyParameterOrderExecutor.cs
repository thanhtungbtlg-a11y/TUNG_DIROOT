using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Parameters;
using TungDiroot.Core.Runtime;

namespace TungDiroot.Addin.Parameters;

public sealed record FamilyParameterReorderResult(int ChangedCount, string BackupPath);

public sealed class RevitFamilyParameterOrderExecutor
{
    private readonly Document _document;

    public RevitFamilyParameterOrderExecutor(Document document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (!_document.IsFamilyDocument) throw new InvalidOperationException("FAMILY_DOCUMENT_REQUIRED");
    }

    public IReadOnlyList<FamilyParameterOrderRow> ReadSnapshot(UIApplication application)
    {
        EnsureDocument(application);
        var groupOrders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<FamilyParameterOrderRow>();
        foreach (var parameter in _document.FamilyManager.GetParameters())
        {
            var group = GroupKey(parameter);
            groupOrders.TryGetValue(group, out var current);
            var order = current + 1;
            groupOrders[group] = order;
            result.Add(new FamilyParameterOrderRow(
                Key(parameter),
                parameter.Definition.Name,
                group,
                order,
                order,
                parameter.IsShared,
                parameter.IsShared ? parameter.GUID : (Guid?)null,
                parameter.IsInstance));
        }
        return result;
    }

    public FamilyParameterReorderResult Apply(
        UIApplication application,
        IReadOnlyList<FamilyParameterOrderRow> editedRows,
        string backupPath)
    {
        EnsureDocument(application);
        var current = ReadSnapshot(application);
        var plan = FamilyParameterOrderPlanner.Plan(editedRows, current);
        if (!plan.IsReady)
            throw new InvalidOperationException("REORDER_REVALIDATION_FAILED: " + string.Join("; ", plan.Issues.Select(issue => issue.Message)));

        FamilyParameterOrderWorkbook.Write(backupPath, current);
        var parameters = _document.FamilyManager.GetParameters();
        var byKey = parameters.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
        var ordered = plan.OrderedKeys.Select(key => byKey.TryGetValue(key, out var parameter)
            ? parameter
            : throw new InvalidOperationException("FAMILY_PARAMETER_NOT_FOUND: " + key)).ToList();

        using var transaction = new Transaction(_document, "TUNG_DIROOT — Reorder Family Parameters");
        if (transaction.Start() != TransactionStatus.Started)
            throw new InvalidOperationException("REVIT_TRANSACTION_NOT_STARTED");
        try
        {
            _document.FamilyManager.ReorderParameters(ordered);
            if (transaction.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException("REVIT_TRANSACTION_NOT_COMMITTED");
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
        return new FamilyParameterReorderResult(plan.Changes.Count, backupPath);
    }

    private static string Key(FamilyParameter parameter)
    {
        if (parameter.IsShared) return "shared:" + parameter.GUID.ToString("D");
        if (parameter.Definition is InternalDefinition definition && definition.BuiltInParameter != BuiltInParameter.INVALID)
            return "builtin:" + ((int)definition.BuiltInParameter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "family:" + parameter.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GroupKey(FamilyParameter parameter) => parameter.Definition.GetGroupTypeId().TypeId;

    private void EnsureDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("Hãy quay lại đúng family đã mở ReOrdering.");
        if (!_document.IsFamilyDocument) throw new InvalidOperationException("FAMILY_DOCUMENT_REQUIRED");
    }
}
