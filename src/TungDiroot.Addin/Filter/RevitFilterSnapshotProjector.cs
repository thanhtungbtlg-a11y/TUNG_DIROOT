using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Addin.DataLink;
using TungDiroot.Core.DataLink;
using TungDiroot.Core.Filter;
using CoreFilterRule = TungDiroot.Core.Filter.FilterRule;

namespace TungDiroot.Addin.Filter;

public sealed class RevitFilterSnapshotProjector
{
    public IReadOnlyList<FilterElementSnapshot> Project(
        Document document,
        IReadOnlyList<long> elementIds,
        FilterQuery query)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (document.IsLinked) throw new InvalidOperationException("TUNG Filter không đọc trực tiếp linked document.");

        var requested = query.Rules
            .GroupBy(rule => rule.ParameterStableId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var result = new List<FilterElementSnapshot>(elementIds.Count);
        foreach (var rawId in elementIds)
        {
            var element = document.GetElement(new ElementId(rawId));
            if (element?.Category == null) continue;
            var type = RevitFilterCatalogReader.GetType(document, element);
            var values = new Dictionary<string, FilterValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in requested)
                values[rule.ParameterStableId] = ReadValue(element, type, rule);
            result.Add(new FilterElementSnapshot(
                element.UniqueId,
                element.Id.Value,
                element.Category.Id.Value,
                element.Category.Name,
                type?.FamilyName ?? string.Empty,
                type?.Name ?? string.Empty,
                ReadLevel(element),
                values));
        }
        return result;
    }

    internal static string WithScope(string stableId, bool isType) => (isType ? "type:" : "instance:") + stableId;

    internal static FilterValueKind? KindOf(Parameter parameter)
    {
        if (parameter == null) return null;
        return parameter.StorageType switch
        {
            StorageType.String => FilterValueKind.String,
            StorageType.Double => FilterValueKind.Number,
            StorageType.Integer => IsBoolean(parameter) ? FilterValueKind.Boolean : FilterValueKind.Integer,
            _ => null
        };
    }

    private static FilterValue ReadValue(Element instance, ElementType? type, CoreFilterRule rule)
    {
        var isType = rule.ParameterStableId.StartsWith("type:", StringComparison.OrdinalIgnoreCase);
        var isInstance = rule.ParameterStableId.StartsWith("instance:", StringComparison.OrdinalIgnoreCase);
        var rawStableId = isType
            ? rule.ParameterStableId.Substring("type:".Length)
            : isInstance ? rule.ParameterStableId.Substring("instance:".Length) : rule.ParameterStableId;
        var target = isType ? (Element?)type : instance;
        if (target == null) return FilterValue.Missing(rule.Kind);
        var parameter = RevitParameterValueCodec.Resolve(target,
            new DataLinkParameterKey(rawStableId, string.Empty, isType, string.Empty, null));
        if (parameter == null) return FilterValue.Missing(rule.Kind);
        var kind = KindOf(parameter);
        if (kind == null) return FilterValue.Missing(rule.Kind);
        return new FilterValue(true, kind.Value, RevitParameterValueCodec.DisplayValue(parameter),
            RevitParameterValueCodec.InternalValue(parameter));
    }

    private static string? ReadLevel(Element element)
    {
        var parameter = element.get_Parameter(BuiltInParameter.LEVEL_PARAM) ??
                        element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
        return parameter?.AsValueString();
    }

    private static bool IsBoolean(Parameter parameter)
    {
        try
        {
            var typeId = parameter.Definition?.GetDataType().TypeId ?? string.Empty;
            return typeId.IndexOf("bool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeId.IndexOf("yesno", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }
}
