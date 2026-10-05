using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json;
using TungDiroot.Core.Links;

namespace TungDiroot.Addin.Links;

public static class ViewLinkMarker
{
    public static void Write(View view, LinkRecord record)
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (record == null) throw new ArgumentNullException(nameof(record));
        var migration = LinkSchemaMigrator.TryToCurrent(record);
        if (!migration.CanWrite) throw new NotSupportedException(migration.WarningCode);
        var entity = new Entity(RevitLinkSchema.ViewMarker);
        entity.Set("Json", JsonConvert.SerializeObject(migration.Record));
        view.SetEntity(entity);
    }

    public static LinkRecord? Read(View view)
    {
        var entity = view.GetEntity(RevitLinkSchema.ViewMarker);
        if (!entity.IsValid()) return null;
        var json = entity.Get<string>("Json");
        return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<LinkRecord>(json);
    }
}
