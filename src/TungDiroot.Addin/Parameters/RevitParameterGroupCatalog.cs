using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace TungDiroot.Addin.Parameters;

public sealed class RevitParameterGroupCatalog
{
#pragma warning disable CS0618
    public IReadOnlyCollection<string> Keys { get; } = Enum.GetNames(typeof(BuiltInParameterGroup))
        .Where(name => name.StartsWith("PG_", StringComparison.Ordinal))
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    public ForgeTypeId Parse(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("PG_", StringComparison.OrdinalIgnoreCase) ||
            !Enum.TryParse(key, true, out BuiltInParameterGroup result))
            throw new InvalidOperationException("REVIT_PARAMETER_GROUP_INVALID: " + key);
        return ParameterUtils.GetParameterGroupTypeId(result);
    }
#pragma warning restore CS0618
}
