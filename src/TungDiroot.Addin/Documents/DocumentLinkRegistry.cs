using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json;
using TungDiroot.Core.Documents;

namespace TungDiroot.Addin.Documents;

public sealed class DocumentLinkRegistry
{
    private const string StorageName = "TUNG_DIROOT.DocumentLinkRegistry";

    public IReadOnlyList<DocumentLinkRecord> Load(Document document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var storage = FindStorage(document);
        if (storage == null) return RecoverFromViews(document);
        var entity = storage.GetEntity(DocumentLinkSchema.Registry);
        if (!entity.IsValid()) return RecoverFromViews(document);
        try
        {
            var json = entity.Get<string>("Json");
            return string.IsNullOrWhiteSpace(json)
                ? Array.Empty<DocumentLinkRecord>()
                : JsonConvert.DeserializeObject<DocumentLinkRecord[]>(json) ?? Array.Empty<DocumentLinkRecord>();
        }
        catch (JsonException)
        {
            return RecoverFromViews(document);
        }
    }

    public void Save(Document document, IEnumerable<DocumentLinkRecord> links)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (links == null) throw new ArgumentNullException(nameof(links));
        var records = links.ToArray();
        if (records.Any(record => record.SchemaVersion > DocumentLinkRecord.CurrentSchemaVersion))
            throw new NotSupportedException("DOCUMENT_SCHEMA_NEWER_THAN_ADDIN");
        var storage = FindStorage(document) ?? DataStorage.Create(document);
        storage.Name = StorageName;
        var entity = new Entity(DocumentLinkSchema.Registry);
        entity.Set("Json", JsonConvert.SerializeObject(records));
        storage.SetEntity(entity);
    }

    public static void WriteViewMarker(View view, DocumentLinkRecord record)
    {
        var entity = new Entity(DocumentLinkSchema.ViewMarker);
        entity.Set("Json", JsonConvert.SerializeObject(record));
        view.SetEntity(entity);
    }

    public static void RemoveViewMarker(View view)
    {
        var schema = Schema.Lookup(DocumentLinkSchema.ViewMarkerGuid);
        if (schema != null) view.DeleteEntity(schema);
    }

    private static IReadOnlyList<DocumentLinkRecord> RecoverFromViews(Document document) =>
        new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>()
            .Select(ReadViewMarker)
            .Where(record => record != null)
            .Cast<DocumentLinkRecord>()
            .GroupBy(record => record.LinkId)
            .Select(group => group.First())
            .ToArray();

    private static DocumentLinkRecord? ReadViewMarker(View view)
    {
        try
        {
            var entity = view.GetEntity(DocumentLinkSchema.ViewMarker);
            if (!entity.IsValid()) return null;
            var json = entity.Get<string>("Json");
            return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<DocumentLinkRecord>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DataStorage? FindStorage(Document document) =>
        new FilteredElementCollector(document).OfClass(typeof(DataStorage)).Cast<DataStorage>()
            .FirstOrDefault(storage => string.Equals(storage.Name, StorageName, StringComparison.Ordinal));
}
