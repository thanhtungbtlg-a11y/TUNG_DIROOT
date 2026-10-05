using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Links;

namespace TungDiroot.Addin.Links;

public sealed record RelinkApplyResult(Guid LinkId, RelinkMatchKind Kind, string? NewPath);

public sealed class RelinkService
{
    private readonly ILinkRegistry _registry;
    private readonly IWorkbookReader _reader;

    public RelinkService(ILinkRegistry? registry = null, IWorkbookReader? reader = null)
    {
        _registry = registry ?? new RevitLinkRegistry();
        _reader = reader ?? new OpenXmlWorkbookReader();
    }

    public IReadOnlyList<RelinkApplyResult> ApplyFolder(Document document, string folder)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        var candidates = BuildCandidates(folder);
        var registry = _registry.Load(document);
        var updated = registry.Links.ToArray();
        var results = new List<RelinkApplyResult>();
        for (var index = 0; index < updated.Length; index++)
        {
            var match = RelinkMatcher.Match(updated[index], candidates);
            if (match.Kind == RelinkMatchKind.ExactFileAndWorksheet)
            {
                var record = updated[index] with { SourcePath = LinkPathResolver.ToStoredPath(match.Match!.FullPath, document.PathName, true), LastResultCode = "RELINKED" };
                updated[index] = record;
                if (document.GetElement(record.TargetViewUniqueId) is View view) ViewLinkMarker.Write(view, record);
                results.Add(new RelinkApplyResult(record.LinkId, match.Kind, match.Match.FullPath));
            }
            else results.Add(new RelinkApplyResult(updated[index].LinkId, match.Kind, null));
        }
        _registry.Save(document, new LinkRegistryModel(updated));
        return results.AsReadOnly();
    }

    private IReadOnlyList<CandidateWorkbook> BuildCandidates(string folder)
    {
        var candidates = new List<CandidateWorkbook>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.xls*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var catalog = _reader.ReadCatalog(path);
                candidates.Add(new CandidateWorkbook(path, catalog.Regions.Select(region => region.WorksheetName + "!" + region.Reference.ToA1()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
            }
            catch { /* An unreadable workbook is not a safe relink candidate. */ }
        }
        return candidates.AsReadOnly();
    }
}
