using System.Collections.Generic;

namespace TungDiroot.Core.Filter;

public sealed record FilterProfile(
    int SchemaVersion,
    string Name,
    FilterQuery Query,
    string GroupParameterStableId,
    IReadOnlyList<string> PaletteHex)
{
    public const int CurrentSchemaVersion = 1;
}
