using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.Revit;
using TungDiroot.Addin.Links;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Layout;
using TungDiroot.Core.Links;
using TungDiroot.Core.RevitPlanning;
using TungDiroot.Core.Updates;
using TungDiroot.Rendering;

namespace TungDiroot.Addin.Import;

public sealed class ManualImportExecutor
{
    private readonly IWorkbookReader _reader;
    private readonly IRevitViewCreationContext _viewContext;
    private readonly ILinkRegistry _registry;

    public ManualImportExecutor(IWorkbookReader? reader = null, IRevitViewCreationContext? viewContext = null, ILinkRegistry? registry = null)
    {
        _reader = reader ?? new OpenXmlWorkbookReader();
        _viewContext = viewContext ?? new DefaultRevitViewCreationContext();
        _registry = registry ?? new RevitLinkRegistry();
    }

    public IReadOnlyList<ImportRowResult> Execute(UIApplication uiApplication, IReadOnlyList<WorkbookImportRequest> requests)
    {
        if (uiApplication == null) throw new ArgumentNullException(nameof(uiApplication));
        if (requests == null) throw new ArgumentNullException(nameof(requests));
        var document = uiApplication.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        var results = new List<ImportRowResult>();
        foreach (var request in requests) results.Add(ExecuteOne(document, request));
        return results.AsReadOnly();
    }

    private ImportRowResult ExecuteOne(Document document, WorkbookImportRequest request)
    {
        PreparedWorkbookImage? preparedImage = null;
        try
        {
            var hasLegendSeed = _viewContext.LegendSeed(document) != null;
            var viewName = string.IsNullOrWhiteSpace(request.RequestedViewName)
                ? request.WorksheetName + " " + request.Range.ToA1()
                : request.RequestedViewName!.Trim();
            var creationPlan = ViewCreationPlanner.Plan(new ViewCreationRequest(request.Mode, request.TargetView, hasLegendSeed, viewName));
            if (!creationPlan.CanCreate) throw new InvalidOperationException(creationPlan.ErrorCode);
            var sourceModel = _reader.ReadRange(request.FilePath, request.WorksheetName, request.Range, new RangeReadOptions(request.IncludeHiddenRowsAndColumns));
            var companyProfile = TableGenCompanyProfileResolver.Resolve(request.StyleProfileName);
            var model = NumericDisplayFormatter.Apply(sourceModel, companyProfile.Numbers);
            var existingLinks = _registry.Load(document).Links.ToArray();
            var linkId = Guid.NewGuid();
            if (request.Mode == ImportMode.Image)
                preparedImage = WorkbookImageRenderService.Prepare(model, request.FilePath, request.WorksheetName, request.Range,
                    new ImageRenderOptions(request.Dpi, request.BlackAndWhite, true), request.RendererEngine);

            using var transaction = new Transaction(document, "TUNG_DIROOT - Import Excel");
            try
            {
                transaction.Start();
                var view = new RevitViewFactory(_viewContext).Create(document, creationPlan);
                if (request.Mode == ImportMode.Image)
                    RevitImageWriter.PlacePng(document, view, preparedImage!.Path, linkId);
                else if (view is ViewSchedule schedule)
                    new ScheduleHeaderTableWriter(companyProfile.Styles).Write(schedule, model, UpdateBehavior.Recreate, request.BlackAndWhite);
                else
                {
                    var styleResolver = new RevitStyleResolver(companyProfile.Styles);
                    new DraftingLegendTableWriter(styleResolver).Write(
                        document, view, TableLayoutPlanner.Create(model, new LayoutOptions(request.BlackAndWhite, true)), linkId);
                }
                var fingerprint = SourceFingerprint.Create(request.FilePath, request.WorksheetName, request.Range, sourceModel);
                var record = new LinkRecord(
                    LinkRecord.CurrentSchemaVersion, linkId, view.UniqueId,
                    LinkPathResolver.ToStoredPath(request.FilePath, document.PathName, preferRelative: true),
                    request.WorksheetName, request.Range.ToA1(), request.Mode, request.TargetView,
                    request.UpdateBehavior, false, fingerprint, DateTimeOffset.UtcNow, "SUCCESS",
                    request.Dpi, request.BlackAndWhite, request.IncludeHiddenRowsAndColumns, StructureStamp.Create(model),
                    request.RendererEngine, preparedImage?.RendererKind.ToString(), companyProfile.Name);
                ViewLinkMarker.Write(view, record);
                _registry.Save(document, new LinkRegistryModel(existingLinks.Concat(new[] { record })));
                transaction.Commit();
                return ImportRowResult.Success(view.Id, model.Warnings);
            }
            catch
            {
                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                throw;
            }
        }
        catch (Exception exception)
        {
            return ImportRowResult.Failure(ImportExceptionClassifier.Classify(exception).Code, exception);
        }
        finally { preparedImage?.Dispose(); }
    }

}

internal sealed class DefaultRevitViewCreationContext : IRevitViewCreationContext
{
    public ElementId DraftingViewFamilyTypeId(Document document) => new FilteredElementCollector(document)
        .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
        .First(type => type.ViewFamily == ViewFamily.Drafting).Id;

    public View? LegendSeed(Document document) => new FilteredElementCollector(document)
        .OfClass(typeof(View)).Cast<View>()
        .FirstOrDefault(view => !view.IsTemplate && view.ViewType == ViewType.Legend);

    public ElementId ScheduleCategoryId(Document document) => new ElementId(BuiltInCategory.OST_GenericModel);
}
