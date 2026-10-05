using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Runtime;
using TungDiroot.Core.SheetTools;

namespace TungDiroot.Addin.Sheets;

public sealed record SheetToolApplyResult(SheetLinkRowKind Kind, int ExcelRow, string Key, bool Success, string Message);

public sealed class RevitSheetToolExecutor
{
    private readonly Document _document;

    public RevitSheetToolExecutor(Document document) => _document = document ?? throw new ArgumentNullException(nameof(document));

    public SheetLinkWorkbook ReadWorkbookSnapshot(UIApplication application)
    {
        EnsureDocument(application);
        var revisions = RevisionsById();
        var sheets = SheetElements().Select(sheet => new SheetLinkSheetRow(
            false,
            SheetLinkAction.Update,
            sheet.UniqueId,
            sheet.SheetNumber,
            sheet.Name,
            TitleBlockNameOnSheet(sheet),
            string.Join(";", sheet.GetAdditionalRevisionIds()
                .Select(id => revisions.TryGetValue(id, out var revision) ? revision.SequenceNumber : 0)
                .Where(sequence => sequence > 0)
                .OrderBy(sequence => sequence))))
            .OrderBy(row => row.SheetNumber, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var viewports = new FilteredElementCollector(_document).OfClass(typeof(Viewport)).Cast<Viewport>().ToArray();
        var sheetById = SheetElements().ToDictionary(sheet => sheet.Id);
        var views = SupportedViews().Select(view =>
        {
            var viewport = viewports.FirstOrDefault(item => item.ViewId == view.Id);
            var sheetNumber = viewport != null && sheetById.TryGetValue(viewport.OwnerViewId, out var owner) ? owner.SheetNumber : string.Empty;
            var center = viewport?.GetBoxCenter();
            return new SheetLinkViewRow(
                false,
                SheetLinkAction.Update,
                view.UniqueId,
                view.Name,
                Kind(view.ViewType),
                view is ViewPlan plan ? plan.GenLevel?.Name ?? string.Empty : string.Empty,
                ViewTemplateName(view),
                sheetNumber,
                center == null ? null : FromInternal(center.X),
                center == null ? null : FromInternal(center.Y));
        }).OrderBy(row => row.ViewName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return new SheetLinkWorkbook(sheets, views, ReadRevisionRows(revisions.Values));
    }

    public SheetLinkReferenceCatalog ReadReferenceCatalog(UIApplication application)
    {
        EnsureDocument(application);
        var snapshot = ReadWorkbookSnapshot(application);
        return new SheetLinkReferenceCatalog(
            SheetElements().Select(sheet => new SheetLinkExistingItem(sheet.UniqueId, sheet.SheetNumber)).ToArray(),
            SupportedViews().Select(view => new SheetLinkExistingItem(view.UniqueId, view.Name)).ToArray(),
            TitleBlockTypes().Select(TitleBlockName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray(),
            RevisionsById().Values.Select(revision => revision.SequenceNumber).Distinct().OrderBy(value => value).ToArray(),
            new FilteredElementCollector(_document).OfClass(typeof(Level)).Cast<Level>().Select(level => level.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray(),
            new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>().Where(view => view.IsTemplate)
                .Select(view => view.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray(),
            snapshot.Sheets, snapshot.Views, snapshot.Revisions);
    }

    public IReadOnlyList<SheetToolApplyResult> Apply(
        UIApplication application,
        SheetLinkWorkbook workbook,
        SheetLinkPreviewPlan plan)
    {
        EnsureDocument(application);
        var currentCatalog = ReadReferenceCatalog(application);
        var currentPlan = SheetLinkPreviewPlanner.Plan(workbook, currentCatalog);
        SheetLinkPreviewPlanner.EnsurePreviewStillCurrent(plan, currentPlan);
        var ready = currentPlan.Items.Where(item => item.Status == SheetLinkPreviewStatus.Ready)
            .ToDictionary(item => (item.Kind, item.ExcelRow));
        var revisionIdsByKey = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in currentPlan.ExistingRevisionAliases)
        {
            if (FindByUniqueId<Revision>(alias.Value) is Revision existing)
                revisionIdsByKey[alias.Key] = existing.Id;
        }
        var results = new List<SheetToolApplyResult>();
        using var group = new TransactionGroup(_document, "TUNG_DIROOT — SheetLink / SheetGen");
        if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("REVIT_TRANSACTION_GROUP_NOT_STARTED");
        try
        {
            for (var index = 0; index < workbook.Revisions.Count; index++)
            {
                var row = workbook.Revisions[index];
                var excelRow = workbook.SourceRow(SheetLinkRowKind.Revision, index);
                if (!ready.ContainsKey((SheetLinkRowKind.Revision, excelRow))) continue;
                Revision? revision = null;
                var result = ApplyOne(SheetLinkRowKind.Revision, excelRow, row.RevisionKey,
                    () => revision = ApplyRevision(row), "Revision " + row.RevisionKey);
                results.Add(result);
                if (result.Success && revision != null) revisionIdsByKey[row.RevisionKey.Trim()] = revision.Id;
            }
            for (var index = 0; index < workbook.Sheets.Count; index++)
            {
                var row = workbook.Sheets[index];
                var excelRow = workbook.SourceRow(SheetLinkRowKind.Sheet, index);
                if (!ready.ContainsKey((SheetLinkRowKind.Sheet, excelRow))) continue;
                results.Add(ApplyOne(SheetLinkRowKind.Sheet, excelRow, row.SheetNumber,
                    () => ApplySheet(row, revisionIdsByKey), "Sheet " + row.SheetNumber));
            }
            for (var index = 0; index < workbook.Views.Count; index++)
            {
                var row = workbook.Views[index];
                var excelRow = workbook.SourceRow(SheetLinkRowKind.View, index);
                if (!ready.ContainsKey((SheetLinkRowKind.View, excelRow))) continue;
                results.Add(ApplyOne(SheetLinkRowKind.View, excelRow, row.ViewName,
                    () => ApplyView(row), "View " + row.ViewName));
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

    private SheetToolApplyResult ApplyOne(SheetLinkRowKind kind, int excelRow, string key, Action action, string transactionName)
    {
        using var transaction = new Transaction(_document, transactionName);
        try
        {
            if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_STARTED");
            action();
            if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_COMMITTED");
            return new SheetToolApplyResult(kind, excelRow, key, true, "Đã áp dụng.");
        }
        catch (Exception exception)
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            return new SheetToolApplyResult(kind, excelRow, key, false, Clean(exception.Message));
        }
    }

    private Revision ApplyRevision(SheetLinkRevisionRow row)
    {
        var revision = row.Action == SheetLinkAction.Create
            ? Revision.Create(_document)
            : FindByUniqueId<Revision>(row.UniqueId) ?? throw new InvalidOperationException("REVISION_UPDATE_NOT_FOUND");
        if (revision.Issued) throw new InvalidOperationException("REVISION_ISSUED_READ_ONLY");
        revision.Description = row.Description.Trim();
        revision.RevisionDate = row.RevisionDate.Trim();
        revision.IssuedBy = row.IssuedBy.Trim();
        revision.IssuedTo = row.IssuedTo.Trim();
        revision.Visibility = row.Visibility switch
        {
            SheetLinkRevisionVisibility.TagOnly => RevisionVisibility.TagVisible,
            SheetLinkRevisionVisibility.Hidden => RevisionVisibility.Hidden,
            _ => RevisionVisibility.CloudAndTagVisible
        };
        // Mark issued only after all metadata is set; the API locks these fields once issued.
        revision.Issued = row.Issued;
        return revision;
    }

    private void ApplySheet(SheetLinkSheetRow row, IReadOnlyDictionary<string, ElementId> revisionIdsByKey)
    {
        var titleBlock = FindTitleBlockType(row.TitleBlockType)
            ?? throw new InvalidOperationException("TITLE_BLOCK_NOT_FOUND: " + row.TitleBlockType);
        ViewSheet sheet;
        if (row.Action == SheetLinkAction.Create)
        {
            sheet = ViewSheet.Create(_document, titleBlock.Id);
        }
        else
        {
            sheet = FindByUniqueId<ViewSheet>(row.UniqueId) ?? throw new InvalidOperationException("SHEET_UPDATE_NOT_FOUND");
            var instance = new FilteredElementCollector(_document, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType().FirstElement();
            if (instance != null && instance.GetTypeId() != titleBlock.Id) instance.ChangeTypeId(titleBlock.Id);
        }
        sheet.SheetNumber = row.SheetNumber.Trim();
        sheet.Name = row.SheetName.Trim();
        var revisions = RevisionsById().Values.ToDictionary(revision => revision.SequenceNumber);
        var ids = SheetLinkPreviewPlanner.ParseRevisionReferences(row.RevisionSequences).Select(reference =>
        {
            if (reference.RevisionKey != null)
                return revisionIdsByKey.TryGetValue(reference.RevisionKey, out var id) ? id
                    : throw new InvalidOperationException("REVISION_KEY_NOT_CREATED: " + reference.RevisionKey);
            return revisions.TryGetValue(reference.SequenceNumber!.Value, out var revision) ? revision.Id
                : throw new InvalidOperationException("REVISION_NOT_FOUND: " + reference.SequenceNumber);
        }).Distinct().ToList();
        sheet.SetAdditionalRevisionIds(ids);
    }

    private void ApplyView(SheetLinkViewRow row)
    {
        View view;
        if (row.Action == SheetLinkAction.Create)
        {
            view = CreateView(row);
        }
        else
        {
            view = FindByUniqueId<View>(row.UniqueId) ?? throw new InvalidOperationException("VIEW_UPDATE_NOT_FOUND");
            if (Kind(view.ViewType) != row.ViewKind) throw new InvalidOperationException("VIEW_KIND_UPDATE_BLOCKED");
            var levelName = view is ViewPlan plan ? plan.GenLevel?.Name ?? string.Empty : string.Empty;
            if (!Same(levelName, row.LevelName)) throw new InvalidOperationException("VIEW_LEVEL_UPDATE_BLOCKED");
        }
        view.Name = row.ViewName.Trim();
        if (string.IsNullOrWhiteSpace(row.ViewTemplate)) view.ViewTemplateId = ElementId.InvalidElementId;
        else
        {
            var template = new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
                .FirstOrDefault(item => item.IsTemplate && Same(item.Name, row.ViewTemplate));
            view.ViewTemplateId = template?.Id ?? throw new InvalidOperationException("VIEW_TEMPLATE_NOT_FOUND: " + row.ViewTemplate);
        }
        ApplyViewport(view, row);
    }

    private View CreateView(SheetLinkViewRow row)
    {
        var family = row.ViewKind switch
        {
            SheetLinkViewKind.FloorPlan => ViewFamily.FloorPlan,
            SheetLinkViewKind.CeilingPlan => ViewFamily.CeilingPlan,
            _ => ViewFamily.Drafting
        };
        var type = new FilteredElementCollector(_document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(item => item.ViewFamily == family)
            ?? throw new InvalidOperationException("VIEW_FAMILY_TYPE_NOT_FOUND: " + family);
        if (row.ViewKind == SheetLinkViewKind.Drafting) return ViewDrafting.Create(_document, type.Id);
        var level = new FilteredElementCollector(_document).OfClass(typeof(Level)).Cast<Level>()
            .FirstOrDefault(item => Same(item.Name, row.LevelName))
            ?? throw new InvalidOperationException("LEVEL_NOT_FOUND: " + row.LevelName);
        return ViewPlan.Create(_document, type.Id, level.Id);
    }

    private void ApplyViewport(View view, SheetLinkViewRow row)
    {
        if (string.IsNullOrWhiteSpace(row.SheetNumber)) return;
        if (!row.CenterXMillimeters.HasValue || !row.CenterYMillimeters.HasValue)
            throw new InvalidOperationException("VIEWPORT_COORDINATES_REQUIRED");
        var sheet = SheetElements().FirstOrDefault(item => Same(item.SheetNumber, row.SheetNumber))
            ?? throw new InvalidOperationException("VIEWPORT_SHEET_NOT_FOUND: " + row.SheetNumber);
        var center = new XYZ(ToInternal(row.CenterXMillimeters.Value), ToInternal(row.CenterYMillimeters.Value), 0D);
        var viewport = new FilteredElementCollector(_document).OfClass(typeof(Viewport)).Cast<Viewport>()
            .FirstOrDefault(item => item.ViewId == view.Id);
        if (viewport != null)
        {
            if (viewport.OwnerViewId != sheet.Id) throw new InvalidOperationException("VIEWPORT_ALREADY_ON_OTHER_SHEET");
            viewport.SetBoxCenter(center);
            return;
        }
        if (!Viewport.CanAddViewToSheet(_document, sheet.Id, view.Id))
            throw new InvalidOperationException("VIEWPORT_CANNOT_ADD_TO_SHEET");
        Viewport.Create(_document, sheet.Id, view.Id, center);
    }

    private IEnumerable<ViewSheet> SheetElements() => new FilteredElementCollector(_document).OfClass(typeof(ViewSheet))
        .Cast<ViewSheet>().Where(sheet => !sheet.IsPlaceholder);

    private IEnumerable<View> SupportedViews() => new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
        .Where(view => !view.IsTemplate && view.ViewType is ViewType.DraftingView or ViewType.FloorPlan or ViewType.CeilingPlan);

    private IEnumerable<FamilySymbol> TitleBlockTypes() => new FilteredElementCollector(_document)
        .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().Cast<FamilySymbol>();

    private FamilySymbol? FindTitleBlockType(string value) => TitleBlockTypes().FirstOrDefault(symbol => Same(TitleBlockName(symbol), value));
    private string TitleBlockNameOnSheet(ViewSheet sheet)
    {
        var instance = new FilteredElementCollector(_document, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsNotElementType().FirstElement();
        return instance != null && _document.GetElement(instance.GetTypeId()) is FamilySymbol symbol ? TitleBlockName(symbol) : string.Empty;
    }

    private static string TitleBlockName(FamilySymbol symbol) => symbol.FamilyName + " : " + symbol.Name;
    private string ViewTemplateName(View view) => view.ViewTemplateId == ElementId.InvalidElementId
        ? string.Empty
        : _document.GetElement(view.ViewTemplateId)?.Name ?? string.Empty;

    private Dictionary<ElementId, Revision> RevisionsById() => Revision.GetAllRevisionIds(_document)
        .Select(id => _document.GetElement(id)).OfType<Revision>().ToDictionary(revision => revision.Id);

    private static IReadOnlyList<SheetLinkRevisionRow> ReadRevisionRows(IEnumerable<Revision> revisions) => revisions
        .OrderBy(revision => revision.SequenceNumber).Select(revision => new SheetLinkRevisionRow(false, SheetLinkAction.Update,
            revision.UniqueId, "R" + revision.SequenceNumber, revision.SequenceNumber, revision.Description, revision.RevisionDate,
            revision.Issued, revision.IssuedBy, revision.IssuedTo, revision.Visibility switch
            {
                RevisionVisibility.TagVisible => SheetLinkRevisionVisibility.TagOnly,
                RevisionVisibility.Hidden => SheetLinkRevisionVisibility.Hidden,
                _ => SheetLinkRevisionVisibility.CloudAndTag
            })).ToArray();

    private T? FindByUniqueId<T>(string? uniqueId) where T : Element => string.IsNullOrWhiteSpace(uniqueId)
        ? null
        : _document.GetElement(uniqueId) as T;

    private void EnsureDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(application.ActiveUIDocument?.Document, _document))
            throw new InvalidOperationException("Hãy quay lại đúng model đã mở SheetLink / SheetGen.");
    }

    private static SheetLinkViewKind Kind(ViewType type) => type switch
    {
        ViewType.FloorPlan => SheetLinkViewKind.FloorPlan,
        ViewType.CeilingPlan => SheetLinkViewKind.CeilingPlan,
        _ => SheetLinkViewKind.Drafting
    };

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static double ToInternal(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
    private static double FromInternal(double value) => UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);
    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}
