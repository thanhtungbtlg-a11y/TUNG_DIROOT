using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.Batch;
using TungDiroot.Core.Links;
using TungDiroot.Core.Import;
using TungDiroot.Core.Runtime;

namespace TungDiroot.Addin.Links;

public sealed class RevitLinkManagementExecutor
{
    private readonly Document _document;
    private readonly ILinkRegistry _registry;

    public RevitLinkManagementExecutor(Document document, ILinkRegistry? registry = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _registry = registry ?? new RevitLinkRegistry();
    }

    public void OpenView(UIApplication application, Guid linkId)
    {
        var link = FindRequiredLink(application, linkId);
        var view = _document.GetElement(link.TargetViewUniqueId) as View;
        EnsureExecutable(LinkManagementAction.OpenView, view != null, SourceExists(link));
        application.ActiveUIDocument.RequestViewChange(view!);
    }

    public void OpenSource(UIApplication application, Guid linkId)
    {
        var link = FindRequiredLink(application, linkId);
        var sourcePath = ResolveSource(link);
        EnsureExecutable(LinkManagementAction.OpenSource, ViewExists(link), File.Exists(sourcePath));
        StartShell(sourcePath);
    }

    public void OpenSourceFolder(UIApplication application, Guid linkId)
    {
        var link = FindRequiredLink(application, linkId);
        var sourcePath = ResolveSource(link);
        EnsureExecutable(LinkManagementAction.OpenSourceFolder, ViewExists(link), File.Exists(sourcePath));
        Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + sourcePath + "\"")
        {
            UseShellExecute = true
        });
    }

    public void UnlinkKeepView(UIApplication application, Guid linkId)
    {
        var link = FindRequiredLink(application, linkId);
        var view = _document.GetElement(link.TargetViewUniqueId) as View;
        var plan = LinkManagementPlanner.Create(LinkManagementAction.UnlinkKeepView, view != null, SourceExists(link));
        ExecuteTransaction("TUNG_DIROOT - Unlink Excel", () =>
        {
            if (plan.ClearViewMarker) view!.DeleteEntity(RevitLinkSchema.ViewMarker);
            if (plan.RemoveRegistryRecord) RemoveRegistryRecord(linkId);
        });
    }

    public void DeleteLinkAndView(UIApplication application, Guid linkId)
    {
        var link = FindRequiredLink(application, linkId);
        var view = _document.GetElement(link.TargetViewUniqueId) as View;
        var plan = LinkManagementPlanner.Create(LinkManagementAction.DeleteLinkAndView, view != null, SourceExists(link));
        ExecuteTransaction("TUNG_DIROOT - Delete Excel link and view", () =>
        {
            if (plan.DeleteTargetView) _document.Delete(view!.Id);
            if (plan.RemoveRegistryRecord) RemoveRegistryRecord(linkId);
        });
    }

    public BatchItemResult RecreateLink(UIApplication application, Guid linkId)
    {
        EnsureActiveDocument(application);
        return new TableGenUpdateProcessor(_registry, expectedDocument: _document)
            .ProcessOne(application, linkId, forceRecreate: true);
    }

    public IReadOnlyList<LinkTargetViewOption> ReadTargetViews(UIApplication application, Guid linkId)
    {
        var link = FindRequiredLink(application, linkId);
        if (link.TargetViewKind == TargetViewKind.Schedule) return Array.Empty<LinkTargetViewOption>();
        return new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
            .Where(view => !view.IsTemplate && !string.Equals(view.UniqueId, link.TargetViewUniqueId, StringComparison.Ordinal))
            .Select(view => new { View = view, Kind = KindOf(view) })
            .Where(item => item.Kind == link.TargetViewKind && !HasOtherLinkMarker(item.View, linkId))
            .Select(item => new LinkTargetViewOption(item.View.UniqueId, item.View.Name, item.Kind!.Value))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public WorkbookImportRequest CreateDuplicateRequest(UIApplication application, Guid linkId)
    {
        var link = FindRequiredLink(application, linkId);
        var view = _document.GetElement(link.TargetViewUniqueId) as View
            ?? throw new InvalidOperationException("VIEW_DELETED");
        var source = ResolveSource(link);
        if (!File.Exists(source)) throw new InvalidOperationException("FILE_MISSING");
        return LinkDuplicateRequestFactory.Create(link, source, view.Name);
    }

    public void ChangeTargetView(UIApplication application, Guid linkId, string targetViewUniqueId)
    {
        var link = FindRequiredLink(application, linkId);
        var sourceView = _document.GetElement(link.TargetViewUniqueId) as View
            ?? throw new InvalidOperationException("VIEW_DELETED");
        var targetView = _document.GetElement(targetViewUniqueId) as View
            ?? throw new InvalidOperationException("TARGET_VIEW_DELETED");
        var targetKind = KindOf(targetView) ?? throw new InvalidOperationException("TARGET_VIEW_KIND_UNSUPPORTED");
        var plan = LinkTargetChangePlanner.Create(
            link.TargetViewKind,
            targetKind,
            string.Equals(sourceView.UniqueId, targetView.UniqueId, StringComparison.Ordinal));
        if (!plan.CanExecute) throw new InvalidOperationException(plan.ErrorCode);
        if (HasOtherLinkMarker(targetView, linkId)) throw new InvalidOperationException("TARGET_VIEW_ALREADY_LINKED");

        using var group = new TransactionGroup(_document, "TUNG_DIROOT - Change TableGen target view");
        group.Start();
        try
        {
            var forced = link with
            {
                TargetViewUniqueId = targetView.UniqueId,
                UpdateBehavior = UpdateBehavior.Recreate,
                LastFingerprint = new SourceFingerprint(0, 0, new string('0', 64)),
                LastResultCode = "TARGET_CHANGE_PENDING"
            };
            using (var prepare = new Transaction(_document, "TUNG_DIROOT - Prepare target view"))
            {
                prepare.Start();
                sourceView.DeleteEntity(RevitLinkSchema.ViewMarker);
                ViewLinkMarker.Write(targetView, forced);
                ReplaceRegistryRecord(linkId, forced);
                prepare.Commit();
            }

            var result = new TableGenUpdateProcessor(_registry, expectedDocument: _document).ProcessOne(application, linkId);
            if (!result.Succeeded || result.Skipped)
                throw new InvalidOperationException(result.ResultCode + (string.IsNullOrWhiteSpace(result.Message) ? string.Empty : ": " + result.Message));

            using (var finish = new Transaction(_document, "TUNG_DIROOT - Finalize target view"))
            {
                finish.Start();
                var latest = _registry.Load(_document).Find(linkId) ?? throw new InvalidOperationException("LINK_NOT_FOUND");
                var restored = latest with { UpdateBehavior = link.UpdateBehavior };
                ViewLinkMarker.Write(targetView, restored);
                ReplaceRegistryRecord(linkId, restored);
                finish.Commit();
            }
            group.Assimilate();
        }
        catch
        {
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            throw;
        }
    }

    private LinkRecord FindRequiredLink(UIApplication application, Guid linkId)
    {
        EnsureActiveDocument(application);
        return _registry.Load(_document).Find(linkId) ?? throw new InvalidOperationException("LINK_NOT_FOUND");
    }

    private void EnsureActiveDocument(UIApplication application)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("DOCUMENT_NOT_ACTIVE");
    }

    private bool ViewExists(LinkRecord link) => _document.GetElement(link.TargetViewUniqueId) is View;

    private bool SourceExists(LinkRecord link)
    {
        try { return File.Exists(ResolveSource(link)); }
        catch { return false; }
    }

    private string ResolveSource(LinkRecord link) => LinkPathResolver.ResolveStoredPath(link.SourcePath, _document.PathName);

    private static void EnsureExecutable(LinkManagementAction action, bool viewExists, bool sourceExists)
    {
        var plan = LinkManagementPlanner.Create(action, viewExists, sourceExists);
        if (!plan.CanExecute) throw new InvalidOperationException(plan.ErrorCode ?? "ACTION_NOT_AVAILABLE");
    }

    private static void StartShell(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void RemoveRegistryRecord(Guid linkId)
    {
        var remaining = _registry.Load(_document).Links.Where(link => link.LinkId != linkId).ToArray();
        _registry.Save(_document, new LinkRegistryModel(remaining));
    }

    private void ReplaceRegistryRecord(Guid linkId, LinkRecord replacement)
    {
        var registry = _registry.Load(_document);
        _registry.Save(_document, new LinkRegistryModel(registry.Links.Select(link => link.LinkId == linkId ? replacement : link)));
    }

    private static TargetViewKind? KindOf(View view) => view.ViewType switch
    {
        ViewType.DraftingView => TargetViewKind.Drafting,
        ViewType.Legend => TargetViewKind.Legend,
        ViewType.Schedule => TargetViewKind.Schedule,
        _ => null
    };

    private static bool HasOtherLinkMarker(View view, Guid linkId)
    {
        try
        {
            var marker = ViewLinkMarker.Read(view);
            return marker != null && marker.LinkId != linkId;
        }
        catch
        {
            return true;
        }
    }

    private void ExecuteTransaction(string name, Action action)
    {
        using var transaction = new Transaction(_document, name);
        transaction.Start();
        try
        {
            action();
            transaction.Commit();
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
    }
}
