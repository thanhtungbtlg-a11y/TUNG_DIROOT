using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Runtime;
using TungDiroot.Core.Visibility;

namespace TungDiroot.Addin.Visibility;

public sealed class RevitBulkVgExecutor
{
    private readonly Document _document;
    private IReadOnlyDictionary<long, TargetBackup>? _lastBackup;

    public RevitBulkVgExecutor(Document document) =>
        _document = document ?? throw new ArgumentNullException(nameof(document));

    public bool CanRestore => _lastBackup != null && _lastBackup.Count > 0;

    public BulkVgCatalog ReadCatalog(UIApplication application)
    {
        EnsureDocument(application);
        var source = new List<BulkVgViewDescriptor>();
        var targets = new List<BulkVgViewDescriptor>();
        foreach (var view in new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
                     .Where(view => view is not ViewSheet && view.ViewType != ViewType.Internal))
        {
            bool allowed;
            try { allowed = view.AreGraphicsOverridesAllowed(); }
            catch { allowed = false; }
            if (!allowed) continue;
            var kind = view.IsTemplate ? BulkVgTargetKind.ViewTemplate : BulkVgTargetKind.View;
            var prefix = view.IsTemplate ? "[Template] " : "[View] " + view.ViewType + " — ";
            var descriptor = new BulkVgViewDescriptor(view.Id.Value, prefix + view.Name, kind, Array.Empty<long>());
            source.Add(descriptor);
            targets.Add(descriptor);
        }
        foreach (var sheet in new FilteredElementCollector(_document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                     .Where(sheet => !sheet.IsTemplate))
        {
            var viewIds = sheet.GetAllViewports().Select(id => _document.GetElement(id) as Viewport)
                .Where(viewport => viewport != null).Select(viewport => viewport!.ViewId.Value).Distinct().ToArray();
            targets.Add(new BulkVgViewDescriptor(sheet.Id.Value,
                "[Sheet] " + sheet.SheetNumber + " — " + sheet.Name,
                BulkVgTargetKind.Sheet, viewIds));
        }
        return new BulkVgCatalog(
            source.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            targets.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray());
    }

    public IReadOnlyList<BulkVgItemDescriptor> ReadSourceItems(UIApplication application, long sourceViewId)
    {
        EnsureDocument(application);
        var view = RequireView(sourceViewId);
        var result = new List<BulkVgItemDescriptor>();
        foreach (Category category in _document.Settings.Categories)
        {
            try
            {
                if (category == null || !view.IsCategoryOverridable(category.Id)) continue;
                result.Add(new BulkVgItemDescriptor(category.Id.Value, BulkVgItemKind.Category, category.Name));
            }
            catch
            {
                // Revit exposes a few pseudo categories that reject V/G queries.
            }
        }
        foreach (var filterId in view.GetOrderedFilters())
        {
            var name = _document.GetElement(filterId)?.Name ?? "Filter " + filterId.Value;
            result.Add(new BulkVgItemDescriptor(filterId.Value, BulkVgItemKind.Filter, name));
        }
        return result.Where(item => item.Kind == BulkVgItemKind.Category)
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Concat(result.Where(item => item.Kind == BulkVgItemKind.Filter))
            .ToArray();
    }

    public IReadOnlyList<BulkVgPreviewResult> Preview(UIApplication application, BulkVgRequest request)
    {
        EnsureDocument(application);
        Validate(request);
        var source = RequireView(request.SourceViewId);
        var selectedFilters = SelectedSourceFilters(source, request);
        var targets = ExpandTargets(request).Select(RequireView).ToArray();
        var rows = new List<BulkVgPreviewResult>();
        foreach (var target in targets)
        {
            var filterPlan = BulkVgFilterPlanner.Plan(target.GetOrderedFilters().Select(id => id.Value), selectedFilters, request.Mode);
            foreach (var item in request.Items)
            {
                try
                {
                    if (item.Kind == BulkVgItemKind.Filter && filterPlan.Skip.Contains(item.Id))
                    {
                        rows.Add(new BulkVgPreviewResult(target.Name, item.Name, "Filter chưa có — bỏ qua", "Skipped"));
                        continue;
                    }
                    var differences = item.Kind == BulkVgItemKind.Category
                        ? CompareCategory(source, target, new ElementId(item.Id), request.Options)
                        : CompareFilter(source, target, new ElementId(item.Id), request.Options);
                    rows.Add(new BulkVgPreviewResult(target.Name, item.Name,
                        differences.Count == 0 ? "Không đổi" : string.Join(", ", differences), "Ready"));
                }
                catch (Exception exception)
                {
                    rows.Add(new BulkVgPreviewResult(target.Name, item.Name, "—", exception.Message));
                }
            }
            foreach (var id in filterPlan.Remove)
                rows.Add(new BulkVgPreviewResult(target.Name, _document.GetElement(new ElementId(id))?.Name ?? "Filter " + id,
                    "Xóa Filter ngoài lựa chọn (Replace)", "Remove"));
            if (filterPlan.RebuildOrder)
                rows.Add(new BulkVgPreviewResult(target.Name, "Thứ tự Filter",
                    "Sắp xếp theo thứ tự nguồn: " + string.Join(" → ", filterPlan.FinalOrder.Select(id =>
                        _document.GetElement(new ElementId(id))?.Name ?? id.ToString())), "Reorder"));
        }
        return rows;
    }

    public IReadOnlyList<BulkVgApplyResult> Apply(UIApplication application, BulkVgRequest request)
    {
        EnsureDocument(application);
        Validate(request);
        var source = RequireView(request.SourceViewId);
        var selectedFilters = SelectedSourceFilters(source, request);
        var targetIds = ExpandTargets(request);
        var backup = targetIds.ToDictionary(id => id, id => Capture(RequireView(id), request.Items));
        var succeededBackup = new Dictionary<long, TargetBackup>();
        var results = new List<BulkVgApplyResult>();

        using var group = new TransactionGroup(_document, "TUNG_DIROOT - Bulk Visibility & Graphics");
        group.Start();
        foreach (var targetId in targetIds)
        {
            var target = RequireView(targetId);
            using var transaction = new Transaction(_document, "TUNG_DIROOT - Apply V/G: " + target.Name);
            transaction.Start();
            try
            {
                var skippedFilters = ApplyToView(source, target, request, selectedFilters);
                transaction.Commit();
                succeededBackup[targetId] = backup[targetId];
                results.Add(new BulkVgApplyResult(target.Name, true, "OK", skippedFilters));
            }
            catch (Exception exception)
            {
                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                results.Add(new BulkVgApplyResult(target.Name, false, exception.Message));
            }
        }
        group.Assimilate();
        _lastBackup = succeededBackup;
        return results;
    }

    public IReadOnlyList<BulkVgApplyResult> RestoreLast(UIApplication application)
    {
        EnsureDocument(application);
        if (_lastBackup == null || _lastBackup.Count == 0) throw new InvalidOperationException("NO_VG_BACKUP");
        var results = new List<BulkVgApplyResult>();
        using var group = new TransactionGroup(_document, "TUNG_DIROOT - Restore Visibility & Graphics");
        group.Start();
        foreach (var pair in _lastBackup)
        {
            var target = _document.GetElement(new ElementId(pair.Key)) as View;
            if (target == null)
            {
                results.Add(new BulkVgApplyResult("<View bị thiếu>", false, "TARGET_VIEW_NOT_FOUND"));
                continue;
            }
            using var transaction = new Transaction(_document, "TUNG_DIROOT - Restore V/G: " + target.Name);
            transaction.Start();
            try
            {
                Restore(target, pair.Value);
                transaction.Commit();
                results.Add(new BulkVgApplyResult(target.Name, true, "Restored"));
            }
            catch (Exception exception)
            {
                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                results.Add(new BulkVgApplyResult(target.Name, false, exception.Message));
            }
        }
        group.Assimilate();
        _lastBackup = null;
        return results;
    }

    private int ApplyToView(View source, View target, BulkVgRequest request, IReadOnlyList<long> selectedFilters)
    {
        var filterPlan = BulkVgFilterPlanner.Plan(target.GetOrderedFilters().Select(id => id.Value), selectedFilters, request.Mode);
        if (filterPlan.RebuildOrder)
        {
            // Revit 2024 has no order setter. Keep every existing selected filter's complete
            // state before remove/add, including unchecked overrides, visibility and enabled.
            var retained = filterPlan.Update.ToDictionary(id => id, id => CaptureFilter(target, new ElementId(id)));
            foreach (var existing in target.GetFilters().ToArray()) target.RemoveFilter(existing);
            foreach (var filterId in filterPlan.FinalOrder)
            {
                var id = new ElementId(filterId);
                target.AddFilter(id);
                if (retained.TryGetValue(filterId, out var prior)) RestoreFilter(target, prior);
            }
        }
        else
        {
            foreach (var id in filterPlan.Remove) target.RemoveFilter(new ElementId(id));
            foreach (var id in filterPlan.Add) target.AddFilter(new ElementId(id));
        }

        foreach (var item in request.Items)
        {
            var id = new ElementId(item.Id);
            if (item.Kind == BulkVgItemKind.Category)
            {
                if (!target.IsCategoryOverridable(id)) throw new InvalidOperationException("CATEGORY_NOT_OVERRIDABLE: " + item.Name);
                var sourceGraphics = source.GetCategoryOverrides(id);
                var targetGraphics = target.GetCategoryOverrides(id);
                target.SetCategoryOverrides(id, MergeGraphics(sourceGraphics, targetGraphics, request.Options));
                if (request.Options.CopyVisibility && target.CanCategoryBeHidden(id) && source.CanCategoryBeHidden(id))
                    target.SetCategoryHidden(id, source.GetCategoryHidden(id));
                continue;
            }

            if (filterPlan.Skip.Contains(item.Id)) continue;
            if (!source.IsFilterApplied(id)) throw new InvalidOperationException("SOURCE_FILTER_NOT_APPLIED: " + item.Name);
            var sourceFilterGraphics = source.GetFilterOverrides(id);
            var targetFilterGraphics = target.GetFilterOverrides(id);
            target.SetFilterOverrides(id, MergeGraphics(sourceFilterGraphics, targetFilterGraphics, request.Options));
            if (request.Options.CopyVisibility)
            {
                target.SetFilterVisibility(id, source.GetFilterVisibility(id));
                target.SetIsFilterEnabled(id, source.GetIsFilterEnabled(id));
            }
        }
        return filterPlan.Skip.Count;
    }

    private static OverrideGraphicSettings MergeGraphics(
        OverrideGraphicSettings source,
        OverrideGraphicSettings target,
        BulkVgApplyOptions options) => BulkVgOverrideMerger.Merge(source, target, options,
        value => new OverrideGraphicSettings(value),
        (result, value) => result.SetProjectionLineWeight(value.ProjectionLineWeight)
            .SetProjectionLineColor(value.ProjectionLineColor).SetProjectionLinePatternId(value.ProjectionLinePatternId),
        (result, value) => result.SetCutLineWeight(value.CutLineWeight)
            .SetCutLineColor(value.CutLineColor).SetCutLinePatternId(value.CutLinePatternId),
        (result, value) => result.SetSurfaceForegroundPatternId(value.SurfaceForegroundPatternId)
            .SetSurfaceForegroundPatternColor(value.SurfaceForegroundPatternColor)
            .SetSurfaceForegroundPatternVisible(value.IsSurfaceForegroundPatternVisible)
            .SetSurfaceBackgroundPatternId(value.SurfaceBackgroundPatternId)
            .SetSurfaceBackgroundPatternColor(value.SurfaceBackgroundPatternColor)
            .SetSurfaceBackgroundPatternVisible(value.IsSurfaceBackgroundPatternVisible)
            .SetCutForegroundPatternId(value.CutForegroundPatternId)
            .SetCutForegroundPatternColor(value.CutForegroundPatternColor)
            .SetCutForegroundPatternVisible(value.IsCutForegroundPatternVisible)
            .SetCutBackgroundPatternId(value.CutBackgroundPatternId)
            .SetCutBackgroundPatternColor(value.CutBackgroundPatternColor)
            .SetCutBackgroundPatternVisible(value.IsCutBackgroundPatternVisible),
        (result, value) => result.SetSurfaceTransparency(value.Transparency).SetHalftone(value.Halftone));

    private IReadOnlyList<string> CompareCategory(View source, View target, ElementId id, BulkVgApplyOptions options)
    {
        if (!target.IsCategoryOverridable(id)) throw new InvalidOperationException("CATEGORY_NOT_OVERRIDABLE");
        return BulkVgDifferenceDetector.Compare(
            Comparable(source.GetCategoryOverrides(id), source.CanCategoryBeHidden(id) ? !source.GetCategoryHidden(id) : true),
            Comparable(target.GetCategoryOverrides(id), target.CanCategoryBeHidden(id) ? !target.GetCategoryHidden(id) : true),
            options with { CopyVisibility = options.CopyVisibility && source.CanCategoryBeHidden(id) && target.CanCategoryBeHidden(id) });
    }

    private IReadOnlyList<string> CompareFilter(View source, View target, ElementId id, BulkVgApplyOptions options)
    {
        if (!source.IsFilterApplied(id)) throw new InvalidOperationException("SOURCE_FILTER_NOT_APPLIED");
        if (!target.IsFilterApplied(id)) return new[] { "Thêm Filter" };
        return BulkVgDifferenceDetector.Compare(
            Comparable(source.GetFilterOverrides(id), source.GetFilterVisibility(id), source.GetIsFilterEnabled(id)),
            Comparable(target.GetFilterOverrides(id), target.GetFilterVisibility(id), target.GetIsFilterEnabled(id)),
            options);
    }

    private static BulkVgComparableState Comparable(OverrideGraphicSettings graphics, bool visible, bool enabled = true) => new(
        visible,
        graphics.ProjectionLineWeight + ":" + graphics.ProjectionLinePatternId.Value + ":" + ColorSignature(graphics.ProjectionLineColor),
        graphics.CutLineWeight + ":" + graphics.CutLinePatternId.Value + ":" + ColorSignature(graphics.CutLineColor),
        FillSignature(graphics),
        graphics.Transparency,
        graphics.Halftone,
        enabled);

    private static IReadOnlyList<long> SelectedSourceFilters(View source, BulkVgRequest request)
    {
        var selected = new HashSet<long>(request.Items.Where(item => item.Kind == BulkVgItemKind.Filter).Select(item => item.Id));
        if (selected.Count == 0) return Array.Empty<long>();
        var sourceOrder = source.GetOrderedFilters().Select(id => id.Value).ToArray();
        if (selected.Except(sourceOrder).Any()) throw new InvalidOperationException("SOURCE_FILTER_NOT_APPLIED");
        return sourceOrder.Where(selected.Contains).ToArray();
    }

    private static string FillSignature(OverrideGraphicSettings graphics) => string.Join("|", new[]
    {
        graphics.SurfaceForegroundPatternId.Value + ":" + ColorSignature(graphics.SurfaceForegroundPatternColor) + ":" + graphics.IsSurfaceForegroundPatternVisible,
        graphics.SurfaceBackgroundPatternId.Value + ":" + ColorSignature(graphics.SurfaceBackgroundPatternColor) + ":" + graphics.IsSurfaceBackgroundPatternVisible,
        graphics.CutForegroundPatternId.Value + ":" + ColorSignature(graphics.CutForegroundPatternColor) + ":" + graphics.IsCutForegroundPatternVisible,
        graphics.CutBackgroundPatternId.Value + ":" + ColorSignature(graphics.CutBackgroundPatternColor) + ":" + graphics.IsCutBackgroundPatternVisible
    });

    private static string ColorSignature(Color color) => color.IsValid ? color.Red + "," + color.Green + "," + color.Blue : "none";

    private TargetBackup Capture(View view, IReadOnlyList<BulkVgSelectedItem> items)
    {
        var categories = new Dictionary<long, CategoryBackup>();
        foreach (var item in items.Where(item => item.Kind == BulkVgItemKind.Category))
        {
            var id = new ElementId(item.Id);
            if (!view.IsCategoryOverridable(id)) continue;
            categories[item.Id] = new CategoryBackup(
                view.CanCategoryBeHidden(id) && view.GetCategoryHidden(id),
                new OverrideGraphicSettings(view.GetCategoryOverrides(id)));
        }
        var filters = view.GetOrderedFilters().Select(id => CaptureFilter(view, id)).ToArray();
        return new TargetBackup(categories, filters);
    }

    private static FilterBackup CaptureFilter(View view, ElementId id) => new(
        id.Value, view.GetFilterVisibility(id), view.GetIsFilterEnabled(id),
        new OverrideGraphicSettings(view.GetFilterOverrides(id)));

    private static void RestoreFilter(View view, FilterBackup filter)
    {
        var id = new ElementId(filter.Id);
        view.SetFilterOverrides(id, new OverrideGraphicSettings(filter.Graphics));
        view.SetFilterVisibility(id, filter.Visible);
        view.SetIsFilterEnabled(id, filter.Enabled);
    }

    private void Restore(View view, TargetBackup backup)
    {
        foreach (var category in backup.Categories)
        {
            var id = new ElementId(category.Key);
            if (!view.IsCategoryOverridable(id)) continue;
            view.SetCategoryOverrides(id, new OverrideGraphicSettings(category.Value.Graphics));
            if (view.CanCategoryBeHidden(id)) view.SetCategoryHidden(id, category.Value.Hidden);
        }
        foreach (var current in view.GetFilters().ToArray()) view.RemoveFilter(current);
        foreach (var filter in backup.Filters)
        {
            var id = new ElementId(filter.Id);
            if (_document.GetElement(id) == null) continue;
            view.AddFilter(id);
            RestoreFilter(view, filter);
        }
    }

    private IReadOnlyList<long> ExpandTargets(BulkVgRequest request) =>
        BulkVgSelectionPlanner.Expand(request.Targets, request.SourceViewId);

    private View RequireView(long id) => _document.GetElement(new ElementId(id)) as View
        ?? throw new InvalidOperationException("TARGET_VIEW_NOT_FOUND: " + id);

    private static void Validate(BulkVgRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Targets.Count == 0) throw new InvalidOperationException("TARGET_REQUIRED");
        if (request.Items.Count == 0) throw new InvalidOperationException("VG_ITEM_REQUIRED");
        if (!request.Options.HasAny) throw new InvalidOperationException("VG_PROPERTY_REQUIRED");
        if (!Enum.IsDefined(typeof(BulkVgApplyMode), request.Mode)) throw new InvalidOperationException("VG_MODE_INVALID");
    }

    private void EnsureDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("DOCUMENT_NOT_ACTIVE");
    }

    private sealed record CategoryBackup(bool Hidden, OverrideGraphicSettings Graphics);
    private sealed record FilterBackup(long Id, bool Visible, bool Enabled, OverrideGraphicSettings Graphics);
    private sealed record TargetBackup(IReadOnlyDictionary<long, CategoryBackup> Categories, IReadOnlyList<FilterBackup> Filters);
}
