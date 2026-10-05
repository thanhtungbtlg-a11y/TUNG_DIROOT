using System;
using System.Linq;

namespace TungDiroot.Core.Families;

public sealed class FamilyReviserPreviewState
{
    private string _path = string.Empty;
    private string _pattern = string.Empty;

    public FamilyReviserPlan? Plan { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanApply => !IsBusy && Plan?.ReadyCount > 0;
    public bool CanEditInputs => !IsBusy;

    public bool ChangeInputs(string path, string? pattern)
    {
        var nextPath = Normalize(path);
        var nextPattern = Normalize(pattern);
        if (Matches(nextPath, nextPattern)) return false;
        _path = nextPath;
        _pattern = nextPattern;
        Invalidate();
        return true;
    }

    public bool SetPreview(string path, string? pattern, FamilyReviserPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (!Matches(Normalize(path), Normalize(pattern)))
        {
            Invalidate();
            return false;
        }
        Plan = new FamilyReviserPlan(plan.Items.ToArray());
        return true;
    }

    public void Invalidate() => Plan = null;
    public void SetBusy(bool value) => IsBusy = value;

    private bool Matches(string path, string pattern) =>
        string.Equals(_path, path, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(_pattern, pattern, StringComparison.Ordinal);
    private static string Normalize(string? value) => (value ?? string.Empty).Trim();
}
