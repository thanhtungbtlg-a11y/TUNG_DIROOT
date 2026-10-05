using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.DataLink;

namespace TungDiroot.Addin.DataLink;

public sealed class RevitDataLinkSnapshotReader
{
    public DataLinkWorkbook Read(UIDocument uiDocument, DataLinkQuery query)
    {
        if (uiDocument == null) throw new ArgumentNullException(nameof(uiDocument));
        if (query == null) throw new ArgumentNullException(nameof(query));
        var document = uiDocument.Document;
        if (document.IsLinked) throw new InvalidOperationException("DataLink không ghi hoặc xuất trực tiếp từ linked document.");

        var categories = new HashSet<long>(query.CategoryIds);
        var elements = Collect(uiDocument, query.Scope)
            .Where(element => element.Category != null)
            .Where(element => categories.Count == 0 || categories.Contains(element.Category.Id.Value));
        var rows = new List<DataLinkElementRow>();
        foreach (var element in elements)
        {
            var type = GetType(document, element);
            foreach (var selected in query.Parameters)
            {
                var target = selected.IsType ? type : element;
                if (target == null) continue;
                var lookup = new DataLinkParameterKey(selected.StableId, string.Empty, selected.IsType, string.Empty, null);
                var parameter = RevitParameterValueCodec.Resolve(target, lookup);
                if (parameter == null) continue;
                var key = RevitParameterValueCodec.Describe(parameter, selected.IsType) with { StableId = selected.StableId };
                var editable = IsEditable(document, target);
                var internalValue = RevitParameterValueCodec.InternalValue(parameter);
                rows.Add(new DataLinkElementRow(element.UniqueId, element.Id.Value, type?.UniqueId,
                    element.Category.Name, type?.FamilyName ?? string.Empty, type?.Name ?? string.Empty,
                    key, internalValue, RevitParameterValueCodec.DisplayValue(parameter),
                    RevitParameterValueCodec.DisplayValue(parameter), parameter.IsReadOnly || !editable));
            }
        }

        return new DataLinkWorkbook(DataLinkWorkbookSchema.CurrentVersion, RevitDataLinkDocumentStamp.Create(document),
            CultureInfo.CurrentCulture.Name, rows);
    }

    private static IEnumerable<Element> Collect(UIDocument uiDocument, DataLinkScope scope)
    {
        var document = uiDocument.Document;
        if (scope == DataLinkScope.CurrentSelection)
            return uiDocument.Selection.GetElementIds().Select(document.GetElement).Where(element => element != null)!;
        var collector = scope == DataLinkScope.ActiveView
            ? new FilteredElementCollector(document, document.ActiveView.Id)
            : new FilteredElementCollector(document);
        return collector.WhereElementIsNotElementType().ToElements();
    }

    private static ElementType? GetType(Document document, Element element)
    {
        var typeId = element.GetTypeId();
        return typeId == ElementId.InvalidElementId ? null : document.GetElement(typeId) as ElementType;
    }

    internal static bool IsEditable(Document document, Element element) =>
        !document.IsWorkshared || WorksharingUtils.GetCheckoutStatus(document, element.Id) != CheckoutStatus.OwnedByOtherUser;
}

internal static class RevitDataLinkDocumentStamp
{
    public static DataLinkDocumentStamp Create(Document document)
    {
        var identity = document.ProjectInformation?.UniqueId;
        if (string.IsNullOrWhiteSpace(identity)) identity = document.PathName;
        if (string.IsNullOrWhiteSpace(identity)) identity = document.Title;
        return new DataLinkDocumentStamp(identity ?? string.Empty);
    }
}
