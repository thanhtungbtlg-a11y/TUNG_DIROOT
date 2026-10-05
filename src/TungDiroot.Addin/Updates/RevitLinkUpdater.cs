using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Addin.Revit;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Links;
using TungDiroot.Core.Updates;

namespace TungDiroot.Addin.Updates;

public sealed class RevitLinkUpdater
{
    private readonly ScheduleHeaderTableWriter _scheduleWriter;
    private readonly Action<Document, View, WorkbookRangeModel, LinkRecord> _recreate;

    public RevitLinkUpdater(ScheduleHeaderTableWriter scheduleWriter, Action<Document, View, WorkbookRangeModel, LinkRecord> recreate)
    {
        _scheduleWriter = scheduleWriter ?? throw new ArgumentNullException(nameof(scheduleWriter));
        _recreate = recreate ?? throw new ArgumentNullException(nameof(recreate));
    }

    public void Update(Document document, LinkRecord link, UpdatePlan plan)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (link == null) throw new ArgumentNullException(nameof(link));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (plan.Action == UpdateAction.None) return;
        var view = document.GetElement(link.TargetViewUniqueId) as View
            ?? throw new InvalidOperationException("VIEW_DELETED");
        if (plan.Action == UpdateAction.DataOnly)
        {
            if (view is ViewSchedule schedule)
                _scheduleWriter.Write(schedule, plan.After, UpdateBehavior.DataOnly, link.BlackAndWhite);
            else
                UpdateLinkedText(document, link.LinkId, plan.Changes);
            return;
        }

        var linkedElements = LinkedElementCollector.Collect(document, link.LinkId);
        var instances = linkedElements.Where(element => !(element is ElementType)).Select(element => element.Id).ToArray();
        var imageTypes = linkedElements.OfType<ImageType>().Select(type => type.Id.Value).ToArray();
        if (imageTypes.Length > 0)
            LinkedImageDeletionGuard.Validate(imageTypes, instances.Select(id => id.Value),
                new FilteredElementCollector(document).OfClass(typeof(ImageInstance)).Cast<ImageInstance>()
                    .Select(image => new ImageInstanceReference(image.Id.Value, image.GetTypeId().Value)));
        if (instances.Length > 0) document.Delete(instances);
        var types = linkedElements.Where(element => element is ElementType && document.GetElement(element.Id) != null)
            .Select(element => element.Id).ToArray();
        if (types.Length > 0) document.Delete(types);
        _recreate(document, view, plan.After, link);
    }

    private static void UpdateLinkedText(Document document, Guid linkId, IReadOnlyList<CellValueChange> changes)
    {
        var byKey = changes.ToDictionary(change => change.CellKey, StringComparer.OrdinalIgnoreCase);
        foreach (var note in LinkedElementCollector.Collect(document, linkId).OfType<TextNote>())
        {
            if (ElementLinkTagger.TryRead(note, out _, out var cellKey, out var role) &&
                StringComparer.Ordinal.Equals(role, "Text") && byKey.TryGetValue(cellKey, out var change))
                note.Text = change.AfterText;
        }
    }
}
