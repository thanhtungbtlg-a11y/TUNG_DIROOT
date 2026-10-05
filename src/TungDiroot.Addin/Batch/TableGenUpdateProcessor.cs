using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.Links;
using TungDiroot.Addin.Diagnostics;
using TungDiroot.Core.Diagnostics;
using TungDiroot.Addin.Revit;
using TungDiroot.Addin.Updates;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Layout;
using TungDiroot.Core.Links;
using TungDiroot.Core.Runtime;
using TungDiroot.Core.Updates;
using TungDiroot.Rendering;

namespace TungDiroot.Addin.Batch;

public sealed class TableGenUpdateProcessor : ITableGenBatchItemProcessor
{
    private readonly ILinkRegistry _registry;
    private readonly IWorkbookReader _reader;
    private readonly ITungDirootLogger _logger;
    private readonly Document? _expectedDocument;

    public TableGenUpdateProcessor(ILinkRegistry? registry = null, IWorkbookReader? reader = null, ITungDirootLogger? logger = null, Document? expectedDocument = null)
    {
        _registry = registry ?? new RevitLinkRegistry();
        _reader = reader ?? new OpenXmlWorkbookReader();
        _logger = logger ?? new RollingFileLogger();
        _expectedDocument = expectedDocument;
    }

    public BatchItemResult ProcessOne(UIApplication application, Guid linkId) =>
        ProcessOne(application, linkId, forceRecreate: false);

    public BatchItemResult ProcessOne(UIApplication application, Guid linkId, bool forceRecreate)
    {
        var document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if (_expectedDocument != null && !NativeObjectIdentity.IsSame(_expectedDocument, document))
            return new BatchItemResult(linkId, false, true, "DOCUMENT_NOT_ACTIVE", null);
        var registry = _registry.Load(document);
        var link = registry.Find(linkId);
        if (link == null) return new BatchItemResult(linkId, false, true, "LINK_NOT_FOUND", null);
        if (document.GetElement(link.TargetViewUniqueId) is not View view)
            return new BatchItemResult(linkId, false, true, "VIEW_DELETED", null);
        var editability = WorksharingEditabilityChecker.Check(document, view.Id);
        if (!editability.CanEdit) return new BatchItemResult(linkId, false, true, editability.ReasonCode ?? "VIEW_NOT_EDITABLE", null);
        string path;
        try { path = LinkPathResolver.ResolveStoredPath(link.SourcePath, document.PathName); }
        catch (Exception exception) { return new BatchItemResult(linkId, false, true, "FILE_MISSING", exception.Message); }
        if (!File.Exists(path)) return new BatchItemResult(linkId, false, true, "FILE_MISSING", null);

        PreparedWorkbookImage? preparedImage = null;
        try
        {
            var range = ExcelRangeReference.Parse(link.RegionReference);
            var sourceAfter = _reader.ReadRange(path, link.WorksheetName, range, new RangeReadOptions(link.IncludeHiddenRowsAndColumns));
            var fingerprint = SourceFingerprint.Create(path, link.WorksheetName, range, sourceAfter);
            if (TableGenUpdatePolicy.ShouldSkip(SourceChangeDetector.Compare(link.LastFingerprint, fingerprint), forceRecreate))
                return new BatchItemResult(linkId, true, true, "UNCHANGED", null);
            var companyProfile = TableGenCompanyProfileResolver.Resolve(link.StyleProfileName);
            var after = NumericDisplayFormatter.Apply(sourceAfter, companyProfile.Numbers);
            var plan = CreatePlan(document, link, after, forceRecreate);
            if (plan.Action == UpdateAction.Recreate && link.ImportMode == ImportMode.Image)
                preparedImage = WorkbookImageRenderService.Prepare(after, path, link.WorksheetName, range,
                    new ImageRenderOptions(link.Dpi, link.BlackAndWhite, true), link.RendererEngine);
            using var transaction = new Transaction(document,
                forceRecreate ? "TUNG_DIROOT - Rebuild Excel table" : "TUNG_DIROOT - Update Excel link");
            TableGenUpdatePolicy.RequireTransactionStarted(transaction.Start() == TransactionStatus.Started);
            try
            {
                var updater = new RevitLinkUpdater(new ScheduleHeaderTableWriter(companyProfile.Styles),
                    (targetDocument, targetView, model, targetLink) => Recreate(
                        targetDocument, targetView, model, targetLink, preparedImage, companyProfile.Styles));
                updater.Update(document, link, plan);
                var updated = link with
                {
                    LastFingerprint = fingerprint,
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                    LastResultCode = "SUCCESS",
                    LastStructureStamp = StructureStamp.Create(sourceAfter),
                    LastRendererKind = preparedImage?.RendererKind.ToString() ?? link.LastRendererKind
                };
                ViewLinkMarker.Write(view, updated);
                _registry.Save(document, new LinkRegistryModel(registry.Links.Select(item => item.LinkId == linkId ? updated : item)));
                TableGenUpdatePolicy.RequireTransactionCommitted(transaction.Commit() == TransactionStatus.Committed);
            }
            catch
            {
                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                throw;
            }
            return new BatchItemResult(linkId, true, false, "SUCCESS", null);
        }
        catch (Exception exception)
        {
            var code = ImportExceptionClassifier.Classify(exception).Code;
            return Failure(application, link, code, exception, code is "FILE_MISSING" or "FILE_LOCKED" or "VIEW_NOT_EDITABLE" or "VIEW_DELETED");
        }
        finally { preparedImage?.Dispose(); }
    }

