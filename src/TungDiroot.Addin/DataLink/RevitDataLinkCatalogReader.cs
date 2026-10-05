using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.DataLink;
using TungDiroot.Core.Runtime;

namespace TungDiroot.Addin.DataLink;

public sealed class RevitDataLinkCatalogReader
{
    private readonly Action<long, string, Exception>? _onSkipped;

    public RevitDataLinkCatalogReader(Action<long, string, Exception>? onSkipped = null) => _onSkipped = onSkipped;

    public int SkippedElementCount { get; private set; }

    public DataLinkCatalog Read(UIDocument uiDocument, DataLinkScope scope)
    {
        if (uiDocument == null) throw new ArgumentNullException(nameof(uiDocument));
        var document = uiDocument.Document;
        if (document.IsLinked) throw new InvalidOperationException("DataLink không đọc trực tiếp linked document.");

        var categories = new Dictionary<long, DataLinkCategoryOption>();
        var parameters = new Dictionary<string, DataLinkParameterOption>(StringComparer.Ordinal);
        var instanceSchemas = new HashSet<string>(StringComparer.Ordinal);
        var scannedTypeIds = new HashSet<long>();
        var currentStage = "Element";
        SkippedElementCount = BestEffortBatchRunner.Run(
            Collect(uiDocument, scope),
            element =>
            {
                currentStage = "Category";
                var category = element.Category;
                if (category == null) return;
                var categoryId = category.Id.Value;
                categories[categoryId] = new DataLinkCategoryOption(categoryId, category.Name);

                currentStage = "TypeId";
                var typeId = element.GetTypeId();
                var typeValue = typeId == ElementId.InvalidElementId ? -1L : typeId.Value;
                var schemaKey = categoryId.ToString() + "|" + typeValue.ToString();
                if (!instanceSchemas.Contains(schemaKey))
                {
                    currentStage = "InstanceParameters";
                    AddParameters(element, false, parameters);
                    instanceSchemas.Add(schemaKey);
                }

                if (typeId == ElementId.InvalidElementId || scannedTypeIds.Contains(typeValue)) return;
                currentStage = "TypeParameters";
                if (document.GetElement(typeId) is ElementType type)
                {
                    AddParameters(type, true, parameters);
                    scannedTypeIds.Add(typeValue);
                }
            },
            IsRecoverableReadFailure,
            (element, exception) => _onSkipped?.Invoke(SafeId(element), currentStage, exception));

        return new DataLinkCatalog(
            categories.Values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            parameters.Values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Selection.IsType).ToArray());
    }

    private static bool IsRecoverableReadFailure(Exception exception) =>
        exception is AccessViolationException or Autodesk.Revit.Exceptions.InvalidObjectException;

    private static long SafeId(Element element)
    {
        try
        {
            return element.Id.Value;
        }
        catch
        {
            return -1L;
        }
    }

    private static void AddParameters(
        Element element,
        bool isType,
        IDictionary<string, DataLinkParameterOption> result)
    {
        foreach (var parameter in element.GetOrderedParameters())
        {
            if (parameter.StorageType is StorageType.None or StorageType.ElementId) continue;
            var key = RevitParameterValueCodec.Describe(parameter, isType);
            var dictionaryKey = key.StableId + (isType ? "|T" : "|I");
            result[dictionaryKey] = new DataLinkParameterOption(
                new DataLinkParameterSelection(key.StableId, isType), key.Name, key.StorageKind);
        }
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
}
