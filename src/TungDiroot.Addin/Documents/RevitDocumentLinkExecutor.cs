using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.Revit;
using TungDiroot.Addin.Updates;
using TungDiroot.Core.Documents;
using TungDiroot.Core.Links;

namespace TungDiroot.Addin.Documents;

public sealed class RevitDocumentLinkExecutor
{
    private const double PageGapFeet = 0.25;
    private readonly Document _document;
    private readonly DocumentLinkRegistry _registry = new();

    public RevitDocumentLinkExecutor(Document document) =>
        _document = document ?? throw new ArgumentNullException(nameof(document));

    public IReadOnlyList<DocumentLinkRecord> Load() => _registry.Load(_document);

    public DocumentLinkRecord ImportPrepared(UIApplication application, PreparedDocumentSource prepared)
    {
        EnsureDocument(application);
        using var transaction = new Transaction(_document, "TUNG_DIROOT - Import PDF/Word");
        transaction.Start();
        try
        {
            var view = CreateDraftingView(prepared.Request.RequestedViewName);
            PlacePages(view, prepared);
            var record = new DocumentLinkRecord(
                DocumentLinkRecord.CurrentSchemaVersion,
                prepared.LinkId,
                view.UniqueId,
                LinkPathResolver.ToStoredPath(prepared.Request.SourcePath, _document.PathName, prepared.Request.PreferRelativePath),
                prepared.Request.SourceKind,
                prepared.Request.Pages.ToString(),
                prepared.Request.Dpi,
                prepared.Request.Columns,
                prepared.PdfPath,
                prepared.Fingerprint,
                DateTimeOffset.UtcNow,
                "OK");
            var links = _registry.Load(_document).Where(link => link.LinkId != record.LinkId).Append(record).ToArray();
            DocumentLinkRegistry.WriteViewMarker(view, record);
            _registry.Save(_document, links);
            transaction.Commit();
            return record;
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
    }

    public DocumentLinkRecord UpdatePrepared(UIApplication application, PreparedDocumentSource prepared)
    {
        EnsureDocument(application);
        var existing = _registry.Load(_document).SingleOrDefault(link => link.LinkId == prepared.LinkId)
            ?? throw new InvalidOperationException("DOCUMENT_LINK_NOT_FOUND");
        var view = _document.GetElement(existing.TargetViewUniqueId) as View
            ?? throw new InvalidOperationException("TARGET_VIEW_NOT_FOUND");
        using var transaction = new Transaction(_document, "TUNG_DIROOT - Update PDF/Word");
        transaction.Start();
        try
        {
            DeleteGeneratedElements(existing.LinkId);
            PlacePages(view, prepared);
            var updated = existing with
            {
                PageExpression = prepared.Request.Pages.ToString(),
                Dpi = prepared.Request.Dpi,
                Columns = prepared.Request.Columns,
                CachedPdfPath = prepared.PdfPath,
                LastFingerprint = prepared.Fingerprint,
                LastUpdatedUtc = DateTimeOffset.UtcNow,
                LastResultCode = "OK"
            };
            var links = _registry.Load(_document).Select(link => link.LinkId == updated.LinkId ? updated : link).ToArray();
            DocumentLinkRegistry.WriteViewMarker(view, updated);
            _registry.Save(_document, links);
            transaction.Commit();
            return updated;
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
    }

    public void OpenView(UIApplication application, Guid linkId)
    {
        EnsureDocument(application);
        var record = Find(linkId);
        var view = _document.GetElement(record.TargetViewUniqueId) as View
            ?? throw new InvalidOperationException("TARGET_VIEW_NOT_FOUND");
        application.ActiveUIDocument.ActiveView = view;
    }

    public void OpenSource(Guid linkId)
    {
        var path = ResolveSource(Find(linkId));
        if (!File.Exists(path)) throw new FileNotFoundException("SOURCE_NOT_FOUND", path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void UnlinkKeepView(UIApplication application, Guid linkId)
    {
        EnsureDocument(application);
        var record = Find(linkId);
        using var transaction = new Transaction(_document, "TUNG_DIROOT - Unlink PDF/Word");
        transaction.Start();
        try
        {
            foreach (var element in LinkedElementCollector.Collect(_document, linkId)) ElementLinkTagger.Remove(element);
            if (_document.GetElement(record.TargetViewUniqueId) is View view) DocumentLinkRegistry.RemoveViewMarker(view);
            _registry.Save(_document, _registry.Load(_document).Where(link => link.LinkId != linkId));
            transaction.Commit();
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
    }

    public void DeleteLinkAndView(UIApplication application, Guid linkId)
    {
        EnsureDocument(application);
        var record = Find(linkId);
        using var transaction = new Transaction(_document, "TUNG_DIROOT - Delete PDF/Word link and View");
        transaction.Start();
        try
        {
            DeleteGeneratedElements(linkId);
            var view = _document.GetElement(record.TargetViewUniqueId);
            if (view != null) _document.Delete(view.Id);
            _registry.Save(_document, _registry.Load(_document).Where(link => link.LinkId != linkId));
            transaction.Commit();
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
    }

    public string ResolveSource(DocumentLinkRecord record) =>
        LinkPathResolver.ResolveStoredPath(record.SourcePath, _document.PathName);

    public string TargetViewName(DocumentLinkRecord record) =>
        (_document.GetElement(record.TargetViewUniqueId) as View)?.Name ?? "<View bị thiếu>";

    private void PlacePages(View view, PreparedDocumentSource prepared)
    {
        var imageTypes = new List<(int Page, ImageType Type)>();
        foreach (var page in prepared.Request.Pages)
        {
            using var options = new ImageTypeOptions(prepared.PdfPath, false, ImageTypeSource.Link)
            {
                PageNumber = page,
                Resolution = (double)(int)prepared.Request.Dpi
            };
            var imageType = ImageType.Create(_document, options);
            imageTypes.Add((page, imageType));
            ElementLinkTagger.Tag(imageType, prepared.LinkId, page.ToString(), "DocumentImageType");
        }

        var layout = DocumentPagePlacementPlanner.Plan(
            imageTypes.Select(item => new DocumentPageSize(item.Page, item.Type.Width, item.Type.Height)).ToArray(),
            prepared.Request.Columns,
            PageGapFeet);
        foreach (var placement in layout)
        {
            var imageType = imageTypes.Single(item => item.Page == placement.PageNumber).Type;
            using var options = new ImagePlacementOptions(
                new XYZ(placement.TopLeftX, placement.TopLeftY, 0), BoxPlacement.TopLeft);
            var instance = ImageInstance.Create(_document, view, imageType.Id, options);
            ElementLinkTagger.Tag(instance, prepared.LinkId, placement.PageNumber.ToString(), "DocumentImageInstance");
        }
    }

    private ViewDrafting CreateDraftingView(string requestedName)
    {
        var type = new FilteredElementCollector(_document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(candidate => candidate.ViewFamily == ViewFamily.Drafting)
            ?? throw new InvalidOperationException("DRAFTING_VIEW_TYPE_NOT_FOUND");
        var view = ViewDrafting.Create(_document, type.Id);
        var existingNames = new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
            .Where(candidate => candidate.Id != view.Id)
            .Select(candidate => candidate.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = requestedName.Trim();
        var name = baseName;
        for (var suffix = 2; existingNames.Contains(name); suffix++) name = baseName + " (" + suffix + ")";
        view.Name = name;
        view.Scale = 1;
        return view;
    }

    private DocumentLinkRecord Find(Guid linkId) =>
        _registry.Load(_document).SingleOrDefault(link => link.LinkId == linkId)
        ?? throw new InvalidOperationException("DOCUMENT_LINK_NOT_FOUND");

    private void DeleteGeneratedElements(Guid linkId)
    {
        var ids = LinkedElementCollector.Collect(_document, linkId).Select(element => element.Id).Distinct().ToArray();
        if (ids.Length > 0) _document.Delete(ids);
    }

    private void EnsureDocument(UIApplication application)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));
        if (!Core.Runtime.NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("DOCUMENT_NOT_ACTIVE");
    }
}
