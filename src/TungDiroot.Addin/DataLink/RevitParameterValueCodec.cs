using System;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Core.DataLink;

namespace TungDiroot.Addin.DataLink;

public static class RevitParameterValueCodec
{
    public static DataLinkParameterKey Describe(Parameter parameter, bool isType)
    {
        if (parameter == null) throw new ArgumentNullException(nameof(parameter));
        return new DataLinkParameterKey(StableId(parameter), parameter.Definition?.Name ?? "Parameter", isType,
            StorageKind(parameter.StorageType), SpecId(parameter));
    }

    public static Parameter? Resolve(Element element, DataLinkParameterKey key)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (key.StableId.StartsWith("builtin:", StringComparison.Ordinal) &&
            int.TryParse(key.StableId.Substring("builtin:".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var builtIn))
            return element.get_Parameter((BuiltInParameter)builtIn);
        if (key.StableId.StartsWith("shared:", StringComparison.Ordinal) &&
            Guid.TryParse(key.StableId.Substring("shared:".Length), out var guid))
            return element.get_Parameter(guid);
        if (key.StableId.StartsWith("parameter:", StringComparison.Ordinal) &&
            long.TryParse(key.StableId.Substring("parameter:".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parameterId))
            return element.GetOrderedParameters().FirstOrDefault(item => item.Id.Value == parameterId);
        return element.GetOrderedParameters().FirstOrDefault(item =>
            string.Equals(item.Definition?.Name, key.Name, StringComparison.Ordinal) &&
            string.Equals(StorageKind(item.StorageType), key.StorageKind, StringComparison.Ordinal));
    }

    public static string InternalValue(Parameter parameter) => parameter.StorageType switch
    {
        StorageType.String => parameter.AsString() ?? string.Empty,
        StorageType.Integer => parameter.AsInteger().ToString(CultureInfo.InvariantCulture),
        StorageType.Double => parameter.AsDouble().ToString("R", CultureInfo.InvariantCulture),
        StorageType.ElementId => parameter.AsElementId().Value.ToString(CultureInfo.InvariantCulture),
        _ => string.Empty
    };

    public static string DisplayValue(Parameter parameter) => parameter.StorageType switch
    {
        StorageType.String => parameter.AsString() ?? string.Empty,
        StorageType.Integer => parameter.AsValueString() ?? parameter.AsInteger().ToString(CultureInfo.CurrentCulture),
        StorageType.Double => parameter.AsValueString() ?? parameter.AsDouble().ToString(CultureInfo.CurrentCulture),
        StorageType.ElementId => parameter.AsValueString() ?? parameter.AsElementId().Value.ToString(CultureInfo.CurrentCulture),
        _ => string.Empty
    };

    public static bool TrySet(Parameter parameter, string internalValue, out string message)
    {
        if (parameter.IsReadOnly)
        {
            message = "Parameter chỉ đọc.";
            return false;
        }

        switch (parameter.StorageType)
        {
            case StorageType.String:
                if (parameter.Set(internalValue ?? string.Empty)) { message = string.Empty; return true; }
                break;
            case StorageType.Integer when int.TryParse(internalValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer):
                if (parameter.Set(integer)) { message = string.Empty; return true; }
                break;
            case StorageType.Double when double.TryParse(internalValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                if (parameter.Set(number)) { message = string.Empty; return true; }
                break;
            case StorageType.ElementId:
                message = "Parameter tham chiếu ElementId chưa được hỗ trợ trong DataLink v1.";
                return false;
        }

        message = "Không thể chuyển hoặc gán giá trị parameter.";
        return false;
    }

    private static string StableId(Parameter parameter)
    {
        if (parameter.IsShared) return "shared:" + parameter.GUID.ToString("D");
        var id = parameter.Id.Value;
        return id < 0 ? "builtin:" + id.ToString(CultureInfo.InvariantCulture) : "parameter:" + id.ToString(CultureInfo.InvariantCulture);
    }

    private static string StorageKind(StorageType storageType) => storageType switch
    {
        StorageType.String => "String",
        StorageType.Integer => "Integer",
        StorageType.Double => "Double",
        StorageType.ElementId => "ElementId",
        _ => "None"
    };

    private static string? SpecId(Parameter parameter)
    {
        try { return parameter.Definition?.GetDataType().TypeId; }
        catch { return null; }
    }
}
