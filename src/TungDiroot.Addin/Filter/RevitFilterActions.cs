using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Core.Filter;

namespace TungDiroot.Addin.Filter;

public sealed class RevitFilterActions
{
    public void SelectAndShow(
        UIApplication application,
        string expectedDocumentIdentity,
        IReadOnlyCollection<string> uniqueIds)
    {
        var uiDocument = RequireDocument(application, expectedDocumentIdentity);
        var ids = ResolveElementIds(uiDocument.Document, uniqueIds);
        uiDocument.Selection.SetElementIds(ids);
        if (ids.Count > 0) uiDocument.ShowElements(ids);
    }

    public void TemporaryIsolate(
        UIApplication application,
        string expectedDocumentIdentity,
        IReadOnlyCollection<string> uniqueIds)
    {
        var uiDocument = RequireDocument(application, expectedDocumentIdentity);
        var view = RequireEditableView(uiDocument.ActiveView);
        var ids = ResolveElementIds(uiDocument.Document, uniqueIds);
        if (ids.Count == 0) throw new InvalidOperationException("Không còn phần tử hợp lệ để cô lập.");
        using var transaction = new Transaction(uiDocument.Document, "TUNG Filter - Cô lập tạm thời");
        transaction.Start();
        view.IsolateElementsTemporary(ids);
        transaction.Commit();
    }

    public void TemporaryHide(
        UIApplication application,
        string expectedDocumentIdentity,
        IReadOnlyCollection<string> uniqueIds)
    {
        var uiDocument = RequireDocument(application, expectedDocumentIdentity);
        var view = RequireEditableView(uiDocument.ActiveView);
        var ids = ResolveElementIds(uiDocument.Document, uniqueIds);
        if (ids.Count == 0) throw new InvalidOperationException("Không còn phần tử hợp lệ để ẩn.");
        using var transaction = new Transaction(uiDocument.Document, "TUNG Filter - Ẩn tạm thời");
        transaction.Start();
        view.HideElementsTemporary(ids);
        transaction.Commit();
    }

    public void ResetTemporaryIsolation(UIApplication application, string expectedDocumentIdentity)
    {
        var uiDocument = RequireDocument(application, expectedDocumentIdentity);
        var view = RequireEditableView(uiDocument.ActiveView);
        if (!view.IsTemporaryHideIsolateActive()) return;
        using var transaction = new Transaction(uiDocument.Document, "TUNG Filter - Bỏ cô lập tạm thời");
        transaction.Start();
        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
        transaction.Commit();
    }

    public ElementId SaveSelectionSet(
        UIApplication application,
        string expectedDocumentIdentity,
        string profileName,
        IReadOnlyCollection<string> uniqueIds)
    {
        var uiDocument = RequireDocument(application, expectedDocumentIdentity);
        var document = uiDocument.Document;
        var ids = ResolveElementIds(document, uniqueIds);
        var name = RevitFilterNames.ToolName(profileName);
        using var transaction = new Transaction(document, "TUNG Filter - Lưu Selection Filter");
        transaction.Start();
        var filter = new FilteredElementCollector(document)
            .OfClass(typeof(SelectionFilterElement))
            .Cast<SelectionFilterElement>()
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (filter == null) filter = SelectionFilterElement.Create(document, name);
        filter.SetElementIds(ids);
        transaction.Commit();
        return filter.Id;
    }

    public void ExportExcel(
        string path,
        IReadOnlyList<FilterMatch> matches,
        IReadOnlyList<FilterExportColumn> columns)
    {
        FilterResultWorkbookWriter.Write(path, matches, columns);
    }

    internal static UIDocument RequireDocument(UIApplication application, string expectedDocumentIdentity)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));
        var uiDocument = application.ActiveUIDocument ??
                         throw new InvalidOperationException("Không có model Revit đang hoạt động.");
        if (uiDocument.Document.IsLinked)
            throw new InvalidOperationException("TUNG Filter không thay đổi linked document.");
        var current = RevitFilterDocumentIdentity.Create(uiDocument.Document);
        if (!string.Equals(current, expectedDocumentIdentity, StringComparison.Ordinal))
            throw new InvalidOperationException("Model đang hoạt động đã thay đổi. Hãy chạy lại bộ lọc.");
        return uiDocument;
    }

    internal static ICollection<ElementId> ResolveElementIds(
        Document document,
        IReadOnlyCollection<string> uniqueIds)
    {
        if (uniqueIds == null) throw new ArgumentNullException(nameof(uniqueIds));
        var ids = new List<ElementId>(uniqueIds.Count);
        var seen = new HashSet<long>();
        foreach (var uniqueId in uniqueIds.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var element = document.GetElement(uniqueId);
            if (element != null && seen.Add(element.Id.Value)) ids.Add(element.Id);
        }
        return ids;
    }

    private static View RequireEditableView(View view)
    {
        if (view == null || view.IsTemplate)
            throw new InvalidOperationException("View hiện tại không hỗ trợ thao tác này.");
        return view;
    }
}

internal static class RevitFilterNames
{
    private static readonly Regex InvalidCharacters = new(@"[\\/:{}\[\]|;<>?`~]", RegexOptions.CultureInvariant);

    public static string ToolName(string profileName)
    {
        var clean = InvalidCharacters.Replace(profileName ?? string.Empty, "_").Trim();
        if (string.IsNullOrWhiteSpace(clean)) clean = "PROFILE";
        if (clean.Length > 70) clean = clean.Substring(0, 70).Trim();
        return "TUNG_FILTER_" + clean;
    }
}
