using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Filter;

namespace TungDiroot.Addin.Filter;

public sealed class RevitFilterVisualization
{
    public View3D Update(
        UIApplication application,
        string expectedDocumentIdentity,
        string profileName,
        IReadOnlyCollection<string> uniqueIds,
        string groupParameterStableId,
        IReadOnlyList<string> paletteHex)
    {
        var uiDocument = RevitFilterActions.RequireDocument(application, expectedDocumentIdentity);
        if (paletteHex == null || paletteHex.Count == 0)
            throw new ArgumentException("Bảng màu Filter không được để trống.", nameof(paletteHex));
        var document = uiDocument.Document;
        var ids = RevitFilterActions.ResolveElementIds(document, uniqueIds);
        if (ids.Count == 0) throw new InvalidOperationException("Không còn phần tử hợp lệ để trực quan hóa.");
        var viewName = RevitFilterNames.ToolName(profileName);
        View3D view;
        using (var transaction = new Transaction(document, "TUNG Filter - Cập nhật view 3D"))
        {
            transaction.Start();
            view = GetOrCreateView(document, viewName);
            ResetToolView(document, view);
            view.IsolateElementsTemporary(ids);
            ApplyColors(document, view, ids, groupParameterStableId, paletteHex);
            transaction.Commit();
        }
        uiDocument.ActiveView = view;
        return view;
    }

    private static View3D GetOrCreateView(Document document, string viewName)
    {
        var sameName = new FilteredElementCollector(document)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(item => string.Equals(item.Name, viewName, StringComparison.OrdinalIgnoreCase));
        if (sameName != null)
        {
            if (sameName.IsTemplate || sameName is not View3D existing)
                throw new InvalidOperationException("Tên view TUNG Filter đang bị một view/template khác sử dụng.");
            return existing;
        }

        var type = new FilteredElementCollector(document)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(item => item.ViewFamily == ViewFamily.ThreeDimensional) ??
                   throw new InvalidOperationException("Project không có ViewFamilyType 3D.");
        var view = View3D.CreateIsometric(document, type.Id);
        view.Name = viewName;
        return view;
    }

    private static void ResetToolView(Document document, View3D view)
    {
        if (view.IsTemporaryHideIsolateActive())
            view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
        var empty = new OverrideGraphicSettings();
        foreach (var id in new FilteredElementCollector(document, view.Id)
                     .WhereElementIsNotElementType()
                     .ToElementIds())
        {
            view.SetElementOverrides(id, empty);
        }
    }

    private static void ApplyColors(
        Document document,
        View3D view,
        ICollection<ElementId> ids,
        string groupParameterStableId,
        IReadOnlyList<string> paletteHex)
    {
        var snapshots = ProjectGroupingValues(document, ids, groupParameterStableId);
        var solidFillId = new FilteredElementCollector(document)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(item => item.GetFillPattern().IsSolidFill)?.Id;
        foreach (var snapshot in snapshots)
        {
            var key = GroupValue(snapshot, groupParameterStableId);
            var color = ParseColor(paletteHex[StableIndex(key, paletteHex.Count)]);
            var graphics = new OverrideGraphicSettings()
                .SetProjectionLineColor(color)
                .SetSurfaceForegroundPatternColor(color);
            if (solidFillId != null && solidFillId != ElementId.InvalidElementId)
                graphics.SetSurfaceForegroundPatternId(solidFillId);
            view.SetElementOverrides(new ElementId(snapshot.ElementId), graphics);
        }
    }

    private static IReadOnlyList<FilterElementSnapshot> ProjectGroupingValues(
        Document document,
        ICollection<ElementId> ids,
        string groupParameterStableId)
    {
        if (string.IsNullOrWhiteSpace(groupParameterStableId))
        {
            return ids.Select(id => document.GetElement(id))
                .Where(element => element?.Category != null)
                .Select(element => new FilterElementSnapshot(
                    element!.UniqueId,
                    element.Id.Value,
                    element.Category.Id.Value,
                    element.Category.Name,
                    string.Empty,
                    string.Empty,
                    null,
                    new Dictionary<string, FilterValue>()))
                .ToArray();
        }
        var rule = new TungDiroot.Core.Filter.FilterRule(
            groupParameterStableId,
            FilterOperator.IsNotEmpty,
            null,
            FilterValueKind.String);
        var query = new FilterQuery(
            FilterScope.WholeModel,
            Array.Empty<long>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { rule },
            FilterJoin.And);
        return new RevitFilterSnapshotProjector().Project(document, ids.Select(id => id.Value).ToArray(), query);
    }

    private static string GroupValue(FilterElementSnapshot snapshot, string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return "<All>";
        if (!snapshot.Values.TryGetValue(stableId, out var value) || !value.Exists) return "<Missing>";
        return value.Display ?? value.Internal ?? "<Empty>";
    }

    private static int StableIndex(string value, int count)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return (int)(hash % (uint)count);
        }
    }

    private static Color ParseColor(string hex)
    {
        if (hex == null || hex.Length != 7 || hex[0] != '#')
            throw new ArgumentException("Màu Filter phải có định dạng #RRGGBB.", nameof(hex));
        return new Color(
            byte.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
