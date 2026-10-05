using System;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace TungDiroot.Addin.Links;

internal static class RevitLinkSchema
{
    public static readonly Guid RegistryGuid = new Guid("B2C9E0DE-8CE5-4C3A-BE22-8D4938784A44");
    public static readonly Guid ViewMarkerGuid = new Guid("30164CBF-C57D-43BA-BD49-ED0D79CE1B24");

    public static Schema Registry => GetOrCreate(RegistryGuid, "TUNG_DIROOT_LinkRegistry");
    public static Schema ViewMarker => GetOrCreate(ViewMarkerGuid, "TUNG_DIROOT_ViewLinkMarker");

    private static Schema GetOrCreate(Guid guid, string name)
    {
        var schema = Schema.Lookup(guid);
        if (schema != null) return schema;
        var builder = new SchemaBuilder(guid);
        builder.SetSchemaName(name);
        builder.SetVendorId("TUNG");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField("Json", typeof(string));
        return builder.Finish();
    }
}
