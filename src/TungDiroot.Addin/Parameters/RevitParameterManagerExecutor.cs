using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Parameters;
using TungDiroot.Core.Runtime;
using RevitApplication = Autodesk.Revit.ApplicationServices.Application;

namespace TungDiroot.Addin.Parameters;

public sealed record ParameterManagerApplyResult(int ExcelRow, string Name, bool Success, string Message);
public sealed record ParameterManagerBatchResult(IReadOnlyList<ParameterManagerApplyResult> Results, string BackupPath);

public sealed class RevitParameterManagerExecutor
{
    private readonly Document _document;
    private readonly SharedDefinitionResolver _resolver = new();
    private readonly RevitCategoryCatalog _categories = new();

    public RevitParameterManagerExecutor(Document document) => _document = document ?? throw new ArgumentNullException(nameof(document));

    public ParameterManagerScope Scope => _document.IsFamilyDocument ? ParameterManagerScope.Family : ParameterManagerScope.Project;

    public IReadOnlyList<ParameterManagerRow> ReadInventory(UIApplication application)
    {
        EnsureDocument(application);
        return _document.IsFamilyDocument ? ReadFamilyInventory() : ReadProjectInventory();
    }

    public ParameterManagerReferenceCatalog ReadReferenceCatalog(
        UIApplication application,
        IReadOnlyList<SharedParameterDefinitionModel> sharedDefinitions)
    {
        EnsureDocument(application);
        var categoryKeys = _document.IsFamilyDocument
            ? Array.Empty<string>()
            : _categories.Read(_document).Values.Where(item => item.IsAvailable).Select(item => item.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var groups = ParameterUtils.GetAllBuiltInGroups().Where(group => !_document.IsFamilyDocument ||
                _document.FamilyManager.IsUserAssignableParameterGroup(group))
            .Select(group => group.TypeId).Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        return new ParameterManagerReferenceCatalog(Scope, ReadInventory(application), categoryKeys, groups,
            sharedDefinitions ?? Array.Empty<SharedParameterDefinitionModel>());
    }

    public ParameterManagerBatchResult Apply(
        UIApplication application,
        ParameterManagerWorkbook workbook,
        IReadOnlyList<SharedParameterDefinitionModel> sharedDefinitions,
        string sharedParameterPath,
        string backupPath)
    {
        EnsureDocument(application);
        var catalog = ReadReferenceCatalog(application, sharedDefinitions);
        var plan = ParameterManagerPlanner.Plan(workbook, catalog);
        var ready = plan.Items.Where(item => item.Status == ParameterManagerPreviewStatus.Ready).ToArray();
        ParameterManagerWorkbook.Write(backupPath, Scope, catalog.CurrentRows, blankRows: 0);
        if (ready.Length == 0) return new ParameterManagerBatchResult(Array.Empty<ParameterManagerApplyResult>(), backupPath);

        var needsSharedFile = ready.Any(item => item.Row.Action == ParameterManagerAction.CreateShared);
        IReadOnlyList<ParameterManagerApplyResult> results;
        if (needsSharedFile)
        {
            if (string.IsNullOrWhiteSpace(sharedParameterPath))
                throw new InvalidOperationException("SHARED_PARAMETER_FILE_REQUIRED");
            results = _resolver.Use(application.Application, sharedParameterPath,
                file => ApplyResolved(application.Application, file, ready));
        }
        else results = ApplyResolved(application.Application, null, ready);
        return new ParameterManagerBatchResult(results, backupPath);
    }

    private IReadOnlyList<ParameterManagerApplyResult> ApplyResolved(
        RevitApplication application,
        DefinitionFile? definitionFile,
        IReadOnlyList<ParameterManagerPreviewItem> ready)
    {
        var results = new List<ParameterManagerApplyResult>();
        using var group = new TransactionGroup(_document, "TUNG_DIROOT — ParaManager");
        if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("REVIT_TRANSACTION_GROUP_NOT_STARTED");
        try
        {
            foreach (var item in ready)
            {
                using var transaction = new Transaction(_document, "ParaManager — " + item.Row.Name);
                try
                {
                    if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_STARTED");
                    if (_document.IsFamilyDocument) ApplyFamily(item, definitionFile);
                    else ApplyProject(application, item, definitionFile);
                    if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_COMMITTED");
                    results.Add(new ParameterManagerApplyResult(item.ExcelRow, item.Row.Name, true, "Đã áp dụng."));
                }
                catch (Exception exception)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    results.Add(new ParameterManagerApplyResult(item.ExcelRow, item.Row.Name, false, Clean(exception.Message)));
                }
            }
            if (group.Assimilate() != TransactionStatus.Committed) throw new InvalidOperationException("REVIT_TRANSACTION_GROUP_NOT_COMMITTED");
            return results;
        }
        catch
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            throw;
        }
    }

    private void ApplyProject(RevitApplication application, ParameterManagerPreviewItem item, DefinitionFile? file)
    {
        var row = item.Row;
        var categorySet = _categories.CreateCategorySet(application, _document, item.EffectiveCategoryKeys);
        Binding binding = row.BindingKind == ParameterBindingKind.Instance
            ? application.Create.NewInstanceBinding(categorySet)
            : application.Create.NewTypeBinding(categorySet);
        var groupId = new ForgeTypeId(row.ParameterGroupId);
        if (row.Action == ParameterManagerAction.CreateShared)
        {
            var definition = Resolve(file, row);
            if (!_document.ParameterBindings.Insert(definition, binding, groupId))
                throw new InvalidOperationException("REVIT_BINDING_INSERT_RETURNED_FALSE");
            return;
        }
        var entry = ProjectEntries().FirstOrDefault(candidate => Same(candidate.Row.Identity, row.Identity))
            ?? throw new InvalidOperationException("PARAMETER_UPDATE_NOT_FOUND");
        if (!_document.ParameterBindings.ReInsert(entry.Definition, binding, groupId))
            throw new InvalidOperationException("REVIT_BINDING_REINSERT_RETURNED_FALSE");
    }

    private void ApplyFamily(ParameterManagerPreviewItem item, DefinitionFile? file)
    {
        var row = item.Row;
        if (row.Action == ParameterManagerAction.CreateShared)
        {
            _document.FamilyManager.AddParameter(Resolve(file, row), new ForgeTypeId(row.ParameterGroupId),
                row.BindingKind == ParameterBindingKind.Instance);
            return;
        }
        var parameter = _document.FamilyManager.GetParameters().FirstOrDefault(candidate =>
            Same(FamilyKey(candidate), row.Identity)) ?? throw new InvalidOperationException("PARAMETER_UPDATE_NOT_FOUND");
        if (!Same(parameter.Definition.Name, row.Name)) _document.FamilyManager.RenameParameter(parameter, row.Name.Trim());
        if (parameter.Definition is InternalDefinition definition &&
            !Same(definition.GetGroupTypeId().TypeId, row.ParameterGroupId))
            definition.SetGroupTypeId(new ForgeTypeId(row.ParameterGroupId));
    }

    private IReadOnlyList<ParameterManagerRow> ReadProjectInventory() => ProjectEntries().Select(entry => entry.Row)
        .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    private IReadOnlyList<ProjectEntry> ProjectEntries()
    {
        var shared = new FilteredElementCollector(_document).OfClass(typeof(SharedParameterElement)).Cast<SharedParameterElement>()
            .Select(element => new { Element = element, Definition = element.GetDefinition() })
            .Where(item => item.Definition != null)
            .GroupBy(item => item.Definition.Id.Value).ToDictionary(group => group.Key, group => group.First().Element);
        var result = new List<ProjectEntry>();
        var iterator = _document.ParameterBindings.ForwardIterator();
        iterator.Reset();
        while (iterator.MoveNext())
        {
            if (iterator.Key is not InternalDefinition definition || iterator.Current is not ElementBinding binding) continue;
            shared.TryGetValue(definition.Id.Value, out var sharedElement);
            var guid = sharedElement?.GuidValue;
            var categories = new List<string>();
            foreach (Category category in binding.Categories)
            {
                var key = Enum.GetName(typeof(BuiltInCategory), category.BuiltInCategory);
                if (!string.IsNullOrWhiteSpace(key) && key.StartsWith("OST_", StringComparison.Ordinal)) categories.Add(key);
            }
            var row = new ParameterManagerRow(false, ParameterManagerAction.Update, ParameterManagerScope.Project,
                guid.HasValue ? "shared:" + guid.Value.ToString("D") : "definition:" + definition.Id.Value,
                definition.Name, string.Empty, guid,
                binding is InstanceBinding ? ParameterBindingKind.Instance : ParameterBindingKind.Type,
                definition.GetGroupTypeId().TypeId, categories.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray(),
                definition.BuiltInParameter != BuiltInParameter.INVALID);
            result.Add(new ProjectEntry(definition, binding, row));
        }
        return result;
    }

    private IReadOnlyList<ParameterManagerRow> ReadFamilyInventory() => _document.FamilyManager.GetParameters().Select(parameter =>
    {
        var definition = parameter.Definition as InternalDefinition;
        return new ParameterManagerRow(false, ParameterManagerAction.Update, ParameterManagerScope.Family,
            FamilyKey(parameter), parameter.Definition.Name, string.Empty, parameter.IsShared ? parameter.GUID : (Guid?)null,
            parameter.IsInstance ? ParameterBindingKind.Instance : ParameterBindingKind.Type,
            parameter.Definition.GetGroupTypeId().TypeId, Array.Empty<string>(),
            definition != null && definition.BuiltInParameter != BuiltInParameter.INVALID);
    }).OrderBy(row => row.ParameterGroupId).ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    private static ExternalDefinition Resolve(DefinitionFile? file, ParameterManagerRow row)
    {
        if (file == null) throw new InvalidOperationException("SHARED_PARAMETER_FILE_REQUIRED");
        var group = file.Groups.get_Item(row.SharedGroup)
            ?? throw new InvalidOperationException("SHARED_PARAMETER_GROUP_MISSING: " + row.SharedGroup);
        var definition = group.Definitions.get_Item(row.Name) as ExternalDefinition
            ?? throw new InvalidOperationException("SHARED_PARAMETER_DEFINITION_MISSING: " + row.Name);
        if (row.Guid.HasValue && definition.GUID != row.Guid.Value)
            throw new InvalidOperationException("SHARED_PARAMETER_GUID_CHANGED: " + row.Name);
        return definition;
    }

    private static string FamilyKey(FamilyParameter parameter)
    {
        if (parameter.IsShared) return "shared:" + parameter.GUID.ToString("D");
        if (parameter.Definition is InternalDefinition definition && definition.BuiltInParameter != BuiltInParameter.INVALID)
            return "builtin:" + ((int)definition.BuiltInParameter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "family:" + parameter.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void EnsureDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("Hãy quay lại đúng document đã mở ParaManager.");
    }

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
    private sealed record ProjectEntry(InternalDefinition Definition, ElementBinding Binding, ParameterManagerRow Row);
}
