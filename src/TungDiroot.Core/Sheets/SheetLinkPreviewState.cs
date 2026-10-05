using System;
using System.Linq;

namespace TungDiroot.Core.SheetTools;

/// <summary>Keeps a reviewed batch bound to the exact selected path and asynchronous load generation.</summary>
public sealed class SheetLinkPreviewState
{
    private string _path = string.Empty;
    public long Version { get; private set; }
    public SheetLinkWorkbook? Workbook { get; private set; }
    public SheetLinkPreviewPlan? Plan { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanApply => !IsBusy && Workbook != null && Plan?.ReadyCount > 0;
    public bool CanEditInputs => !IsBusy;

    public bool ChangePath(string? path)
    {
        var normalized = Normalize(path);
        if (string.Equals(normalized, _path, StringComparison.OrdinalIgnoreCase)) return false;
        _path = normalized;
        Invalidate();
        return true;
    }

    public long BeginOperation(string path)
    {
        ChangePath(path);
        Invalidate();
        IsBusy = true;
        return Version;
    }

    public bool CompletePreview(string path, long version, SheetLinkWorkbook workbook, SheetLinkPreviewPlan plan)
    {
        if (workbook == null) throw new ArgumentNullException(nameof(workbook));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (version != Version || !string.Equals(_path, Normalize(path), StringComparison.OrdinalIgnoreCase)) return false;
        Workbook = new SheetLinkWorkbook(workbook.Sheets.ToArray(), workbook.Views.ToArray(), workbook.Revisions.ToArray())
        {
            SheetSourceRows = workbook.SheetSourceRows?.ToArray(), ViewSourceRows = workbook.ViewSourceRows?.ToArray(),
            RevisionSourceRows = workbook.RevisionSourceRows?.ToArray()
        };
        Plan = plan with { Items = plan.Items.ToArray(),
            ExistingRevisionAliases = plan.ExistingRevisionAliases.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase) };
        IsBusy = false;
        return true;
    }

    public void Invalidate()
    {
        Workbook = null;
        Plan = null;
        Version++;
    }
    public void SetBusy(bool value) => IsBusy = value;
    private static string Normalize(string? path) => (path ?? string.Empty).Trim();
}
