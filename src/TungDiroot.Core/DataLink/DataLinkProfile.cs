using System.Collections.Generic;

namespace TungDiroot.Core.DataLink;

public sealed record DataLinkProfile(
    int SchemaVersion,
    string Name,
    DataLinkScope Scope,
    IReadOnlyList<long> CategoryIds,
    IReadOnlyList<string> FamilyNames,
    IReadOnlyList<string> TypeNames,
    IReadOnlyList<DataLinkParameterSelection> Parameters)
{
    public const int CurrentSchemaVersion = 1;
}
