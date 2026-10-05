using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.DataLink;
using TungDiroot.Core.Filter;

namespace TungDiroot.Addin.Filter;

public sealed class RevitFilterCatalogReader
{
    public FilterCatalog Read(UIDocument uiDocument, FilterScope scope)
    {
        if (uiDocument == null) throw new ArgumentNullException(nameof(uiDocument));
        var document = uiDocument.Document;
        if (document.IsLinked) throw new InvalidOperationException("TUNG Filter không đọc trực tiếp linked document.");

        var categories = new Dictionary<long, FilterCategoryDescriptor>();
        var families = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var types = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var parameters = new Dictionary<string, FilterParameterDescriptor>(StringComparer.Ordinal);
        foreach (var element in Collect(uiDocument, scope))
        {
            if (element.Category != null)
                categories[element.Category.Id.Value] = new FilterCategoryDescriptor(element.Category.Id.Value, element.Category.Name);
            AddParameters(element, false, parameters);
            if (GetType(document, element) is not { } type) continue;
            if (!string.IsNullOrWhiteSpace(type.FamilyName)) families.Add(type.FamilyName);
            if (!string.IsNullOrWhiteSpace(type.Name)) types.Add(type.Name);
            AddParameters(type, true, parameters);
        }

        return new FilterCatalog(
            categories.Values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            families.OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            types.OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            parameters.Values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.IsType).ToArray());
    }

    internal static IEnumerable<Element> Collect(UIDocument uiDocument, FilterScope scope)
    {
        var document = uiDocument.Document;
        if (scope == FilterScope.CurrentSelection)
            return uiDocument.Selection.GetElementIds().Select(document.GetElement).Where(element => element != null)!;
        if (scope == FilterScope.ActiveView)
            return new FilteredElementCollector(document, document.ActiveView.Id).WhereElementIsNotElementType().ToElements();
        if (scope == FilterScope.WholeModel)
            return new FilteredElementCollector(document).WhereElementIsNotElementType().ToElements();
        return Array.Empty<Element>();
    }

    internal static ElementType? GetType(Document document, Element element)
    {
        var typeId = element.GetTypeId();
        return typeId == ElementId.InvalidElementId ? null : document.GetElement(typeId) as ElementType;
    }

    private static void AddParameters(
        Element element,
        bool isType,
        IDictionary<string, FilterParameterDescriptor> result)
    {
        foreach (var parameter in element.GetOrderedParameters())
        {
            var kind = RevitFilterSnapshotProjector.KindOf(parameter);
            if (kind == null) continue;
            var key = RevitParameterValueCodec.Describe(parameter, isType);
            var stableId = RevitFilterSnapshotProjector.WithScope(key.StableId, isType);
            result[stableId] = new FilterParameterDescriptor(stableId, key.Name, kind.Value, isType);
        }
    }
}
