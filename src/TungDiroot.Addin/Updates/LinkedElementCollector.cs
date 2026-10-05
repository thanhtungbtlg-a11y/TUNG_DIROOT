using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Addin.Revit;

namespace TungDiroot.Addin.Updates;

public static class LinkedElementCollector
{
    public static IReadOnlyList<Element> Collect(Document document, Guid linkId)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        return new FilteredElementCollector(document)
            .WhereElementIsNotElementType()
            .Concat(new FilteredElementCollector(document).WhereElementIsElementType())
            .Where(element => ElementLinkTagger.TryRead(element, out var taggedLinkId, out _, out _) && taggedLinkId == linkId)
            .GroupBy(element => element.Id).Select(group => group.First()).ToArray();
    }
}
