using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace TungDiroot.Addin.Revit;

public static class ElementLinkTagger
{
    public static readonly Guid SchemaGuid = new Guid("D155A83C-7DA5-460F-89C7-1627F1158951");

    public static void Tag(Element element, Guid linkId, string cellKey, string role)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        var schema = GetOrCreateSchema();
        var entity = new Entity(schema);
        entity.Set("LinkId", linkId.ToString("D"));
        entity.Set("CellKey", cellKey ?? string.Empty);
        entity.Set("Role", role ?? string.Empty);
        element.SetEntity(entity);
    }

    public static bool TryRead(Element element, out Guid linkId, out string cellKey, out string role)
    {
        linkId = Guid.Empty;
        cellKey = string.Empty;
        role = string.Empty;
        if (element == null) return false;
        var schema = Schema.Lookup(SchemaGuid);
        if (schema == null) return false;
        var entity = element.GetEntity(schema);
        if (!entity.IsValid()) return false;
        if (!Guid.TryParse(entity.Get<string>("LinkId"), out linkId)) return false;
        cellKey = entity.Get<string>("CellKey") ?? string.Empty;
        role = entity.Get<string>("Role") ?? string.Empty;
        return true;
    }

    public static void Remove(Element element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        var schema = Schema.Lookup(SchemaGuid);
        if (schema != null) element.DeleteEntity(schema);
    }

    private static Schema GetOrCreateSchema()
    {
        var existing = Schema.Lookup(SchemaGuid);
        if (existing != null) return existing;
        var builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName("TUNG_DIROOT_ElementLink");
        builder.SetVendorId("TUNG");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField("LinkId", typeof(string));
        builder.AddSimpleField("CellKey", typeof(string));
        builder.AddSimpleField("Role", typeof(string));
        return builder.Finish();
    }
}
