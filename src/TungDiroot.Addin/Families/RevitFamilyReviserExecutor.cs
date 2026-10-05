using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Families;
using TungDiroot.Core.Runtime;

namespace TungDiroot.Addin.Families;

public sealed record FamilyReviserApplyResult(int ExcelRow, string Identity, bool Success, string Message);
public sealed record FamilyReviserBatchResult(IReadOnlyList<FamilyReviserApplyResult> Results, string BackupPath);

public sealed class RevitFamilyReviserExecutor
{
    private readonly Document _document;

    public RevitFamilyReviserExecutor(Document document) => _document = document ?? throw new ArgumentNullException(nameof(document));

    public IReadOnlyList<FamilyReviserRow> ReadInventory(UIApplication application)
    {
        EnsureDocument(application);
        var result = new List<FamilyReviserRow>();
        foreach (var family in new FilteredElementCollector(_document).OfClass(typeof(Family)).Cast<Family>()
                     .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var category = family.FamilyCategory?.Name ?? family.Category?.Name ?? string.Empty;
            var editable = family.IsEditable;
            result.Add(new FamilyReviserRow(false, FamilyReviserAction.RenameFamily, family.UniqueId, category,
                family.Name, family.Name, string.Empty, string.Empty, _document.IsFamilyDocument,
                family.IsInPlace, editable));
            foreach (var symbol in family.GetFamilySymbolIds().Select(id => _document.GetElement(id)).OfType<FamilySymbol>()
                         .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                result.Add(new FamilyReviserRow(false, FamilyReviserAction.RenameType, symbol.UniqueId, category,
                    family.Name, family.Name, symbol.Name, symbol.Name, _document.IsFamilyDocument,
                    family.IsInPlace, editable));
        }
        return result;
    }

    public FamilyReviserBatchResult Apply(
        UIApplication application,
        FamilyReviserPlan confirmedPlan,
        string? namingPattern,
        string backupPath)
    {
        EnsureDocument(application);
        var current = ReadInventory(application);
        var plan = FamilyReviserPlanner.RevalidateConfirmed(confirmedPlan, current, namingPattern);
        FamilyReviserWorkbook.Write(backupPath, current);
        var ready = plan.Items.Where(item => item.Status == FamilyReviserPreviewStatus.Ready).ToArray();
        var results = plan.Items.Where(item => item.Status != FamilyReviserPreviewStatus.Ready)
            .Select(item => new FamilyReviserApplyResult(item.ExcelRow, item.Row.UniqueId, false,
                item.Code + ": " + item.Message)).ToList();
        if (ready.Length == 0) return new FamilyReviserBatchResult(results, backupPath);

        using var group = new TransactionGroup(_document, "TUNG_DIROOT — FamilyReviser");
        if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("REVIT_TRANSACTION_GROUP_NOT_STARTED");
        try
        {
            foreach (var item in ready)
            {
                using var transaction = new Transaction(_document, "FamilyReviser — " + DisplayName(item.Row));
                try
                {
                    if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_STARTED");
                    ApplyRow(item.Row);
                    if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_COMMITTED");
                    results.Add(new FamilyReviserApplyResult(item.ExcelRow, item.Row.UniqueId, true, "Đã đổi tên."));
                }
                catch (Exception exception)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    results.Add(new FamilyReviserApplyResult(item.ExcelRow, item.Row.UniqueId, false, Clean(exception.Message)));
                }
            }
            if (group.Assimilate() != TransactionStatus.Committed) throw new InvalidOperationException("REVIT_TRANSACTION_GROUP_NOT_COMMITTED");
            return new FamilyReviserBatchResult(results.OrderBy(item => item.ExcelRow).ToArray(), backupPath);
        }
        catch
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            throw;
        }
    }

    private void ApplyRow(FamilyReviserRow row)
    {
        var element = _document.GetElement(row.UniqueId) ?? throw new InvalidOperationException("ELEMENT_STALE");
        if (row.Action == FamilyReviserAction.RenameFamily)
        {
            if (element is not Family family) throw new InvalidOperationException("FAMILY_IDENTITY_MISMATCH");
            family.Name = row.TargetFamily.Trim();
            return;
        }
        if (row.Action == FamilyReviserAction.RenameType)
        {
            if (element is not FamilySymbol symbol) throw new InvalidOperationException("TYPE_IDENTITY_MISMATCH");
            symbol.Name = row.TargetType.Trim();
        }
    }

    private void EnsureDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("Hãy quay lại đúng document đã mở FamilyReviser.");
    }

    private static string DisplayName(FamilyReviserRow row) => row.Action == FamilyReviserAction.RenameFamily
        ? row.TargetFamily : row.CurrentFamily + " : " + row.TargetType;
    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}
