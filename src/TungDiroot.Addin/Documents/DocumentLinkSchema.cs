using System;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace TungDiroot.Addin.Documents;

internal static class DocumentLinkSchema
{
    public static readonly Guid RegistryGuid = new Guid("BA551FB3-B82C-469D-A392-6CE0F32C12C8");
    public static readonly Guid ViewMarkerGuid = new Guid("3CB8D665-DCD7-4E65-B01E-7A222060950A");

    public static Schema Registry => GetOrCreate(RegistryGuid, "TUNG_DIROOT_DocumentLinkRegistry");
    public static Schema ViewMarker => GetOrCreate(ViewMarkerGuid, "TUNG_DIROOT_DocumentViewMarker");

    private static Schema GetOrCreate(Guid id, string name)
    {
        var existing = Schema.Lookup(id);
        if (existing != null) return existing;
        var builder = new SchemaBuilder(id);
        builder.SetSchemaName(name);
        builder.SetVendorId("TUNG");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField("Json", typeof(string));
        return builder.Finish();
    }
}
