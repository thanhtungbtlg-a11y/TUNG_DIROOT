using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Core.Parameters;

namespace TungDiroot.Addin.Parameters;

public sealed class RevitBindingSnapshotReader
{
    public IReadOnlyList<ExistingParameterBindingModel> Read(Document document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var sharedByDefinitionId = new FilteredElementCollector(document)
            .OfClass(typeof(SharedParameterElement))
            .Cast<SharedParameterElement>()
            .Select(element => new { Element = element, Definition = element.GetDefinition() })
            .Where(item => item.Definition != null)
            .GroupBy(item => item.Definition.Id.Value)
            .ToDictionary(group => group.Key, group => group.First().Element);

        var result = new List<ExistingParameterBindingModel>();
        var iterator = document.ParameterBindings.ForwardIterator();
        iterator.Reset();
        while (iterator.MoveNext())
        {
            if (!(iterator.Key is InternalDefinition definition) ||
                !(iterator.Current is ElementBinding binding) ||
                !sharedByDefinitionId.TryGetValue(definition.Id.Value, out var sharedElement))
                continue;

            var keys = new List<string>();
            foreach (Category category in binding.Categories)
            {
                var key = Enum.GetName(typeof(BuiltInCategory), category.BuiltInCategory);
                if (!string.IsNullOrWhiteSpace(key) && key.StartsWith("OST_", StringComparison.Ordinal)) keys.Add(key);
            }
            result.Add(new ExistingParameterBindingModel(
                sharedElement.GuidValue,
                definition.Name,
                binding is InstanceBinding ? ParameterBindingKind.Instance : ParameterBindingKind.Type,
                keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
        }
        return result;
    }
}