    private static UpdatePlan CreatePlan(Document document, LinkRecord link, WorkbookRangeModel after, bool forceRecreate)
    {
        var structureCompatible = string.Equals(link.LastStructureStamp, StructureStamp.Create(after), StringComparison.Ordinal);
        var plan = TableGenUpdatePolicy.CreatePlan(new UpdatePlanInput(true, structureCompatible, link.UpdateBehavior,
            after, after, structureCompatible ? null : "STRUCTURE_CHANGED", link.ImportMode), forceRecreate);
        if (plan.Action == UpdateAction.DataOnly)
        {
            if (link.TargetViewKind == TargetViewKind.Schedule)
                return new UpdatePlan(UpdateAction.DataOnly, after, Array.Empty<CellValueChange>(), null);
            var notes = LinkedElementCollector.Collect(document, link.LinkId).OfType<TextNote>()
                .Select(note => new { Note = note, Tag = ReadTag(note) }).Where(item => item.Tag != null && item.Tag.Value.Role == "Text")
                .Select(item => new KeyValuePair<string, string>(item.Tag!.Value.CellKey, item.Note.Text)).ToArray();
            return DraftingTextUpdatePlanner.Create(after, notes);
        }
        return plan;
    }

    private static (string CellKey, string Role)? ReadTag(Element element) =>
        ElementLinkTagger.TryRead(element, out _, out var cellKey, out var role) ? (cellKey, role) : null;

    private static void Recreate(
        Document document,
        View view,
        WorkbookRangeModel model,
        LinkRecord link,
        PreparedWorkbookImage? preparedImage,
        StyleMappingProfile styleProfile)
    {
        if (link.ImportMode == ImportMode.Image)
        {
            if (preparedImage == null) throw new InvalidOperationException("IMAGE_NOT_PREPARED");
            RevitImageWriter.PlacePng(document, view, preparedImage.Path, link.LinkId);
        }
        else if (view is ViewSchedule schedule)
        {
            new ScheduleHeaderTableWriter(styleProfile).Write(schedule, model, UpdateBehavior.Recreate, link.BlackAndWhite);
        }
        else
        {
            var resolver = new RevitStyleResolver(styleProfile);
            new DraftingLegendTableWriter(resolver).Write(document, view,
                TableLayoutPlanner.Create(model, new LayoutOptions(link.BlackAndWhite, true)), link.LinkId);
        }
    }

    private BatchItemResult Failure(UIApplication application, LinkRecord link, string code, Exception exception, bool skipped)
    {
        try
        {
            _logger.Write(DiagnosticEvent.ForLinkFailure(link.LinkId, link.SourcePath.Value, code, exception,
                typeof(TableGenUpdateProcessor).Assembly.GetName().Version?.ToString() ?? "unknown",
                application.Application.VersionNumber));
        }
        catch { /* Diagnostics must never replace the import result. */ }
        return new BatchItemResult(link.LinkId, false, skipped, code, exception.Message);
    }
}
