using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.Revit;

public sealed class RevitStyleCatalogReader
{
    public RevitStyleCatalogSnapshot Read(Document document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var textTypes = new FilteredElementCollector(document)
            .OfClass(typeof(TextNoteType))
            .Cast<TextNoteType>()
            .Select(type => new RevitStyleOption(type.UniqueId, type.Name))
            .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var graphicsStyles = new List<RevitStyleOption>();
        var lines = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        if (lines?.SubCategories != null)
        {
            foreach (Category category in lines.SubCategories)
            {
                var style = category.GetGraphicsStyle(GraphicsStyleType.Projection);
                if (style != null) graphicsStyles.Add(new RevitStyleOption(style.UniqueId, category.Name));
            }
        }
        return new RevitStyleCatalogSnapshot(
            textTypes,
            graphicsStyles
                .GroupBy(style => style.UniqueId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(style => style.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
    }
}
