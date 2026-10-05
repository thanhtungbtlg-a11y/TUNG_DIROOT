using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using TungDiroot.Core.Parameters;
using RevitApplication = Autodesk.Revit.ApplicationServices.Application;

namespace TungDiroot.Addin.Parameters;

public sealed class RevitCategoryCatalog
{
    public IReadOnlyDictionary<string, CategoryAvailability> Read(Document document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var result = new Dictionary<string, CategoryAvailability>(StringComparer.OrdinalIgnoreCase);
        foreach (BuiltInCategory builtIn in Enum.GetValues(typeof(BuiltInCategory)))
        {
            var key = Enum.GetName(typeof(BuiltInCategory), builtIn);
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("OST_", StringComparison.Ordinal)) continue;
            try
            {
                var category = Category.GetCategory(document, builtIn);
                result[key] = category == null
                    ? new CategoryAvailability(key, false, "Category không có trong document")
                    : category.AllowsBoundParameters
                        ? new CategoryAvailability(key, true, null)
                        : new CategoryAvailability(key, false, "Category không cho phép bound parameter");
            }
            catch (Exception exception)
            {
                result[key] = new CategoryAvailability(key, false, Clean(exception.Message));
            }
        }
        return result;
    }

    public CategorySet CreateCategorySet(RevitApplication application, Document document, IEnumerable<string> keys)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        var set = application.Create.NewCategorySet();
        foreach (var key in keys)
        {
            if (!key.StartsWith("OST_", StringComparison.Ordinal) ||
                !Enum.TryParse(key, true, out BuiltInCategory builtIn))
                throw new InvalidOperationException("CATEGORY_KEY_INVALID: " + key);
            var category = Category.GetCategory(document, builtIn)
                ?? throw new InvalidOperationException("CATEGORY_NOT_IN_DOCUMENT: " + key);
            if (!category.AllowsBoundParameters)
                throw new InvalidOperationException("CATEGORY_NOT_BINDABLE: " + key);
            set.Insert(category);
        }
        if (set.IsEmpty) throw new InvalidOperationException("CATEGORY_SET_EMPTY");
        return set;
    }

    private static string Clean(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
