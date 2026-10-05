using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using TungDiroot.Addin.Batch;
using TungDiroot.Addin.Links;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Links;
using TungDiroot.Core.Sync;

namespace TungDiroot.Addin.Sync;

public sealed class DocumentAutoSyncService : IDisposable
{
    private readonly PendingDocumentQueue _pending = new();
    private readonly ILinkRegistry _registry;
    private readonly IWorkbookReader _reader;
    private RevitBatchCoordinator? _batch;

    public DocumentAutoSyncService(ILinkRegistry? registry = null, IWorkbookReader? reader = null)
    {
        _registry = registry ?? new RevitLinkRegistry();
        _reader = reader ?? new OpenXmlWorkbookReader();
    }

    public void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e) => _pending.Enqueue(DocumentKey(e.Document));

    public void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (sender is not UIApplication uiApplication) return;
        var document = uiApplication.ActiveUIDocument?.Document;
        if (_batch?.IsRunning == true) return;
        if (document == null || !_pending.TryDequeue(DocumentKey(document), out _)) return;
        var candidates = BuildCandidates(document);
        var plan = AutoSyncPlanner.SelectChanged(candidates);
        if (plan.SelectedLinkIds.Count == 0) return;
        _batch?.Dispose();
        _batch = new RevitBatchCoordinator(new TableGenUpdateProcessor(_registry, _reader, expectedDocument: document));
        _batch.Enqueue(plan.SelectedLinkIds);
    }

    private IReadOnlyList<AutoSyncCandidate> BuildCandidates(Document document)
    {
        var items = new List<AutoSyncCandidate>();
        foreach (var link in _registry.Load(document).Links)
        {
            if (!link.AutoSync) { items.Add(new AutoSyncCandidate(link.LinkId, false, false, true)); continue; }
            if (document.GetElement(link.TargetViewUniqueId) is not View)
            {
                items.Add(new AutoSyncCandidate(link.LinkId, true, true, false, "VIEW_DELETED"));
                continue;
            }
            try
            {
                var path = LinkPathResolver.ResolveStoredPath(link.SourcePath, document.PathName);
                if (!File.Exists(path)) { items.Add(new AutoSyncCandidate(link.LinkId, true, false, true, "FILE_MISSING")); continue; }
                var range = ExcelRangeReference.Parse(link.RegionReference);
                var model = _reader.ReadRange(path, link.WorksheetName, range, new RangeReadOptions(link.IncludeHiddenRowsAndColumns));
                var fingerprint = SourceFingerprint.Create(path, link.WorksheetName, range, model);
                items.Add(new AutoSyncCandidate(link.LinkId, true, SourceChangeDetector.Compare(link.LastFingerprint, fingerprint) == SourceChange.Changed, true));
            }
            catch { items.Add(new AutoSyncCandidate(link.LinkId, true, false, true, "SOURCE_CHECK_FAILED")); }
        }
        return items.AsReadOnly();
    }

    public void Dispose() => _batch?.Dispose();

    private static string DocumentKey(Document document) => document.GetHashCode().ToString("X8") + "|" + document.PathName + "|" + document.Title;
}
