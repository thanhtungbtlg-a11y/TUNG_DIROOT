using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json;
using TungDiroot.Core.Links;

namespace TungDiroot.Addin.Links;

public sealed class RevitLinkRegistry : ILinkRegistry
{
    private const string StorageName = "TUNG_DIROOT.LinkRegistry";

    public LinkRegistryModel Load(Document document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var storage = FindStorage(document);
        if (storage == null) return RecoverFromViews(document);
        var entity = storage.GetEntity(RevitLinkSchema.Registry);
        if (!entity.IsValid()) return RecoverFromViews(document);
        var json = entity.Get<string>("Json");
        try
        {
            var records = string.IsNullOrWhiteSpace(json) ? Array.Empty<LinkRecord>() : JsonConvert.DeserializeObject<LinkRecord[]>(json) ?? Array.Empty<LinkRecord>();
            return new LinkRegistryModel(records.Select(record => LinkSchemaMigrator.TryToCurrent(record).Record));
        }
        catch (JsonException) { return RecoverFromViews(document); }
    }

    public void Save(Document document, LinkRegistryModel registry)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        var unsupported = registry.Links.FirstOrDefault(record => !LinkSchemaMigrator.TryToCurrent(record).CanWrite);
        if (unsupported != null)
            throw new NotSupportedException("SCHEMA_NEWER_THAN_ADDIN");
        var storage = FindStorage(document) ?? DataStorage.Create(document);
        storage.Name = StorageName;
        var entity = new Entity(RevitLinkSchema.Registry);
        entity.Set("Json", JsonConvert.SerializeObject(registry.Links));
        storage.SetEntity(entity);
    }

    public LinkRegistryModel RecoverFromViews(Document document)
    {
        var records = new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>()
            .Select(SafeRead).Where(record => record != null).Cast<LinkRecord>()
            .GroupBy(record => record.LinkId).Select(group => group.First()).ToArray();
        return new LinkRegistryModel(records);
    }

    private static DataStorage? FindStorage(Document document) => new FilteredElementCollector(document)
        .OfClass(typeof(DataStorage)).Cast<DataStorage>()
        .FirstOrDefault(storage => string.Equals(storage.Name, StorageName, StringComparison.Ordinal));

    private static LinkRecord? SafeRead(View view)
    {
        try { return ViewLinkMarker.Read(view); }
        catch (JsonException) { return null; }
    }
}
